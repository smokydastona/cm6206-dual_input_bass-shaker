using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Cm6206DualRouter.Telemetry;

namespace Cm6206DualRouter;

public sealed class WasapiDualRouter : IDisposable
{
    private RouterConfig _config;

    private readonly DateTime _startUtc = DateTime.UtcNow;

    public int EffectiveSampleRate => _config.SampleRate;
    public string? FormatWarning { get; }

    public string RequestedInputIngestMode { get; }
    public string EffectiveInputIngestMode { get; }
    public string? InputWarning { get; }

    private readonly IAudioInputBackend _inputBackend;

    private readonly MMDevice _outputDevice;

    private readonly BufferedWaveProvider _musicBuffer;
    private readonly BufferedWaveProvider? _shakerBuffer;

    private readonly BstTelemetryHapticsSampleProvider? _telemetryShaker;
    private readonly AutoFallbackShakerSampleProvider? _autoFallbackShaker;
    private readonly TimeNudgeSampleProvider? _shakerNudger;

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

        var useTelemetryHaptics = _config.TelemetryHapticsEnabled;
        var useShakerInputDevice = !string.IsNullOrWhiteSpace(config.ShakerInputRenderDevice);

        RequestedInputIngestMode = (_config.InputIngestMode ?? "WasapiLoopback").Trim();
        var ingest = RequestedInputIngestMode;
        string? warning = null;

        Func<bool> shouldStop = () => Volatile.Read(ref _stopRequested) != 0;
        Action requestStop = Stop;

        if (ingest == "CmvadrIoctl")
        {
            try
            {
                _inputBackend = new CmvadrIoctlBackend(useShakerInputDevice);
            }
            catch (Exception ex)
            {
                // Auto-fallback to loopback if CMVADR is unavailable.
                warning = $"CMVADR input failed; falling back to WASAPI loopback. ({ex.Message})";
                ingest = "WasapiLoopback";
                _inputBackend = new WasapiLoopbackBackend(
                    musicInputRenderDevice: config.MusicInputRenderDevice,
                    shakerInputRenderDevice: useShakerInputDevice ? config.ShakerInputRenderDevice : null,
                    startUtc: _startUtc,
                    shouldStop: shouldStop,
                    requestStop: requestStop);
            }
        }
        else
        {
            _inputBackend = new WasapiLoopbackBackend(
                musicInputRenderDevice: config.MusicInputRenderDevice,
                shakerInputRenderDevice: useShakerInputDevice ? config.ShakerInputRenderDevice : null,
                startUtc: _startUtc,
                shouldStop: shouldStop,
                requestStop: requestStop);
        }

        EffectiveInputIngestMode = ingest;
        InputWarning = warning;

        _musicBuffer = _inputBackend.MusicBuffer;
        _shakerBuffer = _inputBackend.ShakerBuffer;

        _outputDevice = DeviceHelper.GetRenderDeviceByFriendlyName(config.OutputRenderDevice);

        // Negotiate effective format (shared uses mix format; exclusive can fall back).
        var negotiation = OutputFormatNegotiator.Negotiate(_config, _outputDevice);
        _config = negotiation.EffectiveConfig;
        FormatWarning = negotiation.Warning;

        var outputFormat = WaveFormatFactory.Create7Point1Float(_config.SampleRate);

        // Build per-input pipelines
        // - Music can be multi-channel (we preserve up to 7.1 so games can keep surround)
        // - Shaker is treated as stereo and distributed by the router
        var music = BuildMusicProvider(_musicBuffer.ToSampleProvider(), _config.SampleRate);
        ISampleProvider shakerStereo;

        Func<double?> shakerUpstreamBufferMs = () =>
        {
            if (_shakerBuffer is null)
                return null;

            var fmt = _shakerBuffer.WaveFormat;
            var bytesPerFrame = (fmt.Channels * fmt.BitsPerSample) / 8;
            if (bytesPerFrame <= 0 || fmt.SampleRate <= 0)
                return null;

            var bytesPerSec = (double)(fmt.SampleRate * bytesPerFrame);
            if (bytesPerSec <= 1)
                return null;

            return 1000.0 * _shakerBuffer.BufferedBytes / bytesPerSec;
        };

