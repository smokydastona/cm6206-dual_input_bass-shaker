using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Cm6206DualRouter;

internal sealed class NeonMatrixControl : Control
{
    private readonly bool[,] _cells;
    private Point? _hover;
    private Point? _selected;

    private float _hoverT;
    private float _selectT;

    private readonly System.Windows.Forms.Timer _anim = new();
    private bool _animStarted;

    public NeonMatrixControl(int rows = 6, int cols = 2)
    {
        Rows = rows;
        Cols = cols;
        _cells = new bool[rows, cols];

        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        Font = NeonTheme.CreateBaseFont(13);
        BackColor = Color.Transparent;

        _anim.Interval = 16;
        _anim.Tick += (_, _) =>
        {
            var any = false;
            if (_hover is not null && _hoverT < 1f) { _hoverT = Math.Min(1f, _hoverT + 0.12f); any = true; }
            if (_hover is null && _hoverT > 0f) { _hoverT = Math.Max(0f, _hoverT - 0.12f); any = true; }

            if (_selected is not null && _selectT < 1f) { _selectT = Math.Min(1f, _selectT + 0.12f); any = true; }
            if (_selected is null && _selectT > 0f) { _selectT = Math.Max(0f, _selectT - 0.12f); any = true; }

            if (any) Invalidate();
        };

        Size = new Size(360, 240);
        MinimumSize = new Size(320, 220);

        RowLabels = new[] { "Front", "Center", "LFE", "Rear", "Side", "Reserved" };
        ColLabels = new[] { "A", "B" };
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

    public int Rows { get; }
    public int Cols { get; }

    public string[] RowLabels { get; set; }
    public string[] ColLabels { get; set; }

    public event EventHandler? CellsChanged;

    public bool Get(int row, int col) => _cells[row, col];

    public void Set(int row, int col, bool value)
    {
        if (_cells[row, col] == value) return;
        _cells[row, col] = value;
        CellsChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    public void SetAll(bool[,] values)
    {
        for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Cols; c++)
                _cells[r, c] = values[r, c];

        CellsChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = HitTest(e.Location);
        if (hit != _hover)
        {
            _hover = hit;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = null;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        var hit = HitTest(e.Location);
        if (hit is null) return;

        _selected = hit;
        var (r, c) = (hit.Value.X, hit.Value.Y);
        Set(r, c, !Get(r, c));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(NeonTheme.BgPanel);

        var rect = ClientRectangle;
        rect.Inflate(-10, -10);

        var headerH = 24;
        var labelW = 90;

        using var titleFont = NeonTheme.CreateBaseFont(13, FontStyle.Bold);
        using var labelBrush = new SolidBrush(NeonTheme.TextSecondary);

        // Column labels
        for (var c = 0; c < Cols; c++)
        {
            var x = rect.Left + labelW + c * (rect.Width - labelW) / Cols;
            e.Graphics.DrawString(ColLabels.ElementAtOrDefault(c) ?? "", titleFont, labelBrush, x + 6, rect.Top);
        }

        var gridTop = rect.Top + headerH;
        var gridH = rect.Height - headerH;
        var cellW = (rect.Width - labelW) / Cols;
        var cellH = gridH / Rows;

        for (var r = 0; r < Rows; r++)
        {
            e.Graphics.DrawString(RowLabels.ElementAtOrDefault(r) ?? "", Font, labelBrush, rect.Left, gridTop + r * cellH + 8);

            for (var c = 0; c < Cols; c++)
            {
                var cellRect = new Rectangle(rect.Left + labelW + c * cellW, gridTop + r * cellH, cellW - 10, cellH - 10);
                cellRect.Offset(4, 4);

                DrawCell(e.Graphics, cellRect, r, c);
            }
        }
    }

    private void DrawCell(Graphics g, Rectangle rect, int r, int c)
    {
        var isOn = _cells[r, c];
        var isHover = _hover is not null && _hover.Value.X == r && _hover.Value.Y == c;
        var isSel = _selected is not null && _selected.Value.X == r && _selected.Value.Y == c;

        using var path = RoundedRect(rect, 6);

        // Base
        using (var bg = new SolidBrush(NeonTheme.BgRaised))
            g.FillPath(bg, path);

        // Active fill
        if (isOn)
        {
            using var fill = new LinearGradientBrush(rect, Color.FromArgb(60, NeonTheme.NeonCyan), Color.FromArgb(20, NeonTheme.NeonPurple), LinearGradientMode.Vertical);
            g.FillPath(fill, path);
        }

        // Hover halo (soft purple)
        if (isHover || _hoverT > 0f)
        {
            var t = isHover ? _hoverT : 0f;
            using var halo = new Pen(Color.FromArgb((int)(t * 120), NeonTheme.NeonPurple), 2f);
            g.DrawPath(halo, path);
        }

        // Selected ring (cyan)
        if (isSel || _selectT > 0f)
        {
            var t = isSel ? _selectT : 0f;
            using var ring = new Pen(Color.FromArgb((int)(t * 200), NeonTheme.NeonCyan), 2f);
            g.DrawPath(ring, path);

            using var inner = new SolidBrush(Color.FromArgb((int)(t * 35), NeonTheme.NeonCyan));
            g.FillPath(inner, path);
        }

        // Border
        using var border = new Pen(Color.FromArgb(90, 255, 255, 255), 1f);
        g.DrawPath(border, path);

        // Tiny indicator dot
        if (isOn)
        {
            var dot = new Rectangle(rect.Right - 12, rect.Top + 6, 6, 6);
            using var b = new SolidBrush(NeonTheme.NeonCyan);
            g.FillEllipse(b, dot);
        }
    }

    private Point? HitTest(Point p)
    {
        var rect = ClientRectangle;
        rect.Inflate(-10, -10);
        var headerH = 24;
        var labelW = 90;

        var gridTop = rect.Top + headerH;
        var gridH = rect.Height - headerH;
        var cellW = (rect.Width - labelW) / Cols;
        var cellH = gridH / Rows;

        var gx = p.X - (rect.Left + labelW);
        var gy = p.Y - gridTop;
        if (gx < 0 || gy < 0) return null;

        var c = gx / cellW;
        var r = gy / cellH;
        if (r < 0 || r >= Rows || c < 0 || c >= Cols) return null;

        return new Point(r, c);
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
