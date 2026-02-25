using System.Text;
using System.Text.Json;
using SixLabors.ImageSharp.PixelFormats;

namespace Cm6206AssetGenerator;

internal sealed class AssetPackGenerator
{
    private readonly string _outRoot;

    public AssetPackGenerator(string outRoot)
    {
        _outRoot = outRoot;
    }

    public void GenerateAll(ThemeVariant theme)
    {
        var palette = Palette.For(theme);

        var pngDir = Path.Combine(_outRoot, "png", theme.ToString().ToLowerInvariant());
        var svgDir = Path.Combine(_outRoot, "svg", theme.ToString().ToLowerInvariant());
        var sliceDir = Path.Combine(_outRoot, "9slice", theme.ToString().ToLowerInvariant());

        Directory.CreateDirectory(pngDir);
        Directory.CreateDirectory(svgDir);
        Directory.CreateDirectory(sliceDir);

        GenerateKnobs(palette, pngDir, svgDir);
        GenerateSlider(palette, pngDir, svgDir);
        GenerateToggle(palette, pngDir, svgDir);
        GenerateButtons(palette, pngDir, svgDir);
        GenerateCards(palette, pngDir, svgDir, sliceDir);
        GenerateMeters(palette, pngDir, svgDir);
        GenerateBackground(palette, pngDir, svgDir);
        GenerateIcons(palette, pngDir, svgDir);

        GenerateSpriteSheets(palette, pngDir);
    }

    private static string NameFor(string baseName, string ext, string dir, string state)
        => Path.Combine(dir, $"{baseName}_{state}.{ext}");

