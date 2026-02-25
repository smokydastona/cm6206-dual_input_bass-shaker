using System.Text;
using SixLabors.ImageSharp.PixelFormats;

namespace Cm6206AssetGenerator;

internal static class SvgExports
{
    private static string SvgStart(int w, int h)
        => $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{w}\" height=\"{h}\" viewBox=\"0 0 {w} {h}\" shape-rendering=\"crispEdges\">";

    private static string SvgEnd() => "</svg>";

    private static string Rect(int x, int y, int w, int h, Rgba32 c)
        => $"<rect x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{h}\" fill=\"{ColorUtil.ToHexRgb(c)}\"/>";

    public static string KnobSvg(int size, Palette p, bool primary, string state)
    {
        var sb = new StringBuilder();
        sb.Append(SvgStart(size, size));
        sb.Append(Rect(0, 0, size, size, p.BgPrimary));

        // For SVG, provide a geometry-based approximation with the same palette.
        // Pixel-perfect PNG is the canonical export.
        var radius = primary ? 28 : 22;
        var cx = size / 2;
        var cy = size / 2;
        var fill = state == "active" ? p.GraphitePressed : p.BgRaised;
        var arc = state == "hover" ? p.CyanBright20 : p.Cyan;

        sb.Append($"<circle cx=\"{cx}\" cy=\"{cy + (state == "active" ? 1 : 0)}\" r=\"{radius}\" fill=\"{ColorUtil.ToHexRgb(fill)}\"/>");
        sb.Append($"<circle cx=\"{cx}\" cy=\"{cy + (state == "active" ? 1 : 0)}\" r=\"{radius}\" fill=\"none\" stroke=\"{ColorUtil.ToHexRgb(p.BorderOuter)}\" stroke-width=\"2\"/>");

        // Accent arc (approx)
        var arcThickness = primary ? 4 : 3;
        sb.Append($"<path d=\"{ArcPath(cx, cy + (state == "active" ? 1 : 0), radius, 135, 45)}\" fill=\"none\" stroke=\"{ColorUtil.ToHexRgb(arc)}\" stroke-width=\"{arcThickness}\" stroke-linecap=\"round\"/>");

        // Indicator
        sb.Append($"<rect x=\"{cx - (primary ? 3 : 2)}\" y=\"{cy - radius + 6}\" width=\"{(primary ? 6 : 4)}\" height=\"2\" fill=\"{ColorUtil.ToHexRgb(p.White)}\"/>");

        if (state == "hover")
        {
            sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{radius + 2}\" fill=\"none\" stroke=\"{ColorUtil.ToHexRgb(p.Cyan30OnBg)}\" stroke-width=\"1\"/>");
        }

