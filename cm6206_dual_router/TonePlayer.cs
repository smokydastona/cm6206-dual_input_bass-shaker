using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Cm6206DualRouter;

public sealed class TonePlayer : IDisposable
{
    private readonly MMDevice _outputDevice;
    private readonly WasapiOut _output;
    private readonly ToneSampleProvider _tone;

    public TonePlayer(RouterConfig config)
    {
        _outputDevice = DeviceHelper.GetRenderDeviceByFriendlyName(config.OutputRenderDevice);

        var negotiation = OutputFormatNegotiator.Negotiate(config, _outputDevice);
        var effective = negotiation.EffectiveConfig;

        var format = WaveFormatFactory.Create7Point1Float(effective.SampleRate);
        _tone = new ToneSampleProvider(format);

        var waveProvider = new SampleToWaveProvider(_tone);

        var shareMode = effective.UseExclusiveMode ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared;
        _output = new WasapiOut(_outputDevice, shareMode, true, effective.LatencyMs);
        _output.Init(waveProvider);
    }

    public void Start() => _output.Play();

    public void Stop() => _output.Stop();

    public void SetChannel(int channelIndex) => _tone.ChannelIndex = channelIndex;

    public void SetType(ToneType type) => _tone.Type = type;

    public void SetFrequency(float hz) => _tone.FrequencyHz = hz;

    public void SetSweepEndFrequency(float hz) => _tone.SweepEndHz = hz;

    public void SetSweepSeconds(float seconds) => _tone.SweepSeconds = seconds;

    public void SetSweepLoop(bool loop) => _tone.SweepLoop = loop;

    public void SetLevelDb(float db) => _tone.LevelDb = db;

    public void Dispose()
    {
        try { _output.Stop(); } catch { /* ignore */ }
        _output.Dispose();
        _outputDevice.Dispose();
    }
}