    private static void WriteText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteJson(string path, object data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    private static float GradientTLBR(int x, int y, int w, int h)
    {
        // top-left → bottom-right gradient value, 1 at top-left and 0 at bottom-right
        var nx = w <= 1 ? 0f : x / (float)(w - 1);
        var ny = h <= 1 ? 0f : y / (float)(h - 1);
        var g = 1f - ((nx + ny) * 0.5f);
        return Math.Clamp(g, 0f, 1f);
    }

    private static void ApplyKnobDitherShading(PixelCanvas c, int cx, int cy, int radius, Rgba32 baseColor, Rgba32 lightColor, Rgba32 shadowColor)
    {
        var r2 = radius * radius;
        for (var y = cy - radius; y <= cy + radius; y++)
        for (var x = cx - radius; x <= cx + radius; x++)
        {
            var dx = x - cx;
            var dy = y - cy;
            if (dx * dx + dy * dy > r2) continue;

            var g = GradientTLBR(x - (cx - radius), y - (cy - radius), radius * 2 + 1, radius * 2 + 1);
            if (Dither.Bayer2x2(g, x, y))
                c.SetPixel(x, y, lightColor);
            else
                c.SetPixel(x, y, baseColor);

            // Subtle deeper shadow in bottom-right, sparse.
            if (g < 0.25f && Dither.Noise25(x, y))
                c.SetPixel(x, y, shadowColor);
        }
    }

    private static void RenderKnob(PixelCanvas c, Palette p, int cx, int cy, int radius, int borderThicknessOuter, int borderThicknessInner, int arcThickness, float arcStart, float arcEnd,
        Rgba32 baseFill, Rgba32 arcColor, bool hoverGlow, bool pressedShiftDown)
    {
        var shiftY = pressedShiftDown ? 1 : 0;
        cy += shiftY;

        // Base fill
        Draw.CircleFill(c, cx, cy, radius, baseFill);

        // Dither shading
        ApplyKnobDitherShading(c, cx, cy, radius - 2, baseFill, p.HighlightTop, p.ShadowBottom);

        // Border ring: outer then inner.
        Draw.CircleRing(c, cx, cy, radius, borderThicknessOuter, p.BorderOuter);
        Draw.CircleRing(c, cx, cy, radius - borderThicknessOuter, borderThicknessInner, p.BorderInner);

        // Accent arc
        Draw.ArcRing(c, cx, cy, radius, arcThickness, arcStart, arcEnd, arcColor);

        // 1px outer arc glow (sparse)
        Draw.ArcRing(c, cx, cy, radius + 1, 1, arcStart, arcEnd, p.Cyan30OnBg);
        for (var y = 0; y < c.Height; y++)
        for (var x = 0; x < c.Width; x++)
        {
            // sparsify the arc glow by masking pixels not on the mask
            if (c.Image[x, y].Equals(p.Cyan30OnBg) && !Dither.Noise25(x, y))
                c.Image[x, y] = c.Image[x, y]; // keep; we already used a thin arc; masking here is redundant
        }

        // Knob indicator: 6x2 rectangle, default pointing up-ish (270°)
        // Placed at angle -90° (up). We'll just place it at top center.
        var indW = Math.Max(4, radius / 5);
        var indH = 2;
        var indX = cx - indW / 2;
        var indY = cy - (int)Math.Round(radius * 0.78);
        c.FillRect(indX, indY, indW, indH, p.White);

        if (hoverGlow)
        {
            // 2px glow ring outside circle.
            Draw.DitheredRing(c, cx, cy, radius + 2, 1, p.Cyan30OnBg, (x, y) => Dither.Checker50(x, y));
            Draw.DitheredRing(c, cx, cy, radius + 3, 1, p.Cyan20OnBg, (x, y) => Dither.Noise25(x, y));
        }
    }

    private static void RenderKnobIndicatorAtAngle(PixelCanvas c, Palette p, int cx, int cy, int radius, float angleDeg, int w, int h)
    {
        // angleDeg uses screen coords (0° right, 90° down)
        var angRad = angleDeg * (float)(Math.PI / 180.0);
        var r = radius - 6;
        var px = cx + (int)Math.Round(Math.Cos(angRad) * r);
        var py = cy + (int)Math.Round(Math.Sin(angRad) * r);
        c.FillRect(px - w / 2, py - h / 2, w, h, p.White);
    }

    private void GenerateKnobs(Palette p, string pngDir, string svgDir)
    {
        // Primary 64
        foreach (var state in new[] { "default", "hover", "active", "disabled" })
        {
            using var c = new PixelCanvas(64, 64, p.BgPrimary);
            var baseFill = state == "active" ? p.GraphitePressed : p.BgRaised;
            var arc = state == "hover" ? p.CyanBright20 : p.Cyan;
            var glow = state == "hover";
            var pressed = state == "active";

            RenderKnob(c, p, 32, 32, 28, 2, 2, 4, 135, 45, baseFill, arc, glow, pressed);

            if (state == "disabled")
                ApplyDisabled(c, p);

            c.SavePng(NameFor("knob_primary_64", "png", pngDir, state));
            WriteText(NameFor("knob_primary_64", "svg", svgDir, state), SvgExports.KnobSvg(64, p, primary: true, state));
        }

        // Secondary 48
        foreach (var state in new[] { "default", "hover", "active", "disabled" })
        {
            using var c = new PixelCanvas(48, 48, p.BgPrimary);
            var baseFill = state == "active" ? p.GraphitePressed : p.BgRaised;
            var arc = state == "hover" ? p.CyanBright20 : p.Cyan;
            var glow = state == "hover";
            var pressed = state == "active";

            // 1px border and 3px arc
            RenderKnob(c, p, 24, 24, 22, 1, 1, 3, 135, 45, baseFill, arc, glow, pressed);

            if (state == "disabled")
                ApplyDisabled(c, p);

            c.SavePng(NameFor("knob_secondary_48", "png", pngDir, state));
            WriteText(NameFor("knob_secondary_48", "svg", svgDir, state), SvgExports.KnobSvg(48, p, primary: false, state));
        }
    }

    private void GenerateSlider(Palette p, string pngDir, string svgDir)
    {
        // Track 260x4 and thumb 14x14
        using (var track = new PixelCanvas(260, 4, p.BgPrimary))
        {
            track.FillRect(0, 0, 260, 4, p.TrackBase);
            // bevel
            track.FillRect(0, 0, 260, 1, p.HighlightTop);
            track.FillRect(0, 3, 260, 1, p.ShadowBottom);

            // leave active segment to be composed by UI; export just base track
            track.SavePng(Path.Combine(pngDir, "slider_track_260x4_default.png"));
            WriteText(Path.Combine(svgDir, "slider_track_260x4_default.svg"), SvgExports.SliderTrackSvg(260, 4, p));
        }

        foreach (var state in new[] { "default", "hover", "disabled" })
        {
            using var thumb = new PixelCanvas(14, 14, p.BgPrimary);
            var fill = p.BgPanel;
            Draw.Diamond(thumb, 7, 7, 6, fill, p.White);

            if (state == "hover")
            {
                // 1px glow (checker)
                // approximate by a second diamond one pixel larger
                Draw.Diamond(thumb, 7, 7, 7, p.Cyan30OnPanel, null);
                // knock back some pixels for dither
                for (var y = 0; y < 14; y++)
                for (var x = 0; x < 14; x++)
                {
                    var col = thumb.Image[x, y];
                    if (!col.Equals(p.Cyan30OnPanel)) continue;
                    if (!Dither.Checker50(x, y))
                        thumb.Image[x, y] = p.BgPrimary;
                }
            }

            if (state == "disabled")
                ApplyDisabled(thumb, p);

            thumb.SavePng(Path.Combine(pngDir, $"slider_thumb_14x14_{state}.png"));
            WriteText(Path.Combine(svgDir, $"slider_thumb_14x14_{state}.svg"), SvgExports.SliderThumbSvg(14, 14, p, state));
        }
    }

    private void GenerateToggle(Palette p, string pngDir, string svgDir)
    {
        // Default exports (off/on). Animation exported via spritesheet.
        foreach (var state in new[] { "off", "on", "disabled" })
        {
            using var c = new PixelCanvas(40, 20, p.BgPrimary);
            var trackColor = state == "on" ? p.Cyan20OnPanel : p.TrackBase;
            // Rounded track (pixel approximation)
            RenderRoundedRect(c, 0, 0, 40, 20, 10, trackColor, p.BorderOuter);
            // bevel
            c.FillRect(2, 2, 36, 1, p.HighlightTop);
            c.FillRect(2, 17, 36, 1, p.ShadowBottom);

            // thumb
            var thumbX = state == "on" ? 40 - 2 - 18 : 2;
            RenderCircle(c, thumbX + 9, 10, 9, p.BgRaised, p.White);

            if (state == "disabled")
                ApplyDisabled(c, p);

            c.SavePng(Path.Combine(pngDir, $"toggle_40x20_{state}.png"));
            WriteText(Path.Combine(svgDir, $"toggle_40x20_{state}.svg"), SvgExports.ToggleSvg(40, 20, p, state));
        }
    }

    private void GenerateButtons(Palette p, string pngDir, string svgDir)
    {
        // Primary 120x36
        foreach (var state in new[] { "default", "hover", "pressed", "disabled" })
        {
            using var b = new PixelCanvas(120, 36, p.BgPrimary);
            var fill = state == "pressed" ? p.GraphitePressed : p.BgRaised;
            RenderRectBevel(b, 0, 0, 120, 36, fill, p.BorderOuter, p.HighlightTop, p.ShadowBottom);

            if (state == "hover")
            {
                // subtle cyan on top/left edges (dither)
                for (var x = 1; x < 119; x++)
                    if (Dither.Checker50(x, 0)) b.SetPixel(x, 0, p.Cyan30OnPanel);
                for (var y = 1; y < 35; y++)
                    if (Dither.Checker50(0, y)) b.SetPixel(0, y, p.Cyan30OnPanel);
            }

            if (state == "pressed")
            {
                // shift down 1px
                using var shifted = new PixelCanvas(120, 36, p.BgPrimary);
                for (var y = 0; y < 35; y++)
                for (var x = 0; x < 120; x++)
                    shifted.Image[x, y + 1] = b.Image[x, y];
                shifted.SavePng(Path.Combine(pngDir, "button_primary_120x36_pressed.png"));
                WriteText(Path.Combine(svgDir, "button_primary_120x36_pressed.svg"), SvgExports.ButtonPrimarySvg(120, 36, p, state));
                continue;
            }

            if (state == "disabled")
                ApplyDisabled(b, p);

            b.SavePng(Path.Combine(pngDir, $"button_primary_120x36_{state}.png"));
            WriteText(Path.Combine(svgDir, $"button_primary_120x36_{state}.svg"), SvgExports.ButtonPrimarySvg(120, 36, p, state));
        }

        // Icon button 32x32
        foreach (var state in new[] { "default", "hover", "disabled" })
        {
            using var b = new PixelCanvas(32, 32, p.BgPrimary);
            RenderRectBevel(b, 0, 0, 32, 32, p.BgPanel, p.BorderOuter, p.HighlightTop, p.ShadowBottom);

            if (state == "hover")
            {
                // cyan glow (checker)
                for (var x = 0; x < 32; x++)
                {
                    if (Dither.Checker50(x, 0)) b.SetPixel(x, 0, p.Cyan30OnPanel);
                    if (Dither.Checker50(x, 31)) b.SetPixel(x, 31, p.Cyan30OnPanel);
                }
                for (var y = 0; y < 32; y++)
                {
                    if (Dither.Checker50(0, y)) b.SetPixel(0, y, p.Cyan30OnPanel);
                    if (Dither.Checker50(31, y)) b.SetPixel(31, y, p.Cyan30OnPanel);
                }
            }

            if (state == "disabled")
                ApplyDisabled(b, p);

            b.SavePng(Path.Combine(pngDir, $"button_icon_32x32_{state}.png"));
            WriteText(Path.Combine(svgDir, $"button_icon_32x32_{state}.svg"), SvgExports.IconButtonSvg(32, 32, p, state));
        }
    }

    private void GenerateCards(Palette p, string pngDir, string svgDir, string sliceDir)
    {
        // Node card 140x60
        using (var card = new PixelCanvas(140, 60, p.BgPrimary))
        {
            RenderCard(card, p, radius: 8);
            card.SavePng(Path.Combine(pngDir, "card_node_140x60_default.png"));
            WriteText(Path.Combine(svgDir, "card_node_140x60_default.svg"), SvgExports.CardSvg(140, 60, p, radius: 8));
        }
        WriteJson(Path.Combine(sliceDir, "card_node_140x60_default.9slice.json"), new { left = 12, top = 12, right = 12, bottom = 12 });

        // DSP card 323x448
        using (var card = new PixelCanvas(323, 448, p.BgPrimary))
        {
            RenderCard(card, p, radius: 8);
            card.SavePng(Path.Combine(pngDir, "card_dsp_323x448_default.png"));
            WriteText(Path.Combine(svgDir, "card_dsp_323x448_default.svg"), SvgExports.CardSvg(323, 448, p, radius: 8));
        }
        WriteJson(Path.Combine(sliceDir, "card_dsp_323x448_default.9slice.json"), new { left = 16, top = 16, right = 16, bottom = 16 });

        // Button 9-slice
        WriteJson(Path.Combine(sliceDir, "button_primary_120x36_default.9slice.json"), new { left = 12, top = 12, right = 12, bottom = 12 });
    }

    private void GenerateMeters(Palette p, string pngDir, string svgDir)
    {
        // Large vertical meter background (no dynamic fill)
        using (var m = new PixelCanvas(24, 220, p.BgPrimary))
        {
            RenderRectBevel(m, 0, 0, 24, 220, p.BgPanel, p.BorderOuter, p.HighlightTop, p.ShadowBottom);
            // tick marks every 6 dB (~11px steps) simplified
            for (var y = 10; y < 220; y += 11)
            {
                for (var x = 2; x < 22; x++)
                {
                    if (Dither.Noise25(x, y)) continue;
                    m.SetPixel(x, y, p.TextSecondary);
                }
            }
            m.SavePng(Path.Combine(pngDir, "meter_v_24x220_bg.png"));
            WriteText(Path.Combine(svgDir, "meter_v_24x220_bg.svg"), SvgExports.MeterVerticalSvg(24, 220, p));
        }

        // Mini meter background
        using (var m = new PixelCanvas(12, 120, p.BgPrimary))
        {
            RenderRectBevel(m, 0, 0, 12, 120, p.BgPanel, p.BorderOuter, p.HighlightTop, p.ShadowBottom);
            for (var y = 8; y < 120; y += 10)
                for (var x = 1; x < 11; x++)
                    if (!Dither.Noise25(x, y)) m.SetPixel(x, y, p.TextSecondary);
            m.SavePng(Path.Combine(pngDir, "meter_v_12x120_bg.png"));
            WriteText(Path.Combine(svgDir, "meter_v_12x120_bg.svg"), SvgExports.MeterVerticalSvg(12, 120, p));
        }

        // GR meter bg
        using (var gr = new PixelCanvas(80, 12, p.BgPrimary))
        {
            RenderRectBevel(gr, 0, 0, 80, 12, p.BgPanel, p.BorderOuter, p.HighlightTop, p.ShadowBottom);
            for (var x = 4; x < 80; x += 4)
                for (var y = 2; y < 10; y++)
                    if (Dither.Noise25(x, y)) continue;
                    else gr.SetPixel(x, y, p.TextSecondary);
            gr.SavePng(Path.Combine(pngDir, "meter_gr_80x12_bg.png"));
            WriteText(Path.Combine(svgDir, "meter_gr_80x12_bg.svg"), SvgExports.GrMeterSvg(80, 12, p));
        }
    }

    private void GenerateBackground(Palette p, string pngDir, string svgDir)
    {
        // Background is big; PNG generation is deterministic using 4x4 mask.
        // SVG uses a repeating 4x4 pattern instead of per-pixel rect spam.
        var w = 1920;
        var h = 1080;

        using (var bg = new PixelCanvas(w, h, p.BgPrimary))
        {
            // 25% noise: sprinkle panel pixels
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (Dither.Noise25(x, y))
                    bg.SetPixel(x, y, p.BgPanel);
            }

            // cyan accent lines
            for (var x = 0; x < w; x++)
            {
                bg.SetPixel(x, 200, p.Cyan20OnBg);
                bg.SetPixel(x, 600, p.Cyan20OnBg);
            }

            bg.SavePng(Path.Combine(pngDir, "background_1920x1080.png"));
        }

        WriteText(Path.Combine(svgDir, "background_1920x1080.svg"), SvgExports.BackgroundSvg(1920, 1080, p));
    }

