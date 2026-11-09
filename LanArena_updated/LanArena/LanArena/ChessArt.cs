using System.Drawing;
using System.Drawing.Drawing2D;

namespace LanArena;

/// <summary>
/// Draws chess pieces that look three-dimensional. Each piece is a turned (lathe) silhouette
/// with a cylindrical shading gradient, a contact shadow on the board and a specular highlight.
/// Upper case = white, lower case = black.
/// </summary>
public static class ChessArt
{
    // Body profiles as (half-width, height). Width is in units of R (half the base width),
    // height in units of H (the piece height). Listed bottom to top.
    static readonly (float w, float h)[] PawnBody =
        { (1f, 0f), (1f, .10f), (.80f, .14f), (.60f, .20f), (.42f, .30f), (.36f, .40f), (.46f, .50f), (.34f, .58f), (.30f, .64f) };
    static readonly (float w, float h)[] BishopBody =
        { (1f, 0f), (1f, .10f), (.80f, .14f), (.58f, .20f), (.40f, .32f), (.34f, .44f), (.44f, .54f), (.28f, .62f), (.24f, .66f) };
    static readonly (float w, float h)[] RookBody =
        { (1f, 0f), (1f, .10f), (.84f, .14f), (.66f, .22f), (.60f, .30f), (.60f, .68f), (.66f, .72f) };
    static readonly (float w, float h)[] QueenBody =
        { (1f, 0f), (1f, .10f), (.80f, .14f), (.60f, .22f), (.44f, .36f), (.38f, .48f), (.48f, .58f), (.40f, .68f), (.36f, .74f) };
    static readonly (float w, float h)[] KingBody = QueenBody;
    static readonly (float w, float h)[] KnightBase =
        { (1f, 0f), (1f, .10f), (.80f, .14f), (.55f, .20f) };

    // Knight outline (facing left) in units of H, y pointing up.
    static readonly PointF[] Horse =
    {
        new(-.34f, 0f), new(.34f, 0f), new(.30f, .14f), new(.20f, .30f), new(.18f, .44f), new(.10f, .60f),
        new(0f, .72f), new(-.12f, .78f), new(-.16f, .68f), new(-.26f, .66f), new(-.38f, .56f), new(-.40f, .48f),
        new(-.32f, .44f), new(-.22f, .42f), new(-.14f, .30f), new(-.20f, .14f)
    };

