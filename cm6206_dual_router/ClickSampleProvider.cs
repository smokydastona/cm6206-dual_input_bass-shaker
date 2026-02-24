using NAudio.Wave;

namespace Cm6206DualRouter;

/// <summary>
/// Generates a short burst on a single channel (used for latency measurement).
/// Output is 7.1 float (channels dictated by WaveFormat).
/// </summary>
public sealed class ClickSampleProvider : ISampleProvider
{
    private readonly int _sampleRate;
    private double _phase;
    private long _samplePos;

    public ClickSampleProvider(WaveFormat waveFormat)
    {
        WaveFormat = waveFormat;
        _sampleRate = waveFormat.SampleRate;

        ChannelIndex = 0;
        BurstFrequencyHz = 1000f;
        BurstMs = 10;
        LevelDb = -6f;
    }

    public WaveFormat WaveFormat { get; }

    public int ChannelIndex { get; set; }

    public float BurstFrequencyHz { get; set; }

    public int BurstMs { get; set; }

    public float LevelDb { get; set; }

    public int Read(float[] buffer, int offset, int count)
    {
        var channels = WaveFormat.Channels;
        var framesRequested = count / channels;
        if (framesRequested <= 0) return 0;

        var channel = Math.Clamp(ChannelIndex, 0, channels - 1);
        var level = (float)Math.Pow(10.0, LevelDb / 20.0);
        var burstSamples = (long)(_sampleRate * (BurstMs / 1000.0));

        for (var frame = 0; frame < framesRequested; frame++)
        {
            float sample;
            if (_samplePos >= 0 && _samplePos < burstSamples)
            {
                sample = (float)Math.Sin(_phase) * level;
                _phase += 2.0 * Math.PI * (BurstFrequencyHz / _sampleRate);
                if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;
            }
            else
            {
                sample = 0f;
            }

            _samplePos++;

            var baseIndex = offset + (frame * channels);
            for (var ch = 0; ch < channels; ch++)
                buffer[baseIndex + ch] = (ch == channel) ? sample : 0f;
        }

        return framesRequested * channels;
    }
}
