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
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

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
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = ClientRectangle;
        rect.Inflate(-1, -1);

        // Background
        using (var bg = new SolidBrush(NeonTheme.BgRaised))
            e.Graphics.FillRectangle(bg, rect);

        // Border (2px)
        using (var border = new Pen(Color.FromArgb(110, 255, 255, 255), 2f))
            e.Graphics.DrawRectangle(border, rect);

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
        {
            using var grad = CreateMeterGradient(fillRect, Vertical);
            e.Graphics.FillRectangle(grad, fillRect);
        }

        // Clip (red border)
        if (_clip)
        {
            using var pen = new Pen(Color.FromArgb(210, NeonTheme.MeterClip), 3f);
            e.Graphics.DrawRectangle(pen, rect);
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
