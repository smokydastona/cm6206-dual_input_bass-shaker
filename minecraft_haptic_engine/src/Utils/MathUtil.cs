namespace MinecraftHapticEngine.Utils;

public static class MathUtil
{
    public static float DbToLinear(float db) => (float)Math.Pow(10, db / 20.0);

    public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

    public static float MapTelemetry(float value, float min, float max, string curve, float scale, float offset)
    {
        var t = (value - min) / (max - min);
        t = Clamp01(t);

        t = curve.ToLowerInvariant() switch
        {
            "linear" => t,
            "square" => t * t,
            "sqrt" => (float)Math.Sqrt(t),
            _ => t,
        };

        return t * scale + offset;
    }
}