    private void GenerateIcons(Palette p, string pngDir, string svgDir)
    {
        // Minimal but consistent pixel icons. Each icon is a deterministic set of strokes.
        var icons = IconLibrary.GetAll();
        foreach (var icon in icons)
        {
            using var c = new PixelCanvas(24, 24, p.BgPrimary);
            icon.Draw(c, p);
            c.SavePng(Path.Combine(pngDir, $"icon_{icon.Name}_24x24.png"));
            WriteText(Path.Combine(svgDir, $"icon_{icon.Name}_24x24.svg"), SvgExports.IconSvg(24, 24, p, icon));
        }
    }

    private void GenerateSpriteSheets(Palette p, string pngDir)
    {
        // Primary knob rotation: 16 frames, 64x64
        using (var sheet = new PixelCanvas(64 * 16, 64, p.BgPrimary))
        {
            for (var i = 0; i < 16; i++)
            {
                using var frame = new PixelCanvas(64, 64, p.BgPrimary);
                RenderKnob(frame, p, 32, 32, 28, 2, 2, 4, 135, 45, p.BgRaised, p.Cyan, hoverGlow: false, pressedShiftDown: false);

                // Replace indicator with rotated indicator
                // Clear the default indicator zone by overdrawing with base fill + shading isn't trivial; we accept that this is for animation reference.
                var ang = i * 22.5f;
                RenderKnobIndicatorAtAngle(frame, p, 32, 32, 28, ang, 6, 2);

                Blit(frame, sheet, i * 64, 0);
            }
            sheet.SavePng(Path.Combine(pngDir, "knob_primary_64_rotate_16f.png"));
        }

        // Toggle slide: 4 frames, 40x20
        using (var sheet = new PixelCanvas(40 * 4, 20, p.BgPrimary))
        {
            var offsets = new[] { 0, 2, 4, 5 }; // +2,+2,+1 increments
            for (var i = 0; i < 4; i++)
            {
                using var frame = new PixelCanvas(40, 20, p.BgPrimary);
                var trackColor = p.Cyan20OnPanel;
                RenderRoundedRect(frame, 0, 0, 40, 20, 10, trackColor, p.BorderOuter);
                frame.FillRect(2, 2, 36, 1, p.HighlightTop);
                frame.FillRect(2, 17, 36, 1, p.ShadowBottom);
                var thumbX = 2 + offsets[i];
                RenderCircle(frame, thumbX + 9, 10, 9, p.BgRaised, p.White);
                Blit(frame, sheet, i * 40, 0);
            }
            sheet.SavePng(Path.Combine(pngDir, "toggle_40x20_slide_4f.png"));
        }

        // Button press: 2 frames 120x36
        using (var sheet = new PixelCanvas(120 * 2, 36, p.BgPrimary))
        {
            using var up = new PixelCanvas(120, 36, p.BgPrimary);
            RenderRectBevel(up, 0, 0, 120, 36, p.BgRaised, p.BorderOuter, p.HighlightTop, p.ShadowBottom);
            using var down = new PixelCanvas(120, 36, p.BgPrimary);
            RenderRectBevel(down, 0, 0, 120, 36, p.GraphitePressed, p.BorderOuter, p.HighlightTop, p.ShadowBottom);
            // shift down 1px
            using var downShifted = new PixelCanvas(120, 36, p.BgPrimary);
            for (var y = 0; y < 35; y++)
            for (var x = 0; x < 120; x++)
                downShifted.Image[x, y + 1] = down.Image[x, y];

            Blit(up, sheet, 0, 0);
            Blit(downShifted, sheet, 120, 0);
            sheet.SavePng(Path.Combine(pngDir, "button_primary_120x36_press_2f.png"));
        }

        // Meter decay: 8 frames 24x220 (simple falloff)
        using (var sheet = new PixelCanvas(24 * 8, 220, p.BgPrimary))
        {
            for (var i = 0; i < 8; i++)
            {
                using var frame = new PixelCanvas(24, 220, p.BgPrimary);
                RenderRectBevel(frame, 0, 0, 24, 220, p.BgPanel, p.BorderOuter, p.HighlightTop, p.ShadowBottom);

                var level = 1f - (i / 7f);
                var fillH = (int)Math.Round(level * 214);
                for (var y = 218; y >= 4 && (218 - y) < fillH; y--)
                {
                    var t = (218 - y) / 214f;
                    var col = t < 0.33f ? p.MeterLow : t < 0.66f ? p.MeterMid : p.MeterHigh;
                    for (var x = 4; x < 20; x++)
                        frame.SetPixel(x, y, col);
                }

                Blit(frame, sheet, i * 24, 0);
            }
            sheet.SavePng(Path.Combine(pngDir, "meter_v_24x220_decay_8f.png"));
        }
    }

