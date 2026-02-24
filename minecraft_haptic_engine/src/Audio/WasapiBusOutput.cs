using MinecraftHapticEngine.Config;
using MinecraftHapticEngine.Synthesis;
using MinecraftHapticEngine.Utils;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MinecraftHapticEngine.Audio;

public sealed class WasapiBusOutput : IDisposable
{
    private readonly string _busName;
    private readonly BusConfig _config;
    private readonly EffectMixer _mixer;

    private readonly MMDevice _device;
    private readonly WasapiOut _out;

    public WasapiBusOutput(string busName, BusConfig config, EffectMixer mixer)
    {
        _busName = busName;
        _config = config;
        _mixer = mixer;

        _device = DeviceLister.FindRenderDeviceByName(_config.RenderDeviceName);
        _out = new WasapiOut(_device, config.ExclusiveMode ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared, false, config.DesiredLatencyMs);
    }

    public void Start()
    {
        var provider = new BusSampleProvider(_mixer, _config.SampleRate, _config.Channels, _config.BufferSizeFrames);
        var waveProvider = new SampleToWaveProvider(provider);

        _out.Init(waveProvider);
        _out.Play();

        Console.WriteLine($"[{_busName}] Output -> '{_device.FriendlyName}' ({_config.SampleRate} Hz, {_config.Channels} ch)");
    }

    public void Stop()
    {
        try { _out.Stop(); } catch { }
    }

    public void Dispose()
    {
        try { _out.Dispose(); } catch { }
        try { _device.Dispose(); } catch { }
    }
}
