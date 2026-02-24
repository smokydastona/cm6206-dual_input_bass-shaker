namespace MinecraftHapticEngine.Synthesis.Generators;

public sealed class NoiseGenerator : IGenerator
{
    private readonly Random _rng = new();
    private float _prev;
    private readonly float _color;

    public NoiseGenerator(float color)
    {
        // color: 0 = white, 1 = very "pinkish" (simple 1-pole smoothing)
        _color = Math.Clamp(color, 0, 1);
    }

    public float NextSample()
    {
        var white = (float)(_rng.NextDouble() * 2.0 - 1.0);
        _prev = _prev + (white - _prev) * (1 - _color);
        return _prev;
    }

    public void Reset() => _prev = 0;
}
