using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Cm6206DualRouter;

internal sealed class SignalFlowControl : Control
{
    public bool RouterRunning { get; set; }
    public bool OutputOk { get; set; } = true;

    public bool GameAudioDetected { get; set; }
    public bool SecondaryAudioDetected { get; set; }

    public bool SpeakersEnabled { get; set; } = true;
    public bool ShakerEnabled { get; set; } = true;

    public string OutputDeviceDisplay { get; set; } = "Output";

    public SignalFlowControl()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 86;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = ClientRectangle;
        rect.Inflate(-1, -1);

        using (var bg = new SolidBrush(NeonTheme.BgPanel))
            e.Graphics.FillRectangle(bg, rect);

        using (var border = new Pen(Color.FromArgb(90, 255, 255, 255), 1f))
            e.Graphics.DrawRectangle(border, rect);

        var pad = 10;
        var leftW = 150;
        var midW = 110;
        var rightW = Math.Max(180, rect.Width - (pad * 2 + leftW + midW + 170));

        var leftTop = new Rectangle(rect.Left + pad, rect.Top + pad, leftW, 26);
        var leftBot = new Rectangle(rect.Left + pad, rect.Top + pad + 34, leftW, 26);
        var mid = new Rectangle(leftTop.Right + 18, rect.Top + pad + 17, midW, 26);
        var rightTop = new Rectangle(mid.Right + 18, rect.Top + pad, rightW, 26);
        var rightBot = new Rectangle(mid.Right + 18, rect.Top + pad + 34, 160, 26);

        var cSource = ColorTranslator.FromHtml("#3AA8FF"); // meaning blue
        var cOk = ColorTranslator.FromHtml("#00E676");     // meaning green
        var cWarn = ColorTranslator.FromHtml("#FFB000");   // meaning orange
        var cErr = ColorTranslator.FromHtml("#FF3B3B");    // meaning red

        var gameState = RouterRunning ? (GameAudioDetected ? cOk : cWarn) : NeonTheme.TextMuted;
        var secState = RouterRunning ? (SecondaryAudioDetected ? cOk : NeonTheme.TextMuted) : NeonTheme.TextMuted;

        var outputState = OutputOk ? (RouterRunning ? cOk : NeonTheme.TextMuted) : cErr;
        var speakerState = SpeakersEnabled ? outputState : cErr;
        var shakerState = ShakerEnabled ? outputState : cErr;

        DrawNode(e.Graphics, leftTop, "Game Audio", "Capture system audio", cSource, gameState);
        DrawNode(e.Graphics, leftBot, "Music / Secondary", "Optional", cSource, secState);
        DrawNode(e.Graphics, mid, "Mixer", "", NeonTheme.NeonPurple, RouterRunning ? cOk : NeonTheme.TextMuted);
        DrawNode(e.Graphics, rightTop, OutputDeviceDisplay, "Audio device", NeonTheme.TextPrimary, outputState);
        DrawNode(e.Graphics, rightBot, "Bass Shaker", "Bass / Shaker (LFE)", NeonTheme.TextPrimary, shakerState);

        // Merge lines
        var join = new Point(mid.Left - 10, mid.Top + mid.Height / 2);
        DrawArrow(e.Graphics, new Point(leftTop.Right + 6, leftTop.Top + leftTop.Height / 2), join, RouterRunning ? cOk : NeonTheme.TextMuted);
        DrawArrow(e.Graphics, new Point(leftBot.Right + 6, leftBot.Top + leftBot.Height / 2), join, RouterRunning ? cOk : NeonTheme.TextMuted);

        DrawArrow(e.Graphics, new Point(join.X, join.Y), new Point(mid.Left, mid.Top + mid.Height / 2), RouterRunning ? cOk : NeonTheme.TextMuted);

        // Split lines
        DrawArrow(e.Graphics, new Point(mid.Right, mid.Top + mid.Height / 2), new Point(rightTop.Left - 6, rightTop.Top + rightTop.Height / 2), speakerState);
        DrawArrow(e.Graphics, new Point(mid.Right, mid.Top + mid.Height / 2), new Point(rightBot.Left - 6, rightBot.Top + rightBot.Height / 2), shakerState);

        // Tiny status text
        var status = RouterRunning ? "Routing active" : "Routing stopped";
        var statusColor = OutputOk ? (RouterRunning ? cOk : NeonTheme.TextMuted) : cErr;
        using var statusBrush = new SolidBrush(statusColor);
        e.Graphics.DrawString(status, NeonTheme.CreateMonoFont(10), statusBrush, rect.Left + pad, rect.Bottom - pad - 14);
    }

    private static void DrawNode(Graphics g, Rectangle r, string title, string subtitle, Color titleColor, Color stateColor)
    {
        using var path = RoundedRect(r, 6);
        using (var bg = new SolidBrush(NeonTheme.BgRaised))
            g.FillPath(bg, path);

        using (var glow = new Pen(Color.FromArgb(90, stateColor), 2f))
            g.DrawPath(glow, path);

        using (var border = new Pen(Color.FromArgb(80, 255, 255, 255), 1f))
            g.DrawPath(border, path);

        using var tBrush = new SolidBrush(titleColor);
        using var sBrush = new SolidBrush(NeonTheme.TextMuted);

        var tRect = new Rectangle(r.Left + 8, r.Top + 4, r.Width - 16, 14);
        var sRect = new Rectangle(r.Left + 8, r.Top + 18, r.Width - 16, 12);
        g.DrawString(title, NeonTheme.CreateBaseFont(10, FontStyle.Bold), tBrush, tRect);
        if (!string.IsNullOrWhiteSpace(subtitle))
            g.DrawString(subtitle, NeonTheme.CreateMonoFont(8), sBrush, sRect);
    }

    private static void DrawArrow(Graphics g, Point a, Point b, Color color)
    {
        using var pen = new Pen(Color.FromArgb(180, color), 2f);
        pen.CustomEndCap = new AdjustableArrowCap(4, 5);
        g.DrawLine(pen, a, b);
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
