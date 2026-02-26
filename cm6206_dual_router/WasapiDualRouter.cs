using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Cm6206DualRouter;

public sealed class WasapiDualRouter : IDisposable
{
    private RouterConfig _config;

    public int EffectiveSampleRate => _config.SampleRate;
    public string? FormatWarning { get; }

    private readonly MMDevice? _musicDevice;
    private readonly MMDevice? _shakerDevice;
    private readonly MMDevice _outputDevice;

    private readonly WasapiLoopbackCapture? _musicCapture;
    private readonly WasapiLoopbackCapture? _shakerCapture;

    private readonly CmvadrIoctlInput? _musicIoctl;
    private readonly CmvadrIoctlInput? _shakerIoctl;

    private readonly BufferedWaveProvider _musicBuffer;
    private readonly BufferedWaveProvider? _shakerBuffer;

    private readonly WasapiOut _output;
    private readonly ManualResetEventSlim _stopped = new(false);

    private int _stopRequested;

    private readonly object _meterLock = new();
    private float _musicPeakL;
    private float _musicPeakR;
    private float _shakerPeakL;
    private float _shakerPeakR;
    private readonly float[] _outputPeaks = new float[8];

    public WasapiDualRouter(RouterConfig config)
    {
        _config = config;

        var ingest = (_config.InputIngestMode ?? "WasapiLoopback").Trim();

        if (ingest == "WasapiLoopback")
        {
            _musicDevice = DeviceHelper.GetRenderDeviceByFriendlyName(config.MusicInputRenderDevice);
            if (!string.IsNullOrWhiteSpace(config.ShakerInputRenderDevice))
                _shakerDevice = DeviceHelper.GetRenderDeviceByFriendlyName(config.ShakerInputRenderDevice);
        }
        else
        {
            _musicIoctl = CmvadrIoctlInput.Open(VirtualAudioDriverIoctl.GameDeviceWin32Path);
            if (!string.IsNullOrWhiteSpace(config.ShakerInputRenderDevice))
                _shakerIoctl = CmvadrIoctlInput.Open(VirtualAudioDriverIoctl.ShakerDeviceWin32Path);
        }

        _outputDevice = DeviceHelper.GetRenderDeviceByFriendlyName(config.OutputRenderDevice);

        // Negotiate effective format (shared uses mix format; exclusive can fall back).
        var negotiation = OutputFormatNegotiator.Negotiate(_config, _outputDevice);
        _config = negotiation.EffectiveConfig;
        FormatWarning = negotiation.Warning;

        if (ingest == "WasapiLoopback")
        {
            if (_musicDevice is null)
                throw new InvalidOperationException("music device was not resolved");

            _musicCapture = new WasapiLoopbackCapture(_musicDevice);
            if (_shakerDevice is not null)
                _shakerCapture = new WasapiLoopbackCapture(_shakerDevice);

            _musicBuffer = new BufferedWaveProvider(_musicCapture.WaveFormat)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(2)
            };

            if (_shakerCapture is not null)
            {
                _shakerBuffer = new BufferedWaveProvider(_shakerCapture.WaveFormat)
                {
                    DiscardOnBufferOverflow = true,
                    BufferDuration = TimeSpan.FromSeconds(2)
                };
            }

            _musicCapture.DataAvailable += (_, e) =>
            {
                if (Volatile.Read(ref _stopRequested) != 0) return;
                _musicBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
            };

            if (_shakerCapture is not null && _shakerBuffer is not null)
            {
                _shakerCapture.DataAvailable += (_, e) =>
                {
                    if (Volatile.Read(ref _stopRequested) != 0) return;
                    _shakerBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                };
            }

            _musicCapture.RecordingStopped += (_, __) => Stop();
            if (_shakerCapture is not null)
                _shakerCapture.RecordingStopped += (_, __) => Stop();
        }
        else
        {
            if (_musicIoctl is null)
                throw new InvalidOperationException("CMVADR game input was not opened");

            _musicBuffer = _musicIoctl.Buffer;

            if (_shakerIoctl is not null)
                _shakerBuffer = _shakerIoctl.Buffer;
        }

        var outputFormat = WaveFormatFactory.Create7Point1Float(_config.SampleRate);

