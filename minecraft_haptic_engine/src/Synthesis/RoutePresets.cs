using MinecraftHapticEngine.Config;

namespace MinecraftHapticEngine.Synthesis;

public static class RoutePresets
{
    public static float[] Resolve(RouteConfig? route, int channels)
    {
        if (route?.Weights is not null && route.Weights.Length == channels)
        {
            return (float[])route.Weights.Clone();
        }

        var preset = route?.Preset?.ToLowerInvariant() ?? "all";
        return preset switch
        {
            "all" => Enumerable.Repeat(1f, channels).ToArray(),
            "left" when channels >= 2 => Build(channels, on: new[] { 0 }),
            "right" when channels >= 2 => Build(channels, on: new[] { 1 }),
            "front" when channels == 8 => Build(channels, on: new[] { 0, 1, 2 }),
            "rear" when channels == 8 => Build(channels, on: new[] { 4, 5 }),
            "side" when channels == 8 => Build(channels, on: new[] { 6, 7 }),
            "lfe" when channels == 8 => Build(channels, on: new[] { 3 }),
            _ => Enumerable.Repeat(1f, channels).ToArray(),
        };
    }

    private static float[] Build(int channels, int[] on)
    {
        var w = new float[channels];
        foreach (var i in on)
        {
            if (i >= 0 && i < channels) w[i] = 1f;
        }
        return w;
    }
}
