using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Cm6206DualRouter;

internal sealed class NeonToggleButton : Control
{
    private bool _hover;
    private bool _pressed;
    private float _hoverT;

    public NeonToggleButton()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        Width = 34;
        Height = 22;
        Cursor = Cursors.Hand;
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
        _hoverT = 1f;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        _pressed = false;
        _hoverT = 0f;
        Invalidate();
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
        var rect = ClientRectangle;
        rect.Inflate(-1, -1);
        if (rect.Width <= 1 || rect.Height <= 1) return;

        // Asset-backed path (preferred)
        var state = !Enabled ? "disabled" : _pressed ? "pressed" : _hover ? "hover" : "default";
        var img = AaaAssets.TryGetPng($"button_primary_120x36_{state}.png");
        if (img is not null)
        {
            // Slight depress effect: shift content down a pixel.
            if (_pressed) rect.Offset(0, 1);

            AaaAssets.DrawNearestNeighbor(e.Graphics, img, rect);

            if (Checked)
            {
                var glowRect = rect;
                glowRect.Inflate(-2, -2);
                using var overlay = new SolidBrush(Color.FromArgb(40, NeonTheme.NeonCyan));
                e.Graphics.FillRectangle(overlay, glowRect);
            }

            // Text on top
            var textColor = Checked ? NeonTheme.TextPrimary : NeonTheme.TextSecondary;
            using var brush = new SolidBrush(textColor);
            var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(Text, Font, brush, rect, fmt);
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        rect = ClientRectangle;
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
