using SixLabors.ImageSharp.PixelFormats;

namespace Cm6206AssetGenerator;

internal sealed class PixelIcon
{
    public string Name { get; }
    private readonly Action<PixelCanvas, Palette> _draw;

    public PixelIcon(string name, Action<PixelCanvas, Palette> draw)
    {
        Name = name;
        _draw = draw;
    }

    public void Draw(PixelCanvas c, Palette p) => _draw(c, p);
}

internal static class IconLibrary
{
    public static IReadOnlyList<PixelIcon> GetAll() => new[]
    {
        Settings(), Device(), InputA(), InputB(), Filters(), Limiter(), Output(),
        Warning(), Error(), Success(),
        PresetGaming(), PresetMovies(), PresetMusic(), PresetCustom()
    };

    private static PixelIcon Settings() => new("settings", (c, p) =>
    {
        // Gear-ish: ring + 4 teeth.
        Ring(c, 12, 12, 7, p.TextSecondary);
        Ring(c, 12, 12, 5, p.BgRaised);
        Tooth(c, 12, 3, 2, 2, p.TextSecondary);
        Tooth(c, 12, 21, 2, 2, p.TextSecondary);
        Tooth(c, 3, 12, 2, 2, p.TextSecondary);
        Tooth(c, 21, 12, 2, 2, p.TextSecondary);
        Dot(c, 12, 12, p.Cyan);
    });

    private static PixelIcon Device() => new("device", (c, p) =>
    {
        // USB chip: outer box + pins.
        Box(c, 6, 6, 12, 12, p.TextSecondary, p.BgRaised);
        for (var i = 0; i < 4; i++)
        {
            c.SetPixel(5, 8 + i * 3, p.TextSecondary);
            c.SetPixel(18, 8 + i * 3, p.TextSecondary);
        }
        c.SetPixel(10, 10, p.Cyan);
        c.SetPixel(13, 13, p.Cyan);
    });

    private static PixelIcon InputA() => new("input_a", (c, p) =>
    {
        Box(c, 4, 4, 16, 16, p.TextSecondary, p.BgRaised);
        DrawLetterA(c, 8, 7, p.Cyan);
    });

    private static PixelIcon InputB() => new("input_b", (c, p) =>
    {
        Box(c, 4, 4, 16, 16, p.TextSecondary, p.BgRaised);
        DrawLetterB(c, 8, 7, p.Cyan);
    });

    private static PixelIcon Filters() => new("filters", (c, p) =>
    {
        // Funnel.
        for (var x = 5; x <= 18; x++) c.SetPixel(x, 6, p.TextSecondary);
        for (var x = 7; x <= 16; x++) c.SetPixel(x, 7, p.TextSecondary);
        for (var x = 9; x <= 14; x++) c.SetPixel(x, 8, p.TextSecondary);
        for (var y = 9; y <= 16; y++) c.SetPixel(12, y, p.TextSecondary);
        for (var y = 17; y <= 19; y++) c.SetPixel(12, y, p.Cyan);
    });

    private static PixelIcon Limiter() => new("limiter", (c, p) =>
    {
        // Brick wall + arrow.
        for (var y = 6; y <= 18; y++)
            for (var x = 6; x <= 9; x++)
                c.SetPixel(x, y, p.TextSecondary);

        for (var x = 11; x <= 18; x++) c.SetPixel(x, 12, p.Cyan);
        c.SetPixel(17, 11, p.Cyan);
        c.SetPixel(17, 13, p.Cyan);
        c.SetPixel(18, 12, p.Cyan);
    });

    private static PixelIcon Output() => new("output", (c, p) =>
    {
        // Speaker + waves.
        for (var y = 9; y <= 15; y++) c.SetPixel(6, y, p.TextSecondary);
        for (var x = 6; x <= 10; x++) c.SetPixel(x, 9, p.TextSecondary);
        for (var x = 6; x <= 10; x++) c.SetPixel(x, 15, p.TextSecondary);
        for (var y = 10; y <= 14; y++) c.SetPixel(10, y, p.TextSecondary);

        c.SetPixel(14, 11, p.Cyan);
        c.SetPixel(15, 12, p.Cyan);
        c.SetPixel(14, 13, p.Cyan);
        c.SetPixel(17, 10, p.Cyan);
        c.SetPixel(18, 12, p.Cyan);
        c.SetPixel(17, 14, p.Cyan);
    });

    private static PixelIcon Warning() => new("warning", (c, p) =>
    {
        // Triangle + exclamation.
        for (var x = 12; x <= 12; x++) c.SetPixel(x, 6, p.TextSecondary);
        for (var x = 11; x <= 13; x++) c.SetPixel(x, 7, p.TextSecondary);
        for (var x = 10; x <= 14; x++) c.SetPixel(x, 8, p.TextSecondary);
        for (var x = 9; x <= 15; x++) c.SetPixel(x, 9, p.TextSecondary);
        for (var x = 8; x <= 16; x++) c.SetPixel(x, 10, p.TextSecondary);
        for (var y = 10; y <= 18; y++)
        {
            c.SetPixel(8 + (y - 10), y, p.TextSecondary);
            c.SetPixel(16 - (y - 10), y, p.TextSecondary);
        }
        for (var x = 8; x <= 16; x++) c.SetPixel(x, 18, p.TextSecondary);

        for (var y = 11; y <= 15; y++) c.SetPixel(12, y, p.Cyan);
        c.SetPixel(12, 17, p.Cyan);
    });

