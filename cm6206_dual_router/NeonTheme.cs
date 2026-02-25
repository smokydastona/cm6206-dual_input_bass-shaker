using System.Drawing;
using System.Diagnostics;

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
        // Prefer standard Windows fonts first for maximum reliability.
        // Custom fonts (Inter/JetBrains) are optional and occasionally problematic on some systems.
        var sw = Stopwatch.StartNew();
        SafeInfo($"NeonTheme.CreateBaseFont start sizePx={sizePx} style={style}");

        foreach (var family in new[] { "Segoe UI", "Inter", "JetBrains Sans" })
        {
            try
            {
                SafeInfo($"NeonTheme.CreateBaseFont trying family='{family}'");
                return new Font(family, sizePx, style, GraphicsUnit.Pixel);
            }
            catch
            {
                // ignore
            }
        }

        try
        {
            var fallback = SystemFonts.MessageBoxFont ?? new Font("Segoe UI", sizePx, style, GraphicsUnit.Pixel);
            SafeInfo($"NeonTheme.CreateBaseFont fallback in {sw.ElapsedMilliseconds}ms");
            return fallback;
        }
        catch
        {
            // absolute last resort
            return new Font(FontFamily.GenericSansSerif, sizePx, style, GraphicsUnit.Pixel);
        }
    }

    public static Font CreateMonoFont(float sizePx, FontStyle style = FontStyle.Regular)
    {
        var sw = Stopwatch.StartNew();
        SafeInfo($"NeonTheme.CreateMonoFont start sizePx={sizePx} style={style}");

        // Prefer standard monospace fonts first.
        foreach (var family in new[] { "Consolas", "Cascadia Mono", "JetBrains Mono" })
        {
            try
            {
                SafeInfo($"NeonTheme.CreateMonoFont trying family='{family}'");
                return new Font(family, sizePx, style, GraphicsUnit.Pixel);
            }
            catch
            {
                // ignore
            }
        }

        try
        {
            var fallback = SystemFonts.MessageBoxFont ?? new Font("Consolas", sizePx, style, GraphicsUnit.Pixel);
            SafeInfo($"NeonTheme.CreateMonoFont fallback in {sw.ElapsedMilliseconds}ms");
            return fallback;
        }
        catch
        {
            return new Font(FontFamily.GenericMonospace, sizePx, style, GraphicsUnit.Pixel);
        }
    }

    private static void SafeInfo(string message)
    {
        try
        {
            AppLog.Info(message);
        }
        catch
        {
            // ignore
        }
    }
}
