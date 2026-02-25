using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Cm6206DualRouter;

internal enum PillState
{
    Unknown,
    Ok,
    Warning,
    Error
}

internal sealed class StatusPill : Control
{
    public PillState State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            _state = value;
            Invalidate();
        }
    }
    private PillState _state = PillState.Unknown;

    public StatusPill()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        Height = 28;
        Width = 160;
        ForeColor = NeonTheme.TextPrimary;
        BackColor = NeonTheme.BgRaised;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = ClientRectangle;
        rect.Inflate(-1, -1);

        using var path = RoundedRect(rect, rect.Height / 2);

        var accent = State switch
        {
            PillState.Ok => NeonTheme.NeonCyan,
            PillState.Warning => NeonTheme.NeonAmber,
            PillState.Error => NeonTheme.MeterClip,
            _ => NeonTheme.TextSecondary
        };

        using (var bg = new SolidBrush(BackColor))
            e.Graphics.FillPath(bg, path);

        // border
        using (var border = new Pen(Color.FromArgb(90, 255, 255, 255), 1f))
            e.Graphics.DrawPath(border, path);

        // dot
        var dot = new Rectangle(rect.Left + 10, rect.Top + (rect.Height / 2) - 4, 8, 8);
        using (var dotBrush = new SolidBrush(accent))
            e.Graphics.FillEllipse(dotBrush, dot);

        // text
        var textRect = rect;
        textRect.Inflate(-10, 0);
        textRect.X += 16;
        using var textBrush = new SolidBrush(ForeColor);
        var fmt = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString(Text, Font, textBrush, textRect, fmt);
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
