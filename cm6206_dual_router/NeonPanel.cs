using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Cm6206DualRouter;

internal sealed class NeonPanel : Panel
{
    public NeonPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Padding = new Padding(12);
    }

    public bool NoiseOverlay { get; set; } = true;

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // prevent default background paint; we paint everything in OnPaint.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = ClientRectangle;
        if (rect.Width <= 1 || rect.Height <= 1)
            return;

        rect.Inflate(-1, -1);

        using var path = RoundedRect(rect, 8);

        // Soft shadow
        using (var shadowBrush = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
        {
            var shadowRect = rect;
            shadowRect.Offset(0, 2);
            using var shadowPath = RoundedRect(shadowRect, 8);
            e.Graphics.FillPath(shadowBrush, shadowPath);
        }

        // Subtle inner gradient
        using (var grad = new LinearGradientBrush(rect, NeonTheme.BgRaised, NeonTheme.BgPanel, LinearGradientMode.Vertical))
        {
            e.Graphics.FillPath(grad, path);
        }

        // Electric bevel edges: cyan top-left, purple bottom-right
        using (var penC = new Pen(Color.FromArgb(180, NeonTheme.NeonCyan), 1f))
        using (var penP = new Pen(Color.FromArgb(160, NeonTheme.NeonPurple), 1f))
        {
            e.Graphics.DrawLine(penC, rect.Left + 6, rect.Top + 1, rect.Right - 14, rect.Top + 1);
            e.Graphics.DrawLine(penC, rect.Left + 1, rect.Top + 6, rect.Left + 1, rect.Bottom - 14);

            e.Graphics.DrawLine(penP, rect.Left + 14, rect.Bottom - 1, rect.Right - 6, rect.Bottom - 1);
            e.Graphics.DrawLine(penP, rect.Right - 1, rect.Top + 14, rect.Right - 1, rect.Bottom - 6);
        }

        // Thin border
        using (var border = new Pen(Color.FromArgb(90, 255, 255, 255), 1f))
        {
            e.Graphics.DrawPath(border, path);
        }

        if (NoiseOverlay)
        {
            DrawNoise(e.Graphics, rect, alpha: 9); // ~3.5%
        }

        base.OnPaint(e);
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

    private static void DrawNoise(Graphics g, Rectangle rect, int alpha)
    {
        // Deterministic noise for a premium tactile feel.
        // Keep very cheap: sample a coarse grid.
        using var b = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255));
        var seed = rect.Width * 73856093 ^ rect.Height * 19349663;
        var rng = new Random(seed);
        for (var y = rect.Top; y < rect.Bottom; y += 6)
        {
            for (var x = rect.Left; x < rect.Right; x += 6)
            {
                if (rng.NextDouble() < 0.35)
                    g.FillRectangle(b, x, y, 1, 1);
            }
        }
    }
}
