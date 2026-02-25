using System;
using System.Drawing;

namespace Cm6206DualRouter;

internal static class AaaUiMetrics
{
    public const int BaseWidth = 1920;
    public const int BaseHeight = 1080;

    // Spec constants (base pixels)
    public const int OuterMargin = 24;
    public const int HeaderHeight = 64;
    public const int StatusHeight = 32;

    public const int LeftSidebarWidth = 260;  // x=24..284
    public const int RightSidebarWidth = 320; // x=1576..1896

    public const int DspSectionWidth = 323;
    public const int DspSectionHeight = 448;

    public static float ComputeScale(Size clientSize)
    {
        if (clientSize.Width <= 0 || clientSize.Height <= 0)
            return 1f;

        var sx = clientSize.Width / (float)BaseWidth;
        var sy = clientSize.Height / (float)BaseHeight;

        // Use min to preserve aspect. Clamp so we don't get microscopic layout.
        return Math.Clamp(Math.Min(sx, sy), 0.65f, 2.0f);
    }

    public static int S(float scale, int px) => (int)Math.Round(px * scale);
}
