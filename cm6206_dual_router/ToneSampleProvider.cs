using NAudio.Wave;

namespace Cm6206DualRouter;

public enum ToneType
{
    Sine,
    Sweep,
    PinkNoise,
    WhiteNoise
}

/// <summary>
/// Generates a test signal on a single selected 7.1 channel.
/// Channel order: FL, FR, FC, LFE, BL, BR, SL, SR.
/// </summary>
public sealed class ToneSampleProvider : ISampleProvider
{
    private readonly int _sampleRate;
    private double _phase;
    private long _sweepSamplePos;

    // Pink noise (Voss-McCartney) state
    private readonly Random _rng = new(12345);
    private readonly float[] _pinkRows = new float[16];
    private int _pinkCounter;

    public ToneSampleProvider(WaveFormat waveFormat)
    {
        WaveFormat = waveFormat;
        _sampleRate = waveFormat.SampleRate;

        ChannelIndex = 0;
        Type = ToneType.Sine;
        FrequencyHz = 440f;
        SweepEndHz = 2000f;
        SweepSeconds = 5.0f;
        SweepLoop = true;
        LevelDb = -12f;

        for (var i = 0; i < _pinkRows.Length; i++)
            _pinkRows[i] = NextWhite();
    }

    public WaveFormat WaveFormat { get; }

    public int ChannelIndex { get; set; }

    public ToneType Type { get; set; }

    public float FrequencyHz { get; set; }

    // Sweep uses FrequencyHz as start frequency.
    public float SweepEndHz { get; set; }

    public float SweepSeconds { get; set; }

    public bool SweepLoop { get; set; }

    public float LevelDb { get; set; }

    public int Read(float[] buffer, int offset, int count)
    {
        var channels = WaveFormat.Channels;
        var framesRequested = count / channels;
        if (framesRequested <= 0)
            return 0;

        var level = DbToGain(LevelDb);
        var channel = Math.Clamp(ChannelIndex, 0, channels - 1);

        for (var frame = 0; frame < framesRequested; frame++)
        {
            float sample;

            switch (Type)
            {
                case ToneType.Sine:
                    sample = (float)Math.Sin(_phase);
                    _phase += 2.0 * Math.PI * (FrequencyHz / _sampleRate);
                    if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;
                    break;

                case ToneType.Sweep:
                {
                    sample = (float)Math.Sin(_phase);

                    var startHz = Math.Clamp(FrequencyHz, 1f, 20000f);
                    var endHz = Math.Clamp(SweepEndHz, 1f, 20000f);
                    if (endHz < startHz)
                        (startHz, endHz) = (endHz, startHz);

                    var seconds = Math.Max(0.05f, SweepSeconds);
                    var t = (double)_sweepSamplePos / _sampleRate;
                    var progress = t / seconds;

                    if (progress >= 1.0)
                    {
                        if (SweepLoop)
                        {
                            _sweepSamplePos = 0;
                            progress = 0.0;
                        }
                        else
                        {
                            progress = 1.0;
                        }
                    }

                    // Log sweep (sounds smoother across octaves).
                    var ratio = endHz / startHz;
                    var hz = startHz * Math.Pow(ratio, progress);

                    _phase += 2.0 * Math.PI * (hz / _sampleRate);
                    if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;

                    _sweepSamplePos++;
                    break;
                }

                case ToneType.WhiteNoise:
                    sample = NextWhite();
                    break;

                case ToneType.PinkNoise:
                    sample = NextPink();
                    break;

                default:
                    sample = 0f;
                    break;
            }

            sample *= level;

            var baseIndex = offset + (frame * channels);
            for (var ch = 0; ch < channels; ch++)
                buffer[baseIndex + ch] = (ch == channel) ? sample : 0f;
        }

        return framesRequested * channels;
    }

    private float NextWhite() => (float)(_rng.NextDouble() * 2.0 - 1.0);

    private float NextPink()
    {
        // Simple Voss-McCartney
        _pinkCounter++;
        var index = _pinkCounter;
        var i = 0;
        while ((index & 1) == 0)
        {
            index >>= 1;
            i++;
            if (i >= _pinkRows.Length) break;
        }
        if (i < _pinkRows.Length)
            _pinkRows[i] = NextWhite();

        var sum = 0f;
        for (var r = 0; r < _pinkRows.Length; r++)
            sum += _pinkRows[r];

        // normalize to roughly [-1,1]
        return sum / _pinkRows.Length;
    }

    private static float DbToGain(float db) => (float)Math.Pow(10.0, db / 20.0);
}