    private static PixelIcon Error() => new("error", (c, p) =>
    {
        // X mark.
        for (var i = 0; i < 14; i++)
        {
            c.SetPixel(5 + i, 5 + i, p.ClipRed);
            c.SetPixel(5 + i, 18 - i, p.ClipRed);
        }
    });

    private static PixelIcon Success() => new("success", (c, p) =>
    {
        // Check.
        for (var i = 0; i < 6; i++) c.SetPixel(7 + i, 14 + i / 2, p.Cyan);
        for (var i = 0; i < 10; i++) c.SetPixel(10 + i, 16 - i, p.Cyan);
    });

    private static PixelIcon PresetGaming() => new("preset_gaming", (c, p) =>
    {
        // Simple gamepad.
        Box(c, 6, 10, 12, 8, p.TextSecondary, p.BgRaised);
        c.SetPixel(9, 13, p.Cyan);
        c.SetPixel(10, 13, p.Cyan);
        c.SetPixel(9, 14, p.Cyan);
        c.SetPixel(14, 13, p.Cyan);
        c.SetPixel(15, 14, p.Cyan);
    });

    private static PixelIcon PresetMovies() => new("preset_movies", (c, p) =>
    {
        // Clapperboard.
        Box(c, 5, 8, 14, 12, p.TextSecondary, p.BgRaised);
        for (var i = 0; i < 14; i += 2)
            c.SetPixel(6 + i, 8, p.Cyan);
    });

    private static PixelIcon PresetMusic() => new("preset_music", (c, p) =>
    {
        // Note.
        for (var y = 7; y <= 16; y++) c.SetPixel(14, y, p.TextSecondary);
        for (var x = 10; x <= 14; x++) c.SetPixel(x, 7, p.TextSecondary);
        c.SetPixel(10, 11, p.Cyan);
        c.SetPixel(9, 12, p.Cyan);
        c.SetPixel(9, 13, p.Cyan);
        c.SetPixel(10, 14, p.Cyan);
    });

    private static PixelIcon PresetCustom() => new("preset_custom", (c, p) =>
    {
        // Sliders.
        for (var y = 6; y <= 18; y++)
        {
            c.SetPixel(8, y, p.TextSecondary);
            c.SetPixel(12, y, p.TextSecondary);
            c.SetPixel(16, y, p.TextSecondary);
        }
        Box(c, 7, 10, 3, 3, p.Cyan, p.BgRaised);
        Box(c, 11, 14, 3, 3, p.Cyan, p.BgRaised);
        Box(c, 15, 8, 3, 3, p.Cyan, p.BgRaised);
    });

    // --- tiny drawing helpers ---

    private static void Ring(PixelCanvas c, int cx, int cy, int r, Rgba32 color)
    {
        for (var y = cy - r; y <= cy + r; y++)
        for (var x = cx - r; x <= cx + r; x++)
        {
            var d = Math.Abs((x - cx) * (x - cx) + (y - cy) * (y - cy) - r * r);
            if (d <= r) c.SetPixel(x, y, color);
        }
    }

    private static void Tooth(PixelCanvas c, int cx, int cy, int w, int h, Rgba32 col)
        => c.FillRect(cx - w / 2, cy - h / 2, w, h, col);

    private static void Dot(PixelCanvas c, int x, int y, Rgba32 col) => c.SetPixel(x, y, col);

    private static void Box(PixelCanvas c, int x, int y, int w, int h, Rgba32 border, Rgba32 fill)
    {
        c.FillRect(x, y, w, h, fill);
        for (var xx = x; xx < x + w; xx++)
        {
            c.SetPixel(xx, y, border);
            c.SetPixel(xx, y + h - 1, border);
        }
        for (var yy = y; yy < y + h; yy++)
        {
            c.SetPixel(x, yy, border);
            c.SetPixel(x + w - 1, yy, border);
        }
    }

    private static void DrawLetterA(PixelCanvas c, int x, int y, Rgba32 col)
    {
        c.SetPixel(x + 1, y, col);
        c.SetPixel(x, y + 1, col);
        c.SetPixel(x + 2, y + 1, col);
        for (var yy = 2; yy <= 8; yy++)
        {
            c.SetPixel(x, y + yy, col);
            c.SetPixel(x + 2, y + yy, col);
        }
        c.SetPixel(x + 1, y + 4, col);
    }

    private static void DrawLetterB(PixelCanvas c, int x, int y, Rgba32 col)
    {
        for (var yy = 0; yy <= 9; yy++) c.SetPixel(x, y + yy, col);
        c.SetPixel(x + 1, y, col);
        c.SetPixel(x + 2, y + 1, col);
        c.SetPixel(x + 1, y + 4, col);
        c.SetPixel(x + 2, y + 5, col);
        c.SetPixel(x + 1, y + 9, col);
        for (var yy = 2; yy <= 3; yy++) c.SetPixel(x + 2, y + yy, col);
        for (var yy = 6; yy <= 8; yy++) c.SetPixel(x + 2, y + yy, col);
    }
}
