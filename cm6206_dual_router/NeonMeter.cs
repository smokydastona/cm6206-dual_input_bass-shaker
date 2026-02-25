using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Cm6206DualRouter;

internal sealed class NeonMeter : Control
{
    private float _target;
    private bool _clip;

    public NeonMeter()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Width = 18;
        Height = 120;
    }

    public bool Vertical { get; set; } = true;

    // 0..1
    public float Peak
    {
        get => _target;
        set
        {
            var v = Math.Clamp(value, 0f, 1f);
            _target = v;
            _clip = v >= 0.999f;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.None;

        var rect = ClientRectangle;
        rect.Inflate(-1, -1);

        // Asset-backed background (preferred when size matches known exports)
        string? bgName = null;
        if (Vertical)
        {
            if (rect.Width == 24 && rect.Height == 220) bgName = "meter_v_24x220_bg.png";
            else if (rect.Width == 12 && rect.Height == 120) bgName = "meter_v_12x120_bg.png";
        }
        else
        {
            if (rect.Width == 80 && rect.Height == 12) bgName = "meter_gr_80x12_bg.png";
        }

        var bgImg = bgName is null ? null : AaaAssets.TryGetPng(bgName);
        if (bgImg is not null)
        {
            AaaAssets.DrawNearestNeighbor(e.Graphics, bgImg, rect);
        }
        else
        {
            // Fallback background
            using (var bg = new SolidBrush(NeonTheme.BgRaised))
                e.Graphics.FillRectangle(bg, rect);

            // Border (2px)
            using (var border = new Pen(Color.FromArgb(110, 255, 255, 255), 2f))
                e.Graphics.DrawRectangle(border, rect);
        }

        var fillRect = rect;
        if (Vertical)
        {
            var h = (int)Math.Round(rect.Height * _target);
            fillRect = new Rectangle(rect.Left, rect.Bottom - h, rect.Width, h);
        }
        else
        {
            var w = (int)Math.Round(rect.Width * _target);
            fillRect = new Rectangle(rect.Left, rect.Top, w, rect.Height);
        }

        if (fillRect.Width > 0 && fillRect.Height > 0)
            FillMeterPixels(e.Graphics, rect, fillRect, Vertical);

        // Clip (red border)
        if (_clip)
        {
            using var pen = new Pen(Color.FromArgb(210, NeonTheme.MeterClip), 3f);
            e.Graphics.DrawRectangle(pen, rect);
        }
    }

    private static void FillMeterPixels(Graphics g, Rectangle fullRect, Rectangle fillRect, bool vertical)
    {
        // Crisp 3-band fill (low/mid/high) to fit pixel aesthetic.
        if (vertical)
        {
            var yHighEnd = fullRect.Top + (int)Math.Round(fullRect.Height * 0.33f);
            var yMidEnd = fullRect.Top + (int)Math.Round(fullRect.Height * 0.66f);

            using var high = new SolidBrush(NeonTheme.MeterHigh);
            using var mid = new SolidBrush(NeonTheme.MeterMid);
            using var low = new SolidBrush(NeonTheme.MeterLow);

            var rHigh = Rectangle.FromLTRB(fillRect.Left, fillRect.Top, fillRect.Right, Math.Min(fillRect.Bottom, yHighEnd));
            var rMid = Rectangle.FromLTRB(fillRect.Left, Math.Max(fillRect.Top, yHighEnd), fillRect.Right, Math.Min(fillRect.Bottom, yMidEnd));
            var rLow = Rectangle.FromLTRB(fillRect.Left, Math.Max(fillRect.Top, yMidEnd), fillRect.Right, fillRect.Bottom);

            if (rHigh.Height > 0) g.FillRectangle(high, rHigh);
            if (rMid.Height > 0) g.FillRectangle(mid, rMid);
            if (rLow.Height > 0) g.FillRectangle(low, rLow);
        }
        else
        {
            var xLowEnd = fullRect.Left + (int)Math.Round(fullRect.Width * 0.33f);
            var xMidEnd = fullRect.Left + (int)Math.Round(fullRect.Width * 0.66f);

            using var low = new SolidBrush(NeonTheme.MeterLow);
            using var mid = new SolidBrush(NeonTheme.MeterMid);
            using var high = new SolidBrush(NeonTheme.MeterHigh);

            var rLow = Rectangle.FromLTRB(fillRect.Left, fillRect.Top, Math.Min(fillRect.Right, xLowEnd), fillRect.Bottom);
            var rMid = Rectangle.FromLTRB(Math.Max(fillRect.Left, xLowEnd), fillRect.Top, Math.Min(fillRect.Right, xMidEnd), fillRect.Bottom);
            var rHigh = Rectangle.FromLTRB(Math.Max(fillRect.Left, xMidEnd), fillRect.Top, fillRect.Right, fillRect.Bottom);

            if (rLow.Width > 0) g.FillRectangle(low, rLow);
            if (rMid.Width > 0) g.FillRectangle(mid, rMid);
            if (rHigh.Width > 0) g.FillRectangle(high, rHigh);
        }
    }

    private static LinearGradientBrush CreateMeterGradient(Rectangle rect, bool vertical)
    {
        // cyan -> purple -> amber
        var brush = new LinearGradientBrush(
            rect,
            NeonTheme.MeterLow,
            NeonTheme.MeterHigh,
            vertical ? LinearGradientMode.Vertical : LinearGradientMode.Horizontal);
        var blend = new ColorBlend
        {
            Positions = new[] { 0f, 0.6f, 1f },
            Colors = vertical
                ? new[] { NeonTheme.MeterHigh, NeonTheme.MeterMid, NeonTheme.MeterLow }
                : new[] { NeonTheme.MeterLow, NeonTheme.MeterMid, NeonTheme.MeterHigh }
        };
        brush.InterpolationColors = blend;
        return brush;
    }
}
