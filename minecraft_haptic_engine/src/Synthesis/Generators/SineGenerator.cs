namespace MinecraftHapticEngine.Synthesis.Generators;

public sealed class SineGenerator : IGenerator
{
    private readonly int _sampleRate;
    private Func<float> _freqHz;
    private double _phase;

    public SineGenerator(int sampleRate, Func<float> freqHz)
    {
        _sampleRate = sampleRate;
        _freqHz = freqHz;
    }

    public float NextSample()
    {
        var f = Math.Max(0, _freqHz());
        var inc = 2.0 * Math.PI * f / _sampleRate;
        _phase += inc;
        if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;
        return (float)Math.Sin(_phase);
    }

    public void Reset() => _phase = 0;
}
