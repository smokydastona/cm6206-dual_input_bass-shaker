using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Cm6206DualRouter;

public sealed class WasapiDualRouter : IDisposable
{
    private readonly RouterConfig _config;

    private readonly MMDevice _musicDevice;
    private readonly MMDevice _shakerDevice;
    private readonly MMDevice _outputDevice;

    private readonly WasapiLoopbackCapture _musicCapture;
    private readonly WasapiLoopbackCapture _shakerCapture;

    private readonly BufferedWaveProvider _musicBuffer;
    private readonly BufferedWaveProvider _shakerBuffer;

    private readonly WasapiOut _output;
    private readonly ManualResetEventSlim _stopped = new(false);

    private int _stopRequested;

    public WasapiDualRouter(RouterConfig config)
    {
        _config = config;

        _musicDevice = DeviceHelper.GetRenderDeviceByFriendlyName(config.MusicInputRenderDevice);
        _shakerDevice = DeviceHelper.GetRenderDeviceByFriendlyName(config.ShakerInputRenderDevice);
        _outputDevice = DeviceHelper.GetRenderDeviceByFriendlyName(config.OutputRenderDevice);

        _musicCapture = new WasapiLoopbackCapture(_musicDevice);
        _shakerCapture = new WasapiLoopbackCapture(_shakerDevice);

        _musicBuffer = new BufferedWaveProvider(_musicCapture.WaveFormat)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromSeconds(2)
        };

        _shakerBuffer = new BufferedWaveProvider(_shakerCapture.WaveFormat)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromSeconds(2)
        };

        _musicCapture.DataAvailable += (_, e) =>
        {
            if (Volatile.Read(ref _stopRequested) != 0) return;
            _musicBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
        };

        _shakerCapture.DataAvailable += (_, e) =>
        {
            if (Volatile.Read(ref _stopRequested) != 0) return;
            _shakerBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
        };

        _musicCapture.RecordingStopped += (_, __) => Stop();
        _shakerCapture.RecordingStopped += (_, __) => Stop();

        var outputFormat = WaveFormatFactory.Create7Point1Float(config.SampleRate);

        // Build per-input pipelines
        var musicStereo = BuildStereoProvider(_musicBuffer.ToSampleProvider(), config.SampleRate);
        var shakerStereo = BuildStereoProvider(_shakerBuffer.ToSampleProvider(), config.SampleRate);

        var router = new RouterSampleProvider(
            musicStereo,
            shakerStereo,
            outputFormat,
            config);

        var waveProvider = new SampleToWaveProvider(router);

        var shareMode = config.UseExclusiveMode ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared;
        _output = new WasapiOut(_outputDevice, shareMode, true, config.LatencyMs);
        _output.Init(waveProvider);
    }

    public void Start()
    {
        _stopped.Reset();
        _musicCapture.StartRecording();
        _shakerCapture.StartRecording();
        _output.Play();
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) != 0)
            return;

        try { _output.Stop(); } catch { /* ignore */ }
        try { _musicCapture.StopRecording(); } catch { /* ignore */ }
        try { _shakerCapture.StopRecording(); } catch { /* ignore */ }

        _stopped.Set();
    }

    public void WaitUntilStopped() => _stopped.Wait();

    public void Dispose()
    {
        Stop();
        _output.Dispose();
        _musicCapture.Dispose();
        _shakerCapture.Dispose();
        _musicDevice.Dispose();
        _shakerDevice.Dispose();
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
}
