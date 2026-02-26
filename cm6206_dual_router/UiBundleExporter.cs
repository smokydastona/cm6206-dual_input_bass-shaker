using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json.Nodes;

namespace Cm6206DualRouter;

internal static class UiBundleExporter
{
    public static void ExportNeonBundle(string outputRootDir)
    {
        if (string.IsNullOrWhiteSpace(outputRootDir))
            throw new ArgumentException("Output directory is required.", nameof(outputRootDir));

        outputRootDir = Path.GetFullPath(outputRootDir);

        if (File.Exists(outputRootDir))
            throw new InvalidOperationException($"Output path is a file: {outputRootDir}");

        var stylePath = Path.Combine(outputRootDir, "assets", "bassshakertelemetry", "neon", "neon_style.json");
        var animPath = Path.Combine(outputRootDir, "assets", "bassshakertelemetry", "neon", "neon_animation.json");
        var schemaPath = Path.Combine(outputRootDir, "assets", "bassshakertelemetry", "neon", "neon_schema.json");
        var arrowLeftPath = Path.Combine(outputRootDir, "assets", "bassshakertelemetry", "textures", "gui", "neon", "arrow_left.png");
        var arrowRightPath = Path.Combine(outputRootDir, "assets", "bassshakertelemetry", "textures", "gui", "neon", "arrow_right.png");

        Directory.CreateDirectory(Path.GetDirectoryName(stylePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(animPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(schemaPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(arrowLeftPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(arrowRightPath)!);

        // Keep this aligned with the mod's NeonStyle/NeonAnimationTokens JSON keys.
        var primary = NeonTheme.NeonCyan;
        var accent = NeonTheme.NeonAmber;
        var danger = NeonTheme.MeterClip;
        var background = NeonTheme.BgPrimary;
        var panel = NeonTheme.BgPanel;

        // Heuristics for hover/press variants (mod expects distinct keys).
        var primaryHover = Lerp(primary, Color.White, 0.2f);
        var primaryPressed = Lerp(primary, Color.Black, 0.28f);

        var sliderTrack = NeonTheme.BgRaised;
        var toggleOff = Lerp(panel, NeonTheme.TextDisabled, 0.55f);

        const int arrowSizePx = 12;

        var styleJson = $$"""
{
  \"background\": \"{{ToHex(background)}}\",
  \"panel\": \"{{ToHex(panel)}}\",
  \"primary\": \"{{ToHex(primary)}}\",
  \"primary_hover\": \"{{ToHex(primaryHover)}}\",
  \"primary_pressed\": \"{{ToHex(primaryPressed)}}\",
  \"accent\": \"{{ToHex(accent)}}\",
  \"danger\": \"{{ToHex(danger)}}\",

  \"radius\": 4,
  \"padding\": 6,

  \"slider\": {
    \"track\": \"{{ToHex(sliderTrack)}}\",
    \"fill\": \"{{ToHex(primary)}}\"
  },

  \"toggle\": {
    \"on\": \"{{ToHex(primary)}}\",
    \"off\": \"{{ToHex(toggleOff)}}\"
  },

  \"cycle\": {
    \"arrow_left\": \"bassshakertelemetry:textures/gui/neon/arrow_left.png\",
    \"arrow_right\": \"bassshakertelemetry:textures/gui/neon/arrow_right.png\",
    \"arrow_size\": {{arrowSizePx}}
  }
}
""";

        var animJson = """
{
  "brightness": {
    "idle": 1.0,
    "hover": 1.15,
    "pressed": 0.9
  },
  "timing": {
    "hover_speed": 0.25,
    "press_speed": 0.18,
    "idle_speed": 0.2
  }
}
""";

        // Shared UI schema (tree-based): screens -> root panel -> children.
        // The Minecraft mod consumes this as neon/neon_schema.json from the active UI bundle.
        JsonObject schema = NeonSchemaBuilder.BuildSchema();
        var schemaJson = NeonSchemaBuilder.ToJson(schema);

        File.WriteAllText(stylePath, styleJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(animPath, animJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(schemaPath, schemaJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // Arrow icons: crisp, transparent background, filled cyan arrow.
        WriteArrowPng(arrowLeftPath, isLeft: true, sizePx: arrowSizePx, color: primary);
        WriteArrowPng(arrowRightPath, isLeft: false, sizePx: arrowSizePx, color: primary);
    }

    private static void WriteArrowPng(string path, bool isLeft, int sizePx, Color color)
    {
        using var bmp = new Bitmap(sizePx, sizePx, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);

      g.SmoothingMode = SmoothingMode.AntiAlias;
      g.PixelOffsetMode = PixelOffsetMode.HighQuality;
      g.InterpolationMode = InterpolationMode.HighQualityBicubic;

      // Anti-aliased chevron with a subtle glow stroke.
      int pad = Math.Max(1, sizePx / 6);
      float midY = (sizePx - 1) / 2.0f;
      float tipX = isLeft ? pad : (sizePx - pad - 1);
      float baseX = isLeft ? (sizePx - pad - 1) : pad;
      float topY = pad;
      float botY = sizePx - pad - 1;

      var p1 = new PointF(baseX, topY);
      var p2 = new PointF(tipX, midY);
      var p3 = new PointF(baseX, botY);

      using var glowPen = new Pen(Color.FromArgb(90, color), 4.0f)
      {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round,
        LineJoin = LineJoin.Round
      };
      using var pen = new Pen(color, 2.0f)
      {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round,
        LineJoin = LineJoin.Round
      };

      g.DrawLines(glowPen, new[] { p1, p2, p3 });
      g.DrawLines(pen, new[] { p1, p2, p3 });

        bmp.Save(path, ImageFormat.Png);
    }

    private static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static Color Lerp(Color a, Color b, float t)
    {
        if (t <= 0) return a;
        if (t >= 1) return b;

        static int Lerp8(int x, int y, float tt) => (int)Math.Round(x + (y - x) * tt);
        return Color.FromArgb(
            255,
            Lerp8(a.R, b.R, t),
            Lerp8(a.G, b.G, t),
            Lerp8(a.B, b.B, t));
    }
}
