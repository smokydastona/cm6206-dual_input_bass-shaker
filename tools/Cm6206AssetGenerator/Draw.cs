using SixLabors.ImageSharp.PixelFormats;

namespace Cm6206AssetGenerator;

internal static class Draw
{
    public static void CircleFill(PixelCanvas c, int cx, int cy, int radius, Rgba32 color)
    {
        var r2 = radius * radius;
        for (var y = cy - radius; y <= cy + radius; y++)
        for (var x = cx - radius; x <= cx + radius; x++)
        {
            var dx = x - cx;
            var dy = y - cy;
            if (dx * dx + dy * dy <= r2)
                c.SetPixel(x, y, color);
        }
    }

    public static void CircleRing(PixelCanvas c, int cx, int cy, int outerRadius, int thickness, Rgba32 color)
    {
        var innerRadius = Math.Max(0, outerRadius - thickness);
        var outer2 = outerRadius * outerRadius;
        var inner2 = innerRadius * innerRadius;

        for (var y = cy - outerRadius; y <= cy + outerRadius; y++)
        for (var x = cx - outerRadius; x <= cx + outerRadius; x++)
        {
            var dx = x - cx;
            var dy = y - cy;
            var d2 = dx * dx + dy * dy;
            if (d2 <= outer2 && d2 >= inner2)
                c.SetPixel(x, y, color);
        }
    }

    // Angle: degrees, where 0° points right, 90° down (screen coords).
    private static float AngleDeg(int dx, int dy)
    {
        var a = (float)(Math.Atan2(dy, dx) * (180.0 / Math.PI));
        if (a < 0) a += 360f;
        return a;
    }

    public static void ArcRing(PixelCanvas c, int cx, int cy, int radius, int thickness, float startDeg, float endDeg, Rgba32 color)
    {
        // Supports wrap across 0° (e.g., 135° → 45°).
        var inner = Math.Max(0, radius - thickness);
        var outer2 = radius * radius;
        var inner2 = inner * inner;

        bool InArc(float ang)
        {
            if (startDeg <= endDeg)
                return ang >= startDeg && ang <= endDeg;
            // wrap
            return ang >= startDeg || ang <= endDeg;
        }

        for (var y = cy - radius; y <= cy + radius; y++)
        for (var x = cx - radius; x <= cx + radius; x++)
        {
            var dx = x - cx;
            var dy = y - cy;
            var d2 = dx * dx + dy * dy;
            if (d2 > outer2 || d2 < inner2) continue;

            var ang = AngleDeg(dx, dy);
            if (!InArc(ang)) continue;

            c.SetPixel(x, y, color);
        }
    }

    public static void DitheredRing(PixelCanvas c, int cx, int cy, int radius, int thickness, Rgba32 color, Func<int, int, bool> mask)
    {
        var innerRadius = Math.Max(0, radius - thickness);
        var outer2 = radius * radius;
        var inner2 = innerRadius * innerRadius;

        for (var y = cy - radius; y <= cy + radius; y++)
        for (var x = cx - radius; x <= cx + radius; x++)
        {
            var dx = x - cx;
            var dy = y - cy;
            var d2 = dx * dx + dy * dy;
            if (d2 > outer2 || d2 < inner2) continue;
            if (!mask(x, y)) continue;
            c.SetPixel(x, y, color);
        }
    }

    public static void Diamond(PixelCanvas c, int cx, int cy, int half, Rgba32 fill, Rgba32? border = null)
    {
        // Diamond where |dx| + |dy| <= half.
        for (var y = cy - half; y <= cy + half; y++)
        for (var x = cx - half; x <= cx + half; x++)
        {
            var d = Math.Abs(x - cx) + Math.Abs(y - cy);
            if (d > half) continue;

            if (border.HasValue && d == half)
                c.SetPixel(x, y, border.Value);
            else
                c.SetPixel(x, y, fill);
        }
    }
}
