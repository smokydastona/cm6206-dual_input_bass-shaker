namespace MinecraftHapticEngine.Synthesis.Generators;

public interface IGenerator
{
    float NextSample();
    void Reset();
}
