using System.Drawing;

namespace Cm6206DualRouter;

internal static class NeonTheme
{
    // Base
    public static readonly Color BgPrimary = ColorTranslator.FromHtml("#0A0C10");
    public static readonly Color BgPanel = ColorTranslator.FromHtml("#11141A");
    public static readonly Color BgRaised = ColorTranslator.FromHtml("#1A1E26");

    // Text
    public static readonly Color TextPrimary = ColorTranslator.FromHtml("#FFFFFF");
    public static readonly Color TextSecondary = ColorTranslator.FromHtml("#A9B4C6");
    public static readonly Color TextDisabled = ColorTranslator.FromHtml("#5C6370");

    // Back-compat alias used by some UI surfaces.
    public static Color TextMuted => TextSecondary;

    // Neon accents (defaults chosen by us)
    public static readonly Color NeonCyan = ColorTranslator.FromHtml("#00F6FF");
    public static readonly Color NeonPurple = ColorTranslator.FromHtml("#B100FF");
    public static readonly Color NeonAmber = ColorTranslator.FromHtml("#FFB000");

    // Meter palette
    public static readonly Color MeterLow = ColorTranslator.FromHtml("#00F6FF");
    public static readonly Color MeterMid = ColorTranslator.FromHtml("#7A5CFF");
    public static readonly Color MeterHigh = ColorTranslator.FromHtml("#FFB000");
    public static readonly Color MeterClip = ColorTranslator.FromHtml("#FF3B3B");

    public static Font CreateBaseFont(float sizePx, FontStyle style = FontStyle.Regular)
    {
        // Try Inter / JetBrains Sans, fall back to Segoe UI.
        foreach (var family in new[] { "Inter", "JetBrains Sans", "Segoe UI" })
        {
            try
            {
                return new Font(family, sizePx, style, GraphicsUnit.Pixel);
            }
            catch
            {
                // ignore
            }
        }

        return SystemFonts.MessageBoxFont ?? new Font("Segoe UI", sizePx, style, GraphicsUnit.Pixel);
    }

    public static Font CreateMonoFont(float sizePx, FontStyle style = FontStyle.Regular)
    {
        foreach (var family in new[] { "JetBrains Mono", "Cascadia Mono", "Consolas" })
        {
            try
            {
                return new Font(family, sizePx, style, GraphicsUnit.Pixel);
            }
            catch
            {
                // ignore
            }
        }

        return SystemFonts.MessageBoxFont ?? new Font("Consolas", sizePx, style, GraphicsUnit.Pixel);
    }
}
