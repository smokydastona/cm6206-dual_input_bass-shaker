using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace Cm6206DualRouter;

internal static class AaaAssets
{
    private static readonly ConcurrentDictionary<string, Image?> Cache = new(StringComparer.OrdinalIgnoreCase);

    // Default to dark until we add a user-facing theme switch.
    public static string ThemeName { get; set; } = "dark";

    public static string AssetsRoot
        => Path.Combine(AppContext.BaseDirectory, "assets", "generated", "png", ThemeName);

    public static Image? TryGetPng(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        return Cache.GetOrAdd(fileName, static (name) =>
        {
            try
            {
                var path = Path.Combine(AssetsRoot, name);
                if (!File.Exists(path)) return null;

                // Avoid holding an open file handle by cloning from a stream.
                using var fs = File.OpenRead(path);
                using var img = Image.FromStream(fs);
                return new Bitmap(img);
            }
            catch
            {
                return null;
            }
        });
    }

    public static void DrawNearestNeighbor(Graphics g, Image image, Rectangle dest, Rectangle? src = null)
    {
        if (dest.Width <= 0 || dest.Height <= 0) return;

        var prevInterp = g.InterpolationMode;
        var prevPixel = g.PixelOffsetMode;
        var prevSmoothing = g.SmoothingMode;

        try
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;

            if (src is { } s)
            {
                g.DrawImage(image, dest, s, GraphicsUnit.Pixel);
            }
            else
            {
                g.DrawImage(image, dest);
            }
        }
        finally
        {
            g.InterpolationMode = prevInterp;
            g.PixelOffsetMode = prevPixel;
            g.SmoothingMode = prevSmoothing;
        }
    }

    public static Rectangle SpriteFrame(int frameIndex, int frameWidth, int frameHeight)
        => new(frameIndex * frameWidth, 0, frameWidth, frameHeight);
}
