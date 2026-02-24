namespace MinecraftHapticEngine.Synthesis.Generators;

public sealed class ImpulseGenerator : IGenerator
{
    private int _pos;

    public float NextSample()
    {
        var v = _pos == 0 ? 1f : 0f;
        _pos++;
        return v;
    }

    public void Reset() => _pos = 0;
}