        if (useTelemetryHaptics)
        {
            _telemetryShaker = new BstTelemetryHapticsSampleProvider(_config);

            if (_shakerBuffer is not null)
            {
                var fallback = BuildStereoProvider(_shakerBuffer.ToSampleProvider(), _config.SampleRate);
                if (_config.ShakerNudgesEnabled)
                {
                    _shakerNudger = new TimeNudgeSampleProvider(
                        fallback,
                        shakerUpstreamBufferMs,
                        targetBufferMs: _config.ShakerNudgeTargetBufferMs,
                        deadbandMs: _config.ShakerNudgeDeadbandMs);
                    fallback = _shakerNudger;
                }

                _autoFallbackShaker = new AutoFallbackShakerSampleProvider(
                    primary: _telemetryShaker,
                    fallback: fallback,
                    primaryHealthy: () => _telemetryShaker.IsReceivingRecently);
                shakerStereo = _autoFallbackShaker;
            }
            else
            {
                // No fallback device provided; if telemetry drops, the output goes quiet.
                shakerStereo = _telemetryShaker;
            }
        }
        else if (_shakerBuffer is not null)
        {
            var shaker = BuildStereoProvider(_shakerBuffer.ToSampleProvider(), _config.SampleRate);
            if (_config.ShakerNudgesEnabled)
            {
                _shakerNudger = new TimeNudgeSampleProvider(
                    shaker,
                    shakerUpstreamBufferMs,
                    targetBufferMs: _config.ShakerNudgeTargetBufferMs,
                    deadbandMs: _config.ShakerNudgeDeadbandMs);
                shakerStereo = _shakerNudger;
            }
            else
            {
                shakerStereo = shaker;
            }
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

    public readonly record struct EndpointStatus(
        string Name,
        string Backend,
        DateTime StartUtc,
        int SampleRate,
        int Channels,
        int BitsPerSample,
        bool Connected,
        DateTime? LastDataUtc,
        int BufferedBytes,
        int BufferLengthBytes,
        long TotalBytes,
        long TotalFrames,
        long TotalErrors,
        int ConsecutiveErrors,
        long TotalNudgeDropFrames,
        long TotalNudgeInsertFrames);

    public (EndpointStatus Game, EndpointStatus? Shaker) GetInputStatus()
    {
        var (game, shaker) = _inputBackend.GetInputStatus();

        if (shaker is not null && _shakerNudger is not null)
        {
            shaker = shaker.Value with
            {
                TotalNudgeDropFrames = _shakerNudger.TotalDropFrames,
                TotalNudgeInsertFrames = _shakerNudger.TotalInsertFrames
            };
        }

        return (game, shaker);
    }

    public void Start()
    {
        _stopped.Reset();

        _inputBackend.Start();
        _output.Play();
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) != 0)
            return;

        try { _output.Stop(); } catch { /* ignore */ }
        try { _inputBackend.Stop(); } catch { /* ignore */ }

