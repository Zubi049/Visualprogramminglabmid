using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanArena;

/// <summary>
/// Draws the Ludo board and the 3D-looking tokens. A player clicks a highlighted token to move it.
/// Board coordinates are a 15x15 grid: x = column, y = row.
/// </summary>
public class LudoBoardControl : Control
{
    public static readonly Color[] Base =
    {
        Color.FromArgb(222, 52, 62),    // Red
        Color.FromArgb(40, 168, 84),    // Green
        Color.FromArgb(240, 190, 30),   // Yellow
        Color.FromArgb(50, 110, 222)    // Blue
    };
    public static readonly string[] ColorNames = { "Red", "Green", "Yellow", "Blue" };

    // The 52 squares of the outer ring as (row, col), clockwise. Index 1 is Red's start square.
    static readonly (int r, int c)[] Ring =
    {
        (6,0),(6,1),(6,2),(6,3),(6,4),(6,5),(5,6),(4,6),(3,6),(2,6),(1,6),(0,6),(0,7),(0,8),
        (1,8),(2,8),(3,8),(4,8),(5,8),(6,9),(6,10),(6,11),(6,12),(6,13),(6,14),(7,14),(8,14),
        (8,13),(8,12),(8,11),(8,10),(8,9),(9,8),(10,8),(11,8),(12,8),(13,8),(14,8),(14,7),(14,6),
        (13,6),(12,6),(11,6),(10,6),(9,6),(8,5),(8,4),(8,3),(8,2),(8,1),(8,0),(7,0)
    };

    // Top-left corner (row, col) of each 6x6 yard: Red bottom-left, Green top-left, Yellow top-right, Blue bottom-right.
    static readonly (int r, int c)[] YardOrigin = { (9, 0), (0, 0), (0, 9), (9, 9) };
    static readonly int[] Stars = { 9, 22, 35, 48 };   // safe squares that are not start squares

    static readonly int[,] RingAt = new int[15, 15];
    static readonly int[,] HomeAt = new int[15, 15];

    static LudoBoardControl()
    {
        for (int r = 0; r < 15; r++)
            for (int c = 0; c < 15; c++) { RingAt[r, c] = -1; HomeAt[r, c] = -1; }
        for (int i = 0; i < 52; i++) RingAt[Ring[i].r, Ring[i].c] = i;
        for (int k = 0; k < 5; k++)
        {
            HomeAt[7, 1 + k] = 0;      // Red home column (row 7, left)
            HomeAt[1 + k, 7] = 1;      // Green home column (column 7, top)
            HomeAt[7, 13 - k] = 2;     // Yellow home column (row 7, right)
            HomeAt[13 - k, 7] = 3;     // Blue home column (column 7, bottom)
        }
    }

    public LudoGame Game;
    public bool[] Seated = new bool[4];          // who is sitting down (used before the game starts)
    public HashSet<int> Clickable { get; set; } = new();
    public event Action<int> TokenClicked;

    readonly PointF[] tokenPix = new PointF[16];
    readonly float[] tokenRad = new float[16];
    float S, ox, oy;