        sb.Append(SvgEnd());
        return sb.ToString();
    }

    public static string SliderTrackSvg(int w, int h, Palette p)
    {
        var sb = new StringBuilder();
        sb.Append(SvgStart(w, h));
        sb.Append(Rect(0, 0, w, h, p.TrackBase));
        sb.Append(Rect(0, 0, w, 1, p.HighlightTop));
        sb.Append(Rect(0, h - 1, w, 1, p.ShadowBottom));
        sb.Append(SvgEnd());
        return sb.ToString();
    }

    public static string SliderThumbSvg(int w, int h, Palette p, string state)
    {
        var sb = new StringBuilder();
        sb.Append(SvgStart(w, h));
        sb.Append(Rect(0, 0, w, h, p.BgPrimary));
        // diamond path
        sb.Append($"<path d=\"M {w / 2} 0 L {w - 1} {h / 2} L {w / 2} {h - 1} L 0 {h / 2} Z\" fill=\"{ColorUtil.ToHexRgb(p.BgPanel)}\" stroke=\"{ColorUtil.ToHexRgb(p.White)}\" stroke-width=\"1\"/>");
        if (state == "hover")
            sb.Append($"<path d=\"M {w / 2} 0 L {w - 1} {h / 2} L {w / 2} {h - 1} L 0 {h / 2} Z\" fill=\"none\" stroke=\"{ColorUtil.ToHexRgb(p.Cyan30OnPanel)}\" stroke-width=\"1\"/>");
        sb.Append(SvgEnd());
        return sb.ToString();
    }

    public static string ToggleSvg(int w, int h, Palette p, string state)
    {
        var sb = new StringBuilder();
        sb.Append(SvgStart(w, h));
        sb.Append(Rect(0, 0, w, h, p.BgPrimary));
        var track = state == "on" ? p.Cyan20OnPanel : p.TrackBase;
        sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"{h / 2}\" ry=\"{h / 2}\" fill=\"{ColorUtil.ToHexRgb(track)}\" stroke=\"{ColorUtil.ToHexRgb(p.BorderOuter)}\"/>");
        var cx = state == "on" ? w - 2 - 9 : 2 + 9;
        sb.Append($"<circle cx=\"{cx}\" cy=\"{h / 2}\" r=\"9\" fill=\"{ColorUtil.ToHexRgb(p.BgRaised)}\" stroke=\"{ColorUtil.ToHexRgb(p.White)}\"/>");
        sb.Append(SvgEnd());
        return sb.ToString();
    }

    public static string ButtonPrimarySvg(int w, int h, Palette p, string state)
    {
        var sb = new StringBuilder();
        sb.Append(SvgStart(w, h));
        sb.Append(Rect(0, 0, w, h, p.BgPrimary));
        var fill = state == "pressed" ? p.GraphitePressed : p.BgRaised;
        sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"{ColorUtil.ToHexRgb(fill)}\" stroke=\"{ColorUtil.ToHexRgb(p.BorderOuter)}\"/>");
        sb.Append($"<rect x=\"1\" y=\"1\" width=\"{w - 2}\" height=\"1\" fill=\"{ColorUtil.ToHexRgb(p.HighlightTop)}\"/>");
        sb.Append($"<rect x=\"1\" y=\"{h - 2}\" width=\"{w - 2}\" height=\"1\" fill=\"{ColorUtil.ToHexRgb(p.ShadowBottom)}\"/>");
        if (state == "hover")
            sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"none\" stroke=\"{ColorUtil.ToHexRgb(p.Cyan30OnPanel)}\" stroke-width=\"1\"/>");
        sb.Append(SvgEnd());
        return sb.ToString();
    }

    public static string IconButtonSvg(int w, int h, Palette p, string state)
    {
        var sb = new StringBuilder();
        sb.Append(SvgStart(w, h));
        sb.Append(Rect(0, 0, w, h, p.BgPrimary));
        sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"{ColorUtil.ToHexRgb(p.BgPanel)}\" stroke=\"{ColorUtil.ToHexRgb(p.BorderOuter)}\"/>");
        if (state == "hover")
            sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"none\" stroke=\"{ColorUtil.ToHexRgb(p.Cyan30OnPanel)}\" stroke-width=\"1\"/>");
        sb.Append(SvgEnd());
        return sb.ToString();
    }

    public static string MeterVerticalSvg(int w, int h, Palette p)
    {
        var sb = new StringBuilder();
        sb.Append(SvgStart(w, h));
        sb.Append(Rect(0, 0, w, h, p.BgPrimary));
        sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"{ColorUtil.ToHexRgb(p.BgPanel)}\" stroke=\"{ColorUtil.ToHexRgb(p.BorderOuter)}\"/>");
        sb.Append(SvgEnd());
        return sb.ToString();
    }

    public static string GrMeterSvg(int w, int h, Palette p)
    {
        var sb = new StringBuilder();
        sb.Append(SvgStart(w, h));
        sb.Append(Rect(0, 0, w, h, p.BgPrimary));
        sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"{ColorUtil.ToHexRgb(p.BgPanel)}\" stroke=\"{ColorUtil.ToHexRgb(p.BorderOuter)}\"/>");
        sb.Append(SvgEnd());
        return sb.ToString();
    }

    public static string CardSvg(int w, int h, Palette p, int radius)
    {
        var sb = new StringBuilder();
        sb.Append(SvgStart(w, h));
        sb.Append(Rect(0, 0, w, h, p.BgPrimary));

        // Checkerboard fill via pattern
        sb.Append("<defs>");
        sb.Append($"<pattern id=\"chk\" width=\"2\" height=\"2\" patternUnits=\"userSpaceOnUse\">");
        sb.Append($"<rect x=\"0\" y=\"0\" width=\"1\" height=\"1\" fill=\"{ColorUtil.ToHexRgb(p.BgPanel)}\"/>");
        sb.Append($"<rect x=\"1\" y=\"1\" width=\"1\" height=\"1\" fill=\"{ColorUtil.ToHexRgb(p.BgPanel)}\"/>");
        sb.Append($"<rect x=\"1\" y=\"0\" width=\"1\" height=\"1\" fill=\"{ColorUtil.ToHexRgb(p.BgRaised)}\"/>");
        sb.Append($"<rect x=\"0\" y=\"1\" width=\"1\" height=\"1\" fill=\"{ColorUtil.ToHexRgb(p.BgRaised)}\"/>");
        sb.Append("</pattern>");
        sb.Append("</defs>");

        sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"url(#chk)\" stroke=\"{ColorUtil.ToHexRgb(p.BorderOuter)}\" stroke-width=\"2\"/>");
        sb.Append(SvgEnd());
        return sb.ToString();
    }

    public static string BackgroundSvg(int w, int h, Palette p)
    {
        var sb = new StringBuilder();
        sb.Append(SvgStart(w, h));

        sb.Append("<defs>");
        // tileable 4x4 25% noise mask using panel pixels
        sb.Append($"<pattern id=\"noise25\" width=\"4\" height=\"4\" patternUnits=\"userSpaceOnUse\">");
        sb.Append(Rect(0, 0, 4, 4, p.BgPrimary));
        sb.Append(Rect(0, 0, 1, 1, p.BgPanel));
        sb.Append(Rect(2, 1, 1, 1, p.BgPanel));
        sb.Append(Rect(1, 2, 1, 1, p.BgPanel));
        sb.Append(Rect(3, 3, 1, 1, p.BgPanel));
        sb.Append("</pattern>");
        sb.Append("</defs>");

        sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"url(#noise25)\"/>");

        // Cyan accent lines
        sb.Append($"<rect x=\"0\" y=\"200\" width=\"{w}\" height=\"1\" fill=\"{ColorUtil.ToHexRgb(p.Cyan20OnBg)}\"/>");
        sb.Append($"<rect x=\"0\" y=\"600\" width=\"{w}\" height=\"1\" fill=\"{ColorUtil.ToHexRgb(p.Cyan20OnBg)}\"/>");

        sb.Append(SvgEnd());
        return sb.ToString();
    }

    public static string IconSvg(int w, int h, Palette p, PixelIcon icon)
    {
        // SVG export for icons: emit per-pixel rects (small enough).
        var sb = new StringBuilder();
        sb.Append(SvgStart(w, h));
        sb.Append(Rect(0, 0, w, h, p.BgPrimary));

        // draw into a tiny buffer and dump pixels
        using var c = new PixelCanvas(w, h, p.BgPrimary);
        icon.Draw(c, p);
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var px = c.Image[x, y];
            if (px.Equals(p.BgPrimary)) continue;
            sb.Append(Rect(x, y, 1, 1, px));
        }

        sb.Append(SvgEnd());
        return sb.ToString();
    }

    private static string ArcPath(int cx, int cy, int r, float startDeg, float endDeg)
    {
        // SVG arc path. Handles wrap by choosing large-arc flag.
        (float sx, float sy) = PointOnCircle(cx, cy, r, startDeg);
        (float ex, float ey) = PointOnCircle(cx, cy, r, endDeg);

        // if start->end wraps, treat as large arc.
        var delta = endDeg - startDeg;
        if (delta < 0) delta += 360;
        var large = delta > 180 ? 1 : 0;

        return $"M {sx:0.###} {sy:0.###} A {r} {r} 0 {large} 1 {ex:0.###} {ey:0.###}";
    }

    private static (float x, float y) PointOnCircle(int cx, int cy, int r, float deg)
    {
        var rad = deg * (float)(Math.PI / 180.0);
        var x = cx + (float)Math.Cos(rad) * r;
        var y = cy + (float)Math.Sin(rad) * r;
        return (x, y);
    }
}
