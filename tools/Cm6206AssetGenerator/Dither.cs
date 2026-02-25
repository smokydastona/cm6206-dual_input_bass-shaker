namespace Cm6206AssetGenerator;

internal static class Dither
{
    // 2x2 Bayer thresholds (0..3)
    // 0 2
    // 3 1
    private static readonly int[,] Bayer2 =
    {
        { 0, 2 },
        { 3, 1 }
    };

    // Deterministic tileable 4x4 25% mask
    // 1 0 0 0
    // 0 0 1 0
    // 0 1 0 0
    // 0 0 0 1
    private static readonly bool[,] Noise25_4x4 =
    {
        { true,  false, false, false },
        { false, false, true,  false },
        { false, true,  false, false },
        { false, false, false, true  }
    };

    public static bool Checker50(int x, int y) => ((x + y) & 1) == 0;

    public static bool Noise25(int x, int y) => Noise25_4x4[Mod(y, 4), Mod(x, 4)];

    // Returns true if the pixel should be the "lighter" tone for gradient value g in [0..1].
    public static bool Bayer2x2(float g, int x, int y)
    {
        g = Math.Clamp(g, 0f, 1f);
        var t = Bayer2[Mod(y, 2), Mod(x, 2)];
        var threshold = (t + 0.5f) / 4f;
        return g >= threshold;
    }

    private static int Mod(int a, int n)
    {
        var m = a % n;
        return m < 0 ? m + n : m;
    }
}