    public LudoBoardControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Ui.Bg;
    }

    static bool IsYard(int r, int c) => (r < 6 || r > 8) && (c < 6 || c > 8);
    static bool IsCentre(int r, int c) => r >= 6 && r <= 8 && c >= 6 && c <= 8;

    static PointF HomeCentre(int c, int i) => c switch
    {
        0 => new PointF(1 + i + .5f, 7.5f),
        1 => new PointF(7.5f, 1 + i + .5f),
        2 => new PointF(13 - i + .5f, 7.5f),
        _ => new PointF(7.5f, 13 - i + .5f)
    };

    /// <summary>Position of a token in grid units (x = column, y = row).</summary>
    PointF TokenGrid(int c, int k)
    {
        int p = Game.Pos[c, k];
        if (p < 0)
        {
            var (r0, c0) = YardOrigin[c];
            return new PointF(c0 + 1.5f + 3 * (k % 2), r0 + 1.5f + 3 * (k / 2));
        }
        if (p >= LudoGame.Finish) return new PointF(7.5f, 7.5f);
        if (p >= LudoGame.HomeStart) return HomeCentre(c, p - LudoGame.HomeStart);
        var (r, cc) = Ring[LudoGame.Square(c, p)];
        return new PointF(cc + .5f, r + .5f);
    }

    /// <summary>Computes pixel centres and radii for every token; tokens on the same square are spread out.</summary>
    void Layout()
    {
        S = Math.Min(Width, Height) / 15.6f;
        ox = (Width - 15 * S) / 2f;
        oy = (Height - 15 * S) / 2f;

        var groups = new Dictionary<(int, int), List<int>>();
        for (int c = 0; c < 4; c++)
            for (int k = 0; k < 4; k++)
            {
                int id = c * 4 + k;
                tokenRad[id] = 0;
                bool on = Game == null ? false : (Game.Started ? Game.Active[c] : Seated[c]);
                if (!on) continue;
                var g = TokenGrid(c, k);
                tokenPix[id] = new PointF(ox + g.X * S, oy + g.Y * S);
                tokenRad[id] = .34f * S;
                if (Game.Pos[c, k] < 0) continue;     // yard tokens never share a square
                var key = ((int)(g.X * 10), (int)(g.Y * 10));
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<int>();
                list.Add(id);
            }

        foreach (var list in groups.Values)
        {
            if (list.Count == 1) continue;
            for (int i = 0; i < list.Count; i++)
            {
                int id = list[i];
                tokenRad[id] = .27f * S;
                tokenPix[id] = new PointF(
                    tokenPix[id].X + ((i % 2) - .5f) * .5f * S,
                    tokenPix[id].Y + ((i / 2) - .5f) * .5f * S);
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Ui.Bg);
        if (Width < 200 || Height < 200 || Game == null) return;
        Layout();

        PointF P(float x, float y) => new PointF(ox + x * S, oy + y * S);

        // wooden frame
        var frame = new RectangleF(ox - 10, oy - 10, 15 * S + 20, 15 * S + 20);
        using (var fb = new LinearGradientBrush(frame, Color.FromArgb(70, 56, 120), Color.FromArgb(38, 30, 70), 45f))
            g.FillRectangle(fb, frame);

        // ring squares, start squares and home columns
        using (var grid = new Pen(Color.FromArgb(130, 130, 130), 1f))
        {
            for (int r = 0; r < 15; r++)
                for (int c = 0; c < 15; c++)
                {
                    if (IsYard(r, c) || IsCentre(r, c)) continue;
                    int ri = RingAt[r, c], ho = HomeAt[r, c];
                    if (ri < 0 && ho < 0) continue;
                    var rect = new RectangleF(ox + c * S, oy + r * S, S, S);
                    Color fill = Color.FromArgb(250, 250, 248);
                    if (ri >= 0)
                    {
                        for (int col = 0; col < 4; col++)
                            if (LudoGame.StartIdx[col] == ri) fill = Base[col];
                        if (Array.IndexOf(Stars, ri) >= 0) fill = Color.FromArgb(226, 226, 220);
                    }
                    else fill = Base[ho];

                    using (var b = new SolidBrush(fill)) g.FillRectangle(b, rect);
                    g.DrawRectangle(grid, rect.X, rect.Y, rect.Width, rect.Height);
                    if (ri >= 0 && Array.IndexOf(Stars, ri) >= 0)
                    {
                        using var dot = new SolidBrush(Color.FromArgb(150, 150, 140));
                        float d = S * .28f;
                        g.FillEllipse(dot, rect.X + S / 2 - d / 2, rect.Y + S / 2 - d / 2, d, d);
                    }
                }
        }

        // the four yards
        for (int c = 0; c < 4; c++)
        {
            var (r0, c0) = YardOrigin[c];
            var yard = new RectangleF(ox + c0 * S, oy + r0 * S, 6 * S, 6 * S);
            using (var yb = new SolidBrush(Base[c])) g.FillRectangle(yb, yard);
            var inner = RectangleF.Inflate(yard, -S * .55f, -S * .55f);
            using (var wb = new SolidBrush(Color.FromArgb(246, 246, 244))) g.FillRectangle(wb, inner);
            for (int k = 0; k < 4; k++)
            {
                var centre = P(c0 + 1.5f + 3 * (k % 2), r0 + 1.5f + 3 * (k / 2));
                using var hole = new SolidBrush(ControlPaint.Dark(Base[c], .2f));
                g.FillEllipse(hole, centre.X - .45f * S, centre.Y - .45f * S, .9f * S, .9f * S);
            }
            if (Game.Started && !Game.Over && Game.Turn == c && Game.Active[c])
                using (var tp = new Pen(Color.White, 3f))
                    g.DrawRectangle(tp, yard.X + 2, yard.Y + 2, yard.Width - 4, yard.Height - 4);
        }

        // centre: four coloured triangles pointing into the middle
        var centreP = P(7.5f, 7.5f);
        DrawTriangle(g, Base[1], P(6, 6), P(9, 6), centreP);   // green (top)
        DrawTriangle(g, Base[2], P(9, 6), P(9, 9), centreP);   // yellow (right)
        DrawTriangle(g, Base[3], P(9, 9), P(6, 9), centreP);   // blue (bottom)
        DrawTriangle(g, Base[0], P(6, 9), P(6, 6), centreP);   // red (left)
        using (var cb = new SolidBrush(Color.White))
            g.FillEllipse(cb, centreP.X - .4f * S, centreP.Y - .4f * S, .8f * S, .8f * S);

        // tokens
        for (int id = 0; id < 16; id++)
        {
            if (tokenRad[id] <= 0) continue;
            DrawToken(g, id / 4, tokenPix[id], tokenRad[id], Clickable.Contains(id));
        }
    }

    static void DrawTriangle(Graphics g, Color c, PointF a, PointF b, PointF d)
    {
        using var br = new SolidBrush(c);
        g.FillPolygon(br, new[] { a, b, d });
    }

    void DrawToken(Graphics g, int c, PointF p, float rad, bool glow)
    {
        var box = new RectangleF(p.X - rad, p.Y - rad, rad * 2, rad * 2);

        using (var sh = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
            g.FillEllipse(sh, box.X + rad * .12f, box.Y + rad * .24f, box.Width, box.Height);

        if (glow)
            using (var gp = new Pen(Color.FromArgb(255, 240, 90), Math.Max(2f, S * .08f)))
                g.DrawEllipse(gp, RectangleF.Inflate(box, S * .10f, S * .10f));

        using var path = new GraphicsPath();
        path.AddEllipse(box);
        using (var pgb = new PathGradientBrush(path))
        {
            pgb.CenterPoint = new PointF(p.X - rad * .30f, p.Y - rad * .35f);
            pgb.CenterColor = ControlPaint.Light(Base[c], .55f);
            pgb.SurroundColors = new[] { ControlPaint.Dark(Base[c], .25f) };
            g.FillPath(pgb, path);
        }
        using (var rim = new Pen(Color.FromArgb(150, 0, 0, 0), 1f))
            g.DrawEllipse(rim, box);
        using (var hl = new SolidBrush(Color.FromArgb(170, 255, 255, 255)))
            g.FillEllipse(hl, p.X - rad * .55f, p.Y - rad * .62f, rad * .45f, rad * .32f);
    }

    int HitToken(Point pt)
    {
        if (Game == null) return -1;
        Layout();
        int best = -1;
        float bestD = float.MaxValue;
        foreach (int id in Clickable)
        {
            if (tokenRad[id] <= 0) continue;
            float dx = pt.X - tokenPix[id].X, dy = pt.Y - tokenPix[id].Y;
            float d = dx * dx + dy * dy, reach = tokenRad[id] + S * .2f;
            if (d <= reach * reach && d < bestD) { best = id; bestD = d; }
        }
        return best;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = HitToken(e.Location) >= 0 ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int hit = HitToken(e.Location);
        if (hit >= 0) TokenClicked?.Invoke(hit);
    }
}
