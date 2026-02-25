using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Cm6206DualRouter;

internal sealed class NeonToggleButton : Control
{
    private bool _hover;
    private bool _pressed;
    private float _hoverT;

    private readonly System.Windows.Forms.Timer _anim = new();

    public NeonToggleButton()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        Width = 34;
        Height = 22;
        Cursor = Cursors.Hand;

        Font = NeonTheme.CreateBaseFont(13, FontStyle.Bold);

        _anim.Interval = 16;
        _anim.Tick += (_, _) =>
        {
            var target = _hover ? 1f : 0f;
            var step = 0.12f; // ~120ms
            if (_hoverT < target) _hoverT = Math.Min(target, _hoverT + step);
            if (_hoverT > target) _hoverT = Math.Max(target, _hoverT - step);
            Invalidate();
        };
        _anim.Start();
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            CheckedChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }
    private bool _checked;

    public event EventHandler? CheckedChanged;

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        _pressed = false;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _pressed = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        if (_pressed)
        {
            _pressed = false;
            Checked = !Checked;
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = ClientRectangle;
        rect.Inflate(-1, -1);
        if (_pressed) rect.Offset(0, 2); // depress

        using var path = RoundedRect(rect, 6);

        var idle = NeonTheme.BgRaised;
        var activeFill = Color.FromArgb(90, NeonTheme.NeonCyan);

        using (var bg = new SolidBrush(Checked ? activeFill : idle))
            e.Graphics.FillPath(bg, path);

        // Hover outline: purple
        if (_hoverT > 0f)
        {
            using var pen = new Pen(Color.FromArgb((int)(180 * _hoverT), NeonTheme.NeonPurple), 2f);
            e.Graphics.DrawPath(pen, path);
        }

        // Active glow: purple
        if (Checked)
        {
            using var glow = new Pen(Color.FromArgb(140, NeonTheme.NeonPurple), 2f);
            e.Graphics.DrawPath(glow, path);
        }

        // Border
        using (var border = new Pen(Color.FromArgb(90, 255, 255, 255), 1f))
            e.Graphics.DrawPath(border, path);

        // Text
        var textColor = Checked ? NeonTheme.TextPrimary : NeonTheme.TextSecondary;
        using var brush = new SolidBrush(textColor);
        var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString(Text, Font, brush, rect, fmt);
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
