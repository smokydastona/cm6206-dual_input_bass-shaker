using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Cm6206DualRouter;

internal sealed class NeonMeter : Control
{
    private float _target;
    private float _display;
    private bool _clip;
    private float _clipT;

    private readonly Timer _timer = new();

    public NeonMeter()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        BackColor = Color.Transparent;
        Width = 18;
        Height = 120;

        _timer.Interval = 16;
        _timer.Tick += (_, _) =>
        {
            // inertia
            var rise = 0.35f;
            var fall = 0.92f;
            if (_display < _target)
                _display = _display + (_target - _display) * rise;
            else
                _display *= fall;

            if (_clip)
                _clipT = Math.Min(1f, _clipT + 0.22f);
            else
                _clipT = Math.Max(0f, _clipT - 0.12f);

            Invalidate();
        };
        _timer.Start();
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
            var h = (int)Math.Round(rect.Height * _display);
            fillRect = new Rectangle(rect.Left, rect.Bottom - h, rect.Width, h);
        }
        else
        {
            var w = (int)Math.Round(rect.Width * _display);
            fillRect = new Rectangle(rect.Left, rect.Top, w, rect.Height);
        }

        if (fillRect.Width > 0 && fillRect.Height > 0)
        {
            using var grad = CreateMeterGradient(fillRect);
            e.Graphics.FillRectangle(grad, fillRect);
        }

        // Clip pulse (red)
        if (_clipT > 0f)
        {
            var a = (int)(120 * _clipT);
            using var pen = new Pen(Color.FromArgb(a, NeonTheme.MeterClip), 3f);
            e.Graphics.DrawRectangle(pen, rect);
        }
    }

    private static LinearGradientBrush CreateMeterGradient(Rectangle rect)
    {
        // cyan -> purple -> amber
        var brush = new LinearGradientBrush(rect, NeonTheme.MeterLow, NeonTheme.MeterHigh, LinearGradientMode.Vertical);
        var blend = new ColorBlend
        {
            Positions = new[] { 0f, 0.6f, 1f },
            Colors = new[] { NeonTheme.MeterHigh, NeonTheme.MeterMid, NeonTheme.MeterLow }
        };
        brush.InterpolationColors = blend;
        return brush;
    }
}