    /// <summary>Draws one piece inside a square cell.</summary>
    public static void Draw(Graphics g, char piece, RectangleF cell)
    {
        bool white = char.IsUpper(piece);
        float sq = cell.Width;
        float cx = cell.X + sq / 2f;
        float baseY = cell.Y + sq * 0.93f;
        float R = sq * 0.30f;   // half of the base width
        float H = sq * 0.74f;   // height of a unit profile

        var oldMode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // contact shadow on the board
        using (var sh = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
            g.FillEllipse(sh, cx - R * 1.15f, baseY - R * 0.22f, R * 2.3f, R * 0.44f);

        using var body = Shade(white, cx, R);
        using var edge = new Pen(white ? Color.FromArgb(120, 108, 92) : Color.Black, Math.Max(0.8f, sq * 0.014f));

        switch (char.ToLower(piece))
        {
            case 'p':
                Part(g, Lathe(cx, baseY, R, H, PawnBody), body, edge);
                Part(g, Ellipse(cx, baseY - .72f * H, .52f * R, .52f * R), body, edge);
                break;

            case 'b':
                {
                    float hy = baseY - .74f * H;
                    Part(g, Lathe(cx, baseY, R, H, BishopBody), body, edge);
                    Part(g, Ellipse(cx, hy, .44f * R, .62f * R), body, edge);
                    Part(g, Ellipse(cx, hy - .80f * R, .17f * R, .17f * R), body, edge);
                    using var cut = new Pen(white ? Color.FromArgb(150, 140, 125) : Color.FromArgb(70, 70, 80), Math.Max(0.8f, sq * 0.012f));
                    g.DrawLine(cut, cx - .22f * R, hy - .40f * R, cx + .22f * R, hy + .20f * R);
                    break;
                }

            case 'r':
                Part(g, Lathe(cx, baseY, R, H, RookBody), body, edge);
                Part(g, Rect(cx - 1.15f * R, baseY - .80f * H, 2.3f * R, .14f * H), body, edge);
                for (int i = -1; i <= 1; i++)
                    Part(g, Rect(cx + i * .8f * R - .3f * R, baseY - .95f * H, .6f * R, .16f * H), body, edge);
                break;

            case 'q':
                Part(g, Lathe(cx, baseY, R, H, QueenBody), body, edge);
                Part(g, Rect(cx - .62f * R, baseY - .80f * H, 1.24f * R, .10f * H), body, edge);
                for (int i = -2; i <= 2; i++)
                    Part(g, Ellipse(cx + i * .38f * R, baseY - .92f * H, .13f * R, .13f * R), body, edge);
                break;

            case 'k':
                Part(g, Lathe(cx, baseY, R, H, KingBody), body, edge);
                Part(g, Rect(cx - .55f * R, baseY - .80f * H, 1.10f * R, .10f * H), body, edge);
                Part(g, Rect(cx - .09f * R, baseY - 1.00f * H, .18f * R, .22f * H), body, edge);
                Part(g, Rect(cx - .30f * R, baseY - .95f * H, .60f * R, .08f * H), body, edge);
                break;

            case 'n':
                {
                    Part(g, Lathe(cx, baseY, R, H, KnightBase), body, edge);
                    var pts = Horse.Select(p => new PointF(cx + p.X * H, baseY - p.Y * H)).ToArray();
                    Part(g, Poly(pts), body, edge);
                    using var eye = new SolidBrush(Color.FromArgb(25, 25, 30));
                    g.FillEllipse(eye, cx - .26f * H, baseY - .63f * H, .07f * H, .07f * H);
                    break;
                }
        }

        // specular highlight
        using (var hl = new SolidBrush(Color.FromArgb(white ? 170 : 85, 255, 255, 255)))
            g.FillEllipse(hl, cx - .58f * R, baseY - .62f * H, .24f * R, .40f * H);

        g.SmoothingMode = oldMode;
    }

    /// <summary>Cylindrical shading: dark edge, bright highlight band, dark edge.</summary>
    static Brush Shade(bool white, float cx, float R)
    {
        var b = new LinearGradientBrush(new PointF(cx - R, 0), new PointF(cx + R, 0), Color.Black, Color.Black);
        b.InterpolationColors = white
            ? new ColorBlend
            {
                Positions = new[] { 0f, .25f, .45f, .75f, 1f },
                Colors = new[] { Color.FromArgb(118, 106, 90), Color.FromArgb(252, 250, 242), Color.FromArgb(236, 231, 218), Color.FromArgb(190, 182, 168), Color.FromArgb(120, 110, 96) }
            }
            : new ColorBlend
            {
                Positions = new[] { 0f, .25f, .45f, .75f, 1f },
                Colors = new[] { Color.FromArgb(4, 4, 6), Color.FromArgb(96, 96, 108), Color.FromArgb(58, 58, 68), Color.FromArgb(28, 28, 34), Color.FromArgb(6, 6, 9) }
            };
        return b;
    }

    static void Part(Graphics g, GraphicsPath p, Brush b, Pen edge)
    {
        using (p)
        {
            g.FillPath(b, p);
            g.DrawPath(edge, p);
        }
    }

    static GraphicsPath Lathe(float cx, float baseY, float R, float H, (float w, float h)[] prof)
    {
        var pts = new List<PointF>(prof.Length * 2);
        foreach (var (w, h) in prof) pts.Add(new PointF(cx + w * R, baseY - h * H));
        for (int i = prof.Length - 1; i >= 0; i--)
            pts.Add(new PointF(cx - prof[i].w * R, baseY - prof[i].h * H));
        var gp = new GraphicsPath();
        gp.AddPolygon(pts.ToArray());
        return gp;
    }

    static GraphicsPath Ellipse(float cx, float cy, float rx, float ry)
    {
        var gp = new GraphicsPath();
        gp.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2);
        return gp;
    }

    static GraphicsPath Rect(float x, float y, float w, float h)
    {
        var gp = new GraphicsPath();
        gp.AddRectangle(new RectangleF(x, y, w, h));
        return gp;
    }

    static GraphicsPath Poly(PointF[] pts)
    {
        var gp = new GraphicsPath();
        gp.AddPolygon(pts);
        return gp;
    }
}
