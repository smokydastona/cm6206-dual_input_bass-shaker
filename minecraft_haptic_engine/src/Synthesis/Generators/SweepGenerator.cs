namespace MinecraftHapticEngine.Synthesis.Generators;

public sealed class SweepGenerator : IGenerator
{
    private readonly int _sampleRate;
    private readonly float _durationSeconds;
    private readonly float _f0;
    private readonly float _f1;

    private int _pos;
    private double _phase;

    public SweepGenerator(int sampleRate, float f0, float f1, float durationMs)
    {
        _sampleRate = sampleRate;
        _f0 = Math.Max(0, f0);
        _f1 = Math.Max(0, f1);
        _durationSeconds = Math.Max(0.001f, durationMs / 1000f);
    }

    public float NextSample()
    {
        var t = Math.Min(1.0, _pos / (_durationSeconds * _sampleRate));
        var f = _f0 + (_f1 - _f0) * (float)t;
        var inc = 2.0 * Math.PI * f / _sampleRate;
        _phase += inc;
        if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;
        _pos++;
        return (float)Math.Sin(_phase);
    }

    public void Reset()
    {
        _pos = 0;
        _phase = 0;
    }
}
