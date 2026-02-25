using SixLabors.ImageSharp.PixelFormats;

namespace Cm6206AssetGenerator;

internal static class ColorUtil
{
    public static Rgba32 ParseHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) throw new ArgumentException("hex is required", nameof(hex));

        var s = hex.Trim();
        if (s.StartsWith('#')) s = s[1..];
        if (s.Length != 6) throw new ArgumentException("hex must be RRGGBB", nameof(hex));

        var r = Convert.ToByte(s.Substring(0, 2), 16);
        var g = Convert.ToByte(s.Substring(2, 2), 16);
        var b = Convert.ToByte(s.Substring(4, 2), 16);
        return new Rgba32(r, g, b, 255);
    }

    public static string ToHexRgb(Rgba32 c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static Rgba32 WithAlpha(Rgba32 c, byte a) => new(c.R, c.G, c.B, a);
}