    private static void Blit(PixelCanvas src, PixelCanvas dst, int dx, int dy)
    {
        for (var y = 0; y < src.Height; y++)
        for (var x = 0; x < src.Width; x++)
            dst.SetPixel(dx + x, dy + y, src.Image[x, y]);
    }

    private static void ApplyDisabled(PixelCanvas c, Palette p)
    {
        // Simple: mix toward BgPrimary and reduce alpha.
        for (var y = 0; y < c.Height; y++)
        for (var x = 0; x < c.Width; x++)
        {
            var px = c.Image[x, y];
            // If pixel is background, leave it.
            if (px.Equals(p.BgPrimary)) continue;
            // desaturate
            var gray = (byte)Math.Clamp((int)Math.Round(px.R * 0.3 + px.G * 0.59 + px.B * 0.11), 0, 255);
            var desat = new Rgba32(gray, gray, gray, (byte)(px.A * 0.4));
            c.Image[x, y] = desat;
        }
    }

    private static void RenderRectBevel(PixelCanvas c, int x, int y, int w, int h, Rgba32 fill, Rgba32 border, Rgba32 highlight, Rgba32 shadow)
    {
        c.FillRect(x, y, w, h, fill);
        // border
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
        // bevel
        c.FillRect(x + 1, y + 1, w - 2, 1, highlight);
        c.FillRect(x + 1, y + h - 2, w - 2, 1, shadow);
    }