        // Build per-input pipelines
        // - Music can be multi-channel (we preserve up to 7.1 so games can keep surround)
        // - Shaker is treated as stereo and distributed by the router
        var music = BuildMusicProvider(_musicBuffer.ToSampleProvider(), _config.SampleRate);
        ISampleProvider shakerStereo;
        if (_shakerBuffer is not null)
        {
            shakerStereo = BuildStereoProvider(_shakerBuffer.ToSampleProvider(), _config.SampleRate);
        }
        else
        {
            // Secondary input disabled: feed silence, keep predictable routing.
            shakerStereo = new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(_config.SampleRate, 2)).ToSampleProvider();
        }

        var samplesPerNotification = Math.Max(256, _config.SampleRate / 20); // ~20Hz updates

        var meteredMusic = new MeteringSampleProvider(music, samplesPerNotification);
        meteredMusic.StreamVolume += (_, e) =>
        {
            if (e.MaxSampleValues.Length < 2) return;
            lock (_meterLock)
            {
                _musicPeakL = e.MaxSampleValues[0];
                _musicPeakR = e.MaxSampleValues[1];
            }
        };

        var meteredShaker = new MeteringSampleProvider(shakerStereo, samplesPerNotification);
        meteredShaker.StreamVolume += (_, e) =>
        {
            if (e.MaxSampleValues.Length < 2) return;
            lock (_meterLock)
            {
                _shakerPeakL = e.MaxSampleValues[0];
                _shakerPeakR = e.MaxSampleValues[1];
            }
        };

        var router = new RouterSampleProvider(
            meteredMusic,
            meteredShaker,
            outputFormat,
            _config);

        var meteredOutput = new MeteringSampleProvider(router, samplesPerNotification);
        meteredOutput.StreamVolume += (_, e) =>
        {
            var n = Math.Min(e.MaxSampleValues.Length, 8);
            lock (_meterLock)
            {
                for (var i = 0; i < n; i++)
                    _outputPeaks[i] = e.MaxSampleValues[i];
            }
        };

        var waveProvider = new FloatSampleToWaveProvider(meteredOutput);

        var shareMode = _config.UseExclusiveMode ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared;
        _output = new WasapiOut(_outputDevice, shareMode, true, _config.LatencyMs);
        _output.Init(waveProvider);
    }

    public void Start()
    {
        _stopped.Reset();

        _musicIoctl?.Start();
        _shakerIoctl?.Start();

        _musicCapture?.StartRecording();
        _shakerCapture?.StartRecording();
        _output.Play();
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) != 0)
            return;

        try { _output.Stop(); } catch { /* ignore */ }
        try { _musicCapture?.StopRecording(); } catch { /* ignore */ }
        try { _shakerCapture?.StopRecording(); } catch { /* ignore */ }

        try { _musicIoctl?.Stop(); } catch { /* ignore */ }
        try { _shakerIoctl?.Stop(); } catch { /* ignore */ }

        _stopped.Set();
    }

    public void WaitUntilStopped() => _stopped.Wait();

    public void Dispose()
    {
        Stop();
        _output.Dispose();
        _musicCapture?.Dispose();
        _shakerCapture?.Dispose();
        _musicIoctl?.Dispose();
        _shakerIoctl?.Dispose();
        _musicDevice?.Dispose();
        _shakerDevice?.Dispose();
        _outputDevice.Dispose();
        _stopped.Dispose();
    }

    private static ISampleProvider BuildStereoProvider(ISampleProvider source, int targetSampleRate)
    {
        ISampleProvider current = source;

        if (current.WaveFormat.Channels != 2)
        {
            current = new DownmixToStereoSampleProvider(current);
        }

        if (current.WaveFormat.SampleRate != targetSampleRate)
        {
            current = new WdlResamplingSampleProvider(current, targetSampleRate);
        }

        return current;
    }

    private static ISampleProvider BuildMusicProvider(ISampleProvider source, int targetSampleRate)
    {
        ISampleProvider current = source;

        // If the endpoint is > 7.1, fall back to a stereo downmix.
        if (current.WaveFormat.Channels > 8)
        {
            current = new DownmixToStereoSampleProvider(current);
        }

        if (current.WaveFormat.SampleRate != targetSampleRate)
        {
            current = new WdlResamplingSampleProvider(current, targetSampleRate);
        }

        return current;
    }

    public void CopyPeakValues(float[] musicStereo, float[] shakerStereo, float[] output7Point1)
    {
        if (musicStereo.Length < 2) throw new ArgumentException("musicStereo must have length >= 2", nameof(musicStereo));
        if (shakerStereo.Length < 2) throw new ArgumentException("shakerStereo must have length >= 2", nameof(shakerStereo));
        if (output7Point1.Length < 8) throw new ArgumentException("output7Point1 must have length >= 8", nameof(output7Point1));

        lock (_meterLock)
        {
            musicStereo[0] = _musicPeakL;
            musicStereo[1] = _musicPeakR;
            shakerStereo[0] = _shakerPeakL;
            shakerStereo[1] = _shakerPeakR;
            for (var i = 0; i < 8; i++)
                output7Point1[i] = _outputPeaks[i];
        }
    }
}