        _stopped.Set();
    }

    public void WaitUntilStopped() => _stopped.Wait();

    public void Dispose()
    {
        Stop();
        _output.Dispose();
        _telemetryShaker?.Dispose();
        _inputBackend.Dispose();
        _outputDevice.Dispose();
        _stopped.Dispose();
    }

    private interface IAudioInputBackend : IDisposable
    {
        BufferedWaveProvider MusicBuffer { get; }
        BufferedWaveProvider? ShakerBuffer { get; }

        void Start();
        void Stop();
        (EndpointStatus Game, EndpointStatus? Shaker) GetInputStatus();
    }

    private sealed class WasapiLoopbackBackend : IAudioInputBackend
    {
        private readonly DateTime _startUtc;
        private readonly Func<bool> _shouldStop;
        private readonly Action _requestStop;

        private readonly MMDevice _musicDevice;
        private readonly MMDevice? _shakerDevice;

        private readonly WasapiLoopbackCapture _musicCapture;
        private readonly WasapiLoopbackCapture? _shakerCapture;

        private DateTime? _lastMusicCaptureUtc;
        private DateTime? _lastShakerCaptureUtc;
        private long _musicCaptureBytes;
        private long _shakerCaptureBytes;

        public BufferedWaveProvider MusicBuffer { get; }
        public BufferedWaveProvider? ShakerBuffer { get; }

        public WasapiLoopbackBackend(
            string musicInputRenderDevice,
            string? shakerInputRenderDevice,
            DateTime startUtc,
            Func<bool> shouldStop,
            Action requestStop)
        {
            _startUtc = startUtc;
            _shouldStop = shouldStop;
            _requestStop = requestStop;

            _musicDevice = DeviceHelper.GetRenderDeviceByFriendlyName(musicInputRenderDevice);
            if (!string.IsNullOrWhiteSpace(shakerInputRenderDevice))
                _shakerDevice = DeviceHelper.GetRenderDeviceByFriendlyName(shakerInputRenderDevice);

            _musicCapture = new WasapiLoopbackCapture(_musicDevice);
            if (_shakerDevice is not null)
                _shakerCapture = new WasapiLoopbackCapture(_shakerDevice);

            MusicBuffer = new BufferedWaveProvider(_musicCapture.WaveFormat)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(2)
            };

            if (_shakerCapture is not null)
            {
                ShakerBuffer = new BufferedWaveProvider(_shakerCapture.WaveFormat)
                {
                    DiscardOnBufferOverflow = true,
                    BufferDuration = TimeSpan.FromSeconds(2)
                };
            }

            _musicCapture.DataAvailable += (_, e) =>
            {
                if (_shouldStop()) return;
                _lastMusicCaptureUtc = DateTime.UtcNow;
                _musicCaptureBytes += e.BytesRecorded;
                MusicBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
            };

            if (_shakerCapture is not null && ShakerBuffer is not null)
            {
                _shakerCapture.DataAvailable += (_, e) =>
                {
                    if (_shouldStop()) return;
                    _lastShakerCaptureUtc = DateTime.UtcNow;
                    _shakerCaptureBytes += e.BytesRecorded;
                    ShakerBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                };
            }

            _musicCapture.RecordingStopped += (_, __) => _requestStop();
            if (_shakerCapture is not null)
                _shakerCapture.RecordingStopped += (_, __) => _requestStop();
        }

        public void Start()
        {
            _musicCapture.StartRecording();
            _shakerCapture?.StartRecording();
        }

        public void Stop()
        {
            try { _musicCapture.StopRecording(); } catch { /* ignore */ }
            try { _shakerCapture?.StopRecording(); } catch { /* ignore */ }
        }

        public (EndpointStatus Game, EndpointStatus? Shaker) GetInputStatus()
        {
            // WASAPI loopback: best-effort (no IOCTL stats).
            var fmt = MusicBuffer.WaveFormat;
            var gameEp = new EndpointStatus(
                Name: "Game",
                Backend: "Loopback",
                StartUtc: _startUtc,
                SampleRate: fmt.SampleRate,
                Channels: fmt.Channels,
                BitsPerSample: fmt.BitsPerSample,
                Connected: true,
                LastDataUtc: _lastMusicCaptureUtc,
                BufferedBytes: MusicBuffer.BufferedBytes,
                BufferLengthBytes: MusicBuffer.BufferLength,
                TotalBytes: _musicCaptureBytes,
                TotalFrames: 0,
                TotalErrors: 0,
                ConsecutiveErrors: 0,
                TotalNudgeDropFrames: 0,
                TotalNudgeInsertFrames: 0);

            EndpointStatus? shakerEp = null;
            if (ShakerBuffer is not null)
            {
                var sfmt = ShakerBuffer.WaveFormat;
                shakerEp = new EndpointStatus(
                    Name: "Shaker",
                    Backend: "Loopback",
                    StartUtc: _startUtc,
                    SampleRate: sfmt.SampleRate,
                    Channels: sfmt.Channels,
                    BitsPerSample: sfmt.BitsPerSample,
                    Connected: true,
                    LastDataUtc: _lastShakerCaptureUtc,
                    BufferedBytes: ShakerBuffer.BufferedBytes,
                    BufferLengthBytes: ShakerBuffer.BufferLength,
                    TotalBytes: _shakerCaptureBytes,
                    TotalFrames: 0,
                    TotalErrors: 0,
                    ConsecutiveErrors: 0,
                    TotalNudgeDropFrames: 0,
                    TotalNudgeInsertFrames: 0);
            }

            return (gameEp, shakerEp);
        }

        public void Dispose()
        {
            _musicCapture.Dispose();
            _shakerCapture?.Dispose();
            _musicDevice.Dispose();
            _shakerDevice?.Dispose();
        }
    }

    private sealed class CmvadrIoctlBackend : IAudioInputBackend
    {
        private readonly CmvadrIoctlInput _musicIoctl;
        private readonly CmvadrIoctlInput? _shakerIoctl;

        public BufferedWaveProvider MusicBuffer => _musicIoctl.Buffer;
        public BufferedWaveProvider? ShakerBuffer => _shakerIoctl?.Buffer;

        public CmvadrIoctlBackend(bool useShaker)
        {
            _musicIoctl = CmvadrIoctlInput.Open(VirtualAudioDriverIoctl.GameDeviceWin32Path);
            if (useShaker)
                _shakerIoctl = CmvadrIoctlInput.Open(VirtualAudioDriverIoctl.ShakerDeviceWin32Path);
        }

        public void Start()
        {
            _musicIoctl.Start();
            _shakerIoctl?.Start();
        }

        public void Stop()
        {
            _musicIoctl.Stop();
            _shakerIoctl?.Stop();
        }

        public (EndpointStatus Game, EndpointStatus? Shaker) GetInputStatus()
        {
            var g = _musicIoctl.GetStatusSnapshot();
            EndpointStatus? s = null;
            if (_shakerIoctl is not null)
            {
                var sh = _shakerIoctl.GetStatusSnapshot();
                s = new EndpointStatus(
                    Name: "Shaker",
                    Backend: "CMVADR",
                    StartUtc: sh.StartUtc,
                    SampleRate: (int)sh.Format.SampleRate,
                    Channels: (int)sh.Format.Channels,
                    BitsPerSample: (int)sh.Format.BitsPerSample,
                    Connected: sh.ConsecutiveIoctlFailures == 0,
                    LastDataUtc: sh.LastSuccessfulReadUtc,
                    BufferedBytes: sh.BufferedBytes,
                    BufferLengthBytes: sh.BufferLengthBytes,
                    TotalBytes: sh.TotalBytesRead,
                    TotalFrames: sh.TotalFramesRead,
                    TotalErrors: sh.TotalIoctlFailures,
                    ConsecutiveErrors: sh.ConsecutiveIoctlFailures,
                    TotalNudgeDropFrames: 0,
                    TotalNudgeInsertFrames: 0);
            }

            var game = new EndpointStatus(
                Name: "Game",
                Backend: "CMVADR",
                StartUtc: g.StartUtc,
                SampleRate: (int)g.Format.SampleRate,
                Channels: (int)g.Format.Channels,
                BitsPerSample: (int)g.Format.BitsPerSample,
                Connected: g.ConsecutiveIoctlFailures == 0,
                LastDataUtc: g.LastSuccessfulReadUtc,
                BufferedBytes: g.BufferedBytes,
                BufferLengthBytes: g.BufferLengthBytes,
                TotalBytes: g.TotalBytesRead,
                TotalFrames: g.TotalFramesRead,
                TotalErrors: g.TotalIoctlFailures,
                ConsecutiveErrors: g.ConsecutiveIoctlFailures,
                TotalNudgeDropFrames: 0,
                TotalNudgeInsertFrames: 0);

            return (game, s);
        }

        public void Dispose()
        {
            _musicIoctl.Dispose();
            _shakerIoctl?.Dispose();
        }
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
