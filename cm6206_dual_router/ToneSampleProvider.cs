using NAudio.Wave;

namespace Cm6206DualRouter;

public enum ToneType
{
    Sine,
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
        LevelDb = -12f;

        for (var i = 0; i < _pinkRows.Length; i++)
            _pinkRows[i] = NextWhite();
    }

    public WaveFormat WaveFormat { get; }

    public int ChannelIndex { get; set; }

    public ToneType Type { get; set; }

    public float FrequencyHz { get; set; }

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
            var sample = Type switch
            {
                ToneType.Sine => (float)Math.Sin(_phase),
                ToneType.WhiteNoise => NextWhite(),
                ToneType.PinkNoise => NextPink(),
                _ => 0f
            };

            sample *= level;

            // advance phase for sine
            if (Type == ToneType.Sine)
            {
                _phase += 2.0 * Math.PI * (FrequencyHz / _sampleRate);
                if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;
            }

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
