using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Cm6206AssetGenerator;

internal sealed record Palette(
    Rgba32 BgPrimary,
    Rgba32 BgPanel,
    Rgba32 BgRaised,
    Rgba32 TextSecondary,
    Rgba32 White,
    Rgba32 Cyan,
    Rgba32 MeterLow,
    Rgba32 MeterMid,
    Rgba32 MeterHigh,
    Rgba32 ClipRed,
    // Derived
    Rgba32 BorderOuter,
    Rgba32 BorderInner,
    Rgba32 HighlightTop,
    Rgba32 ShadowBottom,
    Rgba32 TrackBase,
    Rgba32 GraphitePressed,
    Rgba32 CyanBright20,
    Rgba32 CyanBright35,
    Rgba32 Cyan30OnBg,
    Rgba32 Cyan20OnBg,
    Rgba32 Cyan30OnPanel,
    Rgba32 Cyan20OnPanel)
{
    public static Palette For(ThemeVariant theme)
    {
        // Values align with docs/assets/PIXEL_ASSET_PACK_BLUEPRINT.md
        if (theme == ThemeVariant.Dark)
        {
            return new Palette(
                BgPrimary: Hex("#0A0C10"),
                BgPanel: Hex("#11141A"),
                BgRaised: Hex("#1A1E26"),
                TextSecondary: Hex("#A9B4C6"),
                White: Hex("#FFFFFF"),
                Cyan: Hex("#00F6FF"),
                MeterLow: Hex("#00F6FF"),
                MeterMid: Hex("#7A5CFF"),
                MeterHigh: Hex("#FFB000"),
                ClipRed: Hex("#FF3B3B"),
                BorderOuter: Hex("#3E444E"),
                BorderInner: Hex("#111419"),
                HighlightTop: Hex("#43464D"),
                ShadowBottom: Hex("#0C0E11"),
                TrackBase: Hex("#15181F"),
                GraphitePressed: Hex("#171B22"),
                CyanBright20: Hex("#33F8FF"),
                CyanBright35: Hex("#59F9FF"),
                Cyan30OnBg: Hex("#075258"),
                Cyan20OnBg: Hex("#083B40"),
                Cyan30OnPanel: Hex("#0C585F"),
                Cyan20OnPanel: Hex("#0E4148"));
        }

        // Light theme: recolor pass. Keep cyan + meter colors identical.
        // Border/highlight/shadow are hand-picked to preserve contrast on stone.
        return new Palette(
            BgPrimary: Hex("#ECECED"),
            BgPanel: Hex("#DEDEDF"),
            BgRaised: Hex("#D6D6D8"),
            TextSecondary: Hex("#3B3F45"),
            White: Hex("#FFFFFF"),
            Cyan: Hex("#00F6FF"),
            MeterLow: Hex("#00F6FF"),
            MeterMid: Hex("#7A5CFF"),
            MeterHigh: Hex("#FFB000"),
            ClipRed: Hex("#FF3B3B"),
            BorderOuter: Hex("#B7BCC6"),
            BorderInner: Hex("#C9CDD4"),
            HighlightTop: Hex("#FFFFFF"),
            ShadowBottom: Hex("#A3A8B2"),
            TrackBase: Hex("#D2D3D7"),
            GraphitePressed: Hex("#C9CACE"),
            CyanBright20: Hex("#33F8FF"),
            CyanBright35: Hex("#59F9FF"),
            // Preblended cyan pixels against stone (approx):
            Cyan30OnBg: Hex("#B2EFF2"),
            Cyan20OnBg: Hex("#BEECEF"),
            Cyan30OnPanel: Hex("#A8EAEE"),
            Cyan20OnPanel: Hex("#B6E7EB"));
    }

    private static Rgba32 Hex(string hex) => ColorUtil.ParseHex(hex);
}