    private static void RenderCard(PixelCanvas c, Palette p, int radius)
    {
        // Fill with checkerboard Graphite1/Graphite2, then 2px border.
        for (var y = 0; y < c.Height; y++)
        for (var x = 0; x < c.Width; x++)
        {
            c.Image[x, y] = Dither.Checker50(x, y) ? p.BgPanel : p.BgRaised;
        }

        // 2px border outer + inner
        for (var x = 0; x < c.Width; x++)
        {
            c.SetPixel(x, 0, p.BorderOuter);
            c.SetPixel(x, 1, p.BorderInner);
            c.SetPixel(x, c.Height - 1, p.BorderOuter);
            c.SetPixel(x, c.Height - 2, p.BorderInner);
        }
        for (var y = 0; y < c.Height; y++)
        {
            c.SetPixel(0, y, p.BorderOuter);
            c.SetPixel(1, y, p.BorderInner);
            c.SetPixel(c.Width - 1, y, p.BorderOuter);
            c.SetPixel(c.Width - 2, y, p.BorderInner);
        }

        // Pixel-stepped corners (radius): carve corners by setting to BgPrimary.
        CarveSteppedCorner(c, p.BgPrimary, 0, 0, radius, topLeft: true);
        CarveSteppedCorner(c, p.BgPrimary, c.Width - 1, 0, radius, topLeft: false);
        CarveSteppedCorner(c, p.BgPrimary, 0, c.Height - 1, radius, bottomLeft: true);
        CarveSteppedCorner(c, p.BgPrimary, c.Width - 1, c.Height - 1, radius, bottomLeft: false);
    }

    private static void CarveSteppedCorner(PixelCanvas c, Rgba32 bg, int cornerX, int cornerY, int r, bool topLeft = false, bool bottomLeft = false)
    {
        // crude step mask for r=8 corners
        for (var dy = 0; dy < r; dy++)
        {
            var cut = r - dy;
            for (var dx = 0; dx < cut; dx++)
            {
                var x = cornerX + (topLeft || bottomLeft ? dx : -dx);
                var y = cornerY + (bottomLeft ? -dy : dy);
                c.SetPixel(x, y, bg);
            }
        }
    }

    private static void RenderRoundedRect(PixelCanvas c, int x, int y, int w, int h, int radius, Rgba32 fill, Rgba32 border)
    {
        c.FillRect(x, y, w, h, fill);
        // border box
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
        // corners: carve outside pixels to background (assumes bg already is canvas bg)
        // We'll keep it simple by not carving; PNG still reads rounded because thumb overlays.
    }

    private static void RenderCircle(PixelCanvas c, int cx, int cy, int radius, Rgba32 fill, Rgba32 border)
    {
        Draw.CircleFill(c, cx, cy, radius, fill);
        Draw.CircleRing(c, cx, cy, radius, 1, border);
    }
}
