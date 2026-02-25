using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Cm6206DualRouter;

internal sealed class NeonKnob : Control
{
    private float _min = 0f;
    private float _max = 1f;
    private float _value;

    private bool _drag;
    private int _dragStartY;
    private float _dragStartValue;
    private bool _hover;

    public float Minimum
    {
        get => _min;
        set
        {
            _min = value;
            if (_max < _min) _max = _min;
            Value = _value;
        }
    }

    public float Maximum
    {
        get => _max;
        set
        {
            _max = Math.Max(value, _min);
            Value = _value;
        }
    }

    public float Value
    {
        get => _value;
        set
        {
            var v = Math.Clamp(value, Minimum, Maximum);
            if (Math.Abs(_value - v) < 0.0001f) return;
            _value = v;
            ValueChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    public event EventHandler? ValueChanged;

    public NeonKnob()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor,
            true);

        Width = 64;
        Height = 64;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _drag = true;
        _dragStartY = e.Y;
        _dragStartValue = Value;
        Capture = true;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        _drag = false;
        Capture = false;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_drag) return;

        // Vertical drag: up increases value.
        var dy = _dragStartY - e.Y;
        var range = Math.Max(1e-6f, Maximum - Minimum);

        // 120px for full sweep by default.
        var delta = (dy / 120f) * range;
        Value = _dragStartValue + delta;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // Asset-backed path (preferred)
        var rect = ClientRectangle;
        if (rect.Width > 0 && rect.Height > 0)
        {
            // Map value -> sprite frame
            var assetT = (Value - Minimum) / Math.Max(1e-6f, (Maximum - Minimum));
            assetT = Math.Clamp(assetT, 0f, 1f);

            const int frames = 16;
            var frame = (int)Math.Round(assetT * (frames - 1));
            frame = Math.Clamp(frame, 0, frames - 1);

            var sprite = AaaAssets.TryGetPng("knob_primary_64_rotate_16f.png");
            if (sprite is not null && sprite.Width >= 64 * frames && sprite.Height >= 64)
            {
                var src = AaaAssets.SpriteFrame(frame, 64, 64);
                AaaAssets.DrawNearestNeighbor(e.Graphics, sprite, rect, src);

                // Small hover/active hint: keep it subtle to avoid fighting the sprite.
                if (!Enabled)
                {
                    using var overlay = new SolidBrush(Color.FromArgb(120, NeonTheme.BgPrimary));
                    e.Graphics.FillRectangle(overlay, rect);
                }
                else if (_drag)
                {
                    using var overlay = new SolidBrush(Color.FromArgb(30, NeonTheme.NeonCyan));
                    e.Graphics.FillRectangle(overlay, rect);
                }
                else if (_hover)
                {
                    using var overlay = new SolidBrush(Color.FromArgb(20, NeonTheme.NeonPurple));
                    e.Graphics.FillRectangle(overlay, rect);
                }

                return;
            }

            // Fallback to static state images if the sprite sheet isn't present.
            var state = !Enabled ? "disabled" : _drag ? "active" : _hover ? "hover" : "default";
            var stateImg = AaaAssets.TryGetPng($"knob_primary_64_{state}.png");
            if (stateImg is not null)
            {
                AaaAssets.DrawNearestNeighbor(e.Graphics, stateImg, rect);
                return;
            }
        }

        // Vector fallback (dev-safe)
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        rect = ClientRectangle;
        rect.Inflate(-1, -1);

        var cx = rect.Left + rect.Width / 2f;
        var cy = rect.Top + rect.Height / 2f;

        var outerR = Math.Min(rect.Width, rect.Height) / 2f - 2f;
        var innerR = outerR - 12f;

        // Background outer
        using (var bg = new SolidBrush(NeonTheme.BgRaised))
            e.Graphics.FillEllipse(bg, cx - outerR, cy - outerR, outerR * 2, outerR * 2);

        // Arc
        var arcT = (Value - Minimum) / Math.Max(1e-6f, (Maximum - Minimum));
        arcT = Math.Clamp(arcT, 0f, 1f);

        var startAngle = 135f;
        var sweep = 270f;
        var sweepAngle = sweep * arcT;

        using (var arcPen = new Pen(NeonTheme.NeonCyan, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            var arcRect = new RectangleF(cx - outerR + 6, cy - outerR + 6, (outerR - 6) * 2, (outerR - 6) * 2);
            e.Graphics.DrawArc(arcPen, arcRect, startAngle, sweepAngle);
        }

        // Inner circle
        using (var inner = new SolidBrush(NeonTheme.BgPanel))
            e.Graphics.FillEllipse(inner, cx - innerR, cy - innerR, innerR * 2, innerR * 2);

        // Indicator dot
        var angleDeg = startAngle + sweepAngle;
        var angle = angleDeg * (float)(Math.PI / 180.0);
        var dotR = 3f;
        var dotOrbit = innerR - 4f;
        var dx = (float)Math.Cos(angle) * dotOrbit;
        var dy = (float)Math.Sin(angle) * dotOrbit;
        using (var dot = new SolidBrush(NeonTheme.NeonPurple))
            e.Graphics.FillEllipse(dot, cx + dx - dotR, cy + dy - dotR, dotR * 2, dotR * 2);

        // Border
        using var border = new Pen(Color.FromArgb(90, 255, 255, 255), 1f);
        e.Graphics.DrawEllipse(border, cx - outerR, cy - outerR, outerR * 2, outerR * 2);
    }
}
