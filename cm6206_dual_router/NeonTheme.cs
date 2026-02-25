using System.Drawing;
using System.Windows.Forms;

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

    private static readonly Font BaseUiFont = GetSystemUiFont();
    private static readonly Font MonoUiFont = GetSystemUiFont();

    // NOTE: These intentionally return shared system fonts (no custom construction) to avoid
    // rare-but-real startup hangs during GDI font creation on some machines.
    public static Font CreateBaseFont(float sizePx, FontStyle style = FontStyle.Regular) => BaseUiFont;

    public static Font CreateMonoFont(float sizePx, FontStyle style = FontStyle.Regular) => MonoUiFont;

    private static Font GetSystemUiFont()
    {
        try
        {
            return SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;
        }
        catch
        {
            return Control.DefaultFont;
        }
    }
}
