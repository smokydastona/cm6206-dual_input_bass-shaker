using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Cm6206DualRouter;

internal sealed class NeonSlider : Control
{
    private int _minimum;
    private int _maximum = 100;
    private int _value;

    private bool _hover;
    private bool _drag;
    private float _hoverT;

    private readonly System.Windows.Forms.Timer _anim = new();
    private bool _animStarted;

    public NeonSlider()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        Height = 22;
        MinimumSize = new Size(120, 22);
        BackColor = Color.Transparent;

        _anim.Interval = 16;
        _anim.Tick += (_, _) =>
        {
            var target = _hover ? 1f : 0f;
            var step = 0.12f; // ~120ms
            if (_hoverT < target) _hoverT = Math.Min(target, _hoverT + step);
            if (_hoverT > target) _hoverT = Math.Max(target, _hoverT - step);
            Invalidate();
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        StartAnimIfNeeded();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        StopAnimIfNeeded();
        base.OnHandleDestroyed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopAnimIfNeeded();
            _anim.Dispose();
        }

        base.Dispose(disposing);
    }

    private void StartAnimIfNeeded()
    {
        if (_animStarted) return;
        _animStarted = true;
        _anim.Start();
    }

    private void StopAnimIfNeeded()
    {
        if (!_animStarted) return;
        _anim.Stop();
        _animStarted = false;
    }

    public int Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_maximum < _minimum) _maximum = _minimum;
            Value = _value;
            Invalidate();
        }
    }

    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(value, _minimum);
            Value = _value;
            Invalidate();
        }
    }

    public int Value
    {
        get => _value;
        set
        {
            var v = Math.Clamp(value, Minimum, Maximum);
            if (_value == v) return;
            _value = v;
            ValueChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    public event EventHandler? ValueChanged;

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        _drag = false;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _drag = true;
        Capture = true;
        SetFromMouse(e.Location);
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
        if (_drag) SetFromMouse(e.Location);
    }

    private void SetFromMouse(Point p)
    {
        var rect = ClientRectangle;
        rect.Inflate(-10, 0);
        var track = new Rectangle(rect.Left + 10, rect.Top + (rect.Height / 2) - 3, rect.Width - 20, 6);
        if (track.Width <= 1) return;

        var t = (p.X - track.Left) / (float)track.Width;
        t = Math.Clamp(t, 0f, 1f);
        var v = Minimum + (int)Math.Round(t * (Maximum - Minimum));
        Value = v;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = ClientRectangle;
        rect.Inflate(-1, -1);

        var track = new Rectangle(rect.Left + 10, rect.Top + (rect.Height / 2) - 3, rect.Width - 20, 6);
        if (track.Width <= 1) return;

        var t = (Value - Minimum) / (float)Math.Max(1, Maximum - Minimum);
        t = Math.Clamp(t, 0f, 1f);

        var fill = track;
        fill.Width = (int)Math.Round(track.Width * t);

        // Track
        using (var trackBrush = new SolidBrush(ColorTranslator.FromHtml("#1A1E26")))
            e.Graphics.FillRectangle(trackBrush, track);

        // Filled portion (electric cyan)
        if (fill.Width > 0)
        {
            using var fillBrush = new SolidBrush(NeonTheme.NeonCyan);
            e.Graphics.FillRectangle(fillBrush, fill);
        }

        // Hover outline
        if (_hoverT > 0f)
        {
            using var pen = new Pen(Color.FromArgb((int)(120 * _hoverT), NeonTheme.NeonPurple), 2f);
            e.Graphics.DrawRoundedRectangle(pen, track, 3);
        }

        // Handle: 12px circle, cyan ring + faint glow
        var hx = track.Left + (int)Math.Round(track.Width * t);
        var handle = new Rectangle(hx - 6, track.Top + (track.Height / 2) - 6, 12, 12);

        using (var glow = new SolidBrush(Color.FromArgb((int)(60 + 70 * _hoverT), NeonTheme.NeonCyan)))
        {
            var glowRect = handle;
            glowRect.Inflate(5, 5);
            e.Graphics.FillEllipse(glow, glowRect);
        }

        using (var inner = new SolidBrush(NeonTheme.BgPanel))
            e.Graphics.FillEllipse(inner, handle);

        using (var ring = new Pen(Color.FromArgb(220, NeonTheme.NeonCyan), 2f))
            e.Graphics.DrawEllipse(ring, handle);

        // Border
        using var border = new Pen(Color.FromArgb(90, 255, 255, 255), 1f);
        e.Graphics.DrawRectangle(border, track);
    }
}

internal static class GraphicsExtensions
{
    public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle rect, int radius)
    {
        using var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.DrawPath(pen, path);
    }
}
