using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanArena;

/// <summary>Wooden chess board with 3D-looking pieces. Click a piece, then click a highlighted square.</summary>
public class ChessBoardControl : Control
{
    const int Pad = 30;
    static readonly Color Light = Color.FromArgb(240, 217, 181);
    static readonly Color Dark = Color.FromArgb(181, 136, 99);

    public ChessGame Game = new ChessGame();
    public bool PlayerIsWhite = true;
    public bool Active = true;
    public event Action<Move> MoveMade;

    int selR = -1, selC = -1;
    List<Move> selMoves = new();
    float sq, ox, oy;

    public ChessBoardControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Ui.Bg;
    }

    public void Reset(bool playerIsWhite)
    {
        Game.Reset();
        PlayerIsWhite = playerIsWhite;
        ClearSel();
        Invalidate();
    }

    void ClearSel() { selR = selC = -1; selMoves = new List<Move>(); }

    /// <summary>Move received from the opponent. It is validated again here.</summary>
    public bool ApplyRemote(Move m)
    {
        if (Game.Result != null || Game.WhiteToMove == PlayerIsWhite) return false;
        if (!Game.TryMake(m)) return false;
        ClearSel();
        Invalidate();
        return true;
    }

    // ----- geometry -----
    void Geo()
    {
        float size = Math.Max(Math.Min(Width, Height) - 2 * Pad, 8);
        sq = size / 8f;
        ox = (Width - sq * 8) / 2f;
        oy = (Height - sq * 8) / 2f;
    }

    PointF Pos(int r, int c)
    {
        int dr = PlayerIsWhite ? r : 7 - r;
        int dc = PlayerIsWhite ? c : 7 - c;
        return new PointF(ox + dc * sq, oy + dr * sq);
    }

    bool Hit(Point p, out int r, out int c)
    {
        r = c = 0;
        int dc = (int)Math.Floor((p.X - ox) / sq), dr = (int)Math.Floor((p.Y - oy) / sq);
        if (dc < 0 || dc > 7 || dr < 0 || dr > 7) return false;
        r = PlayerIsWhite ? dr : 7 - dr;
        c = PlayerIsWhite ? dc : 7 - dc;
        return true;
    }

    // ----- painting -----
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.Clear(Ui.Bg);
        if (Math.Min(Width, Height) < 2 * Pad + 80) return;
        Geo();

        // wooden frame with a bevelled edge
        var frame = new RectangleF(ox - 12, oy - 12, sq * 8 + 24, sq * 8 + 24);
        using (var wood = new LinearGradientBrush(frame, Color.FromArgb(138, 90, 50), Color.FromArgb(72, 44, 22), 45f))
            g.FillRectangle(wood, frame);
        using (var bevel = new Pen(Color.FromArgb(200, 160, 100), 1.5f))
            g.DrawRectangle(bevel, frame.X + 3, frame.Y + 3, frame.Width - 6, frame.Height - 6);

        using var lightB = new SolidBrush(Light);
        using var darkB = new SolidBrush(Dark);
        using var seam = new Pen(Color.FromArgb(60, 0, 0, 0), 0.6f);
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var p = Pos(r, c);
                var rect = new RectangleF(p.X, p.Y, sq, sq);
                g.FillRectangle((r + c) % 2 == 0 ? lightB : darkB, rect);
                g.DrawRectangle(seam, rect.X, rect.Y, rect.Width, rect.Height);
            }

        // last move
        if (Game.HasLast)
        {
            using var hb = new SolidBrush(Color.FromArgb(110, 246, 246, 105));
            var a = Pos(Game.LastMove.FR, Game.LastMove.FC);
            var b = Pos(Game.LastMove.TR, Game.LastMove.TC);
            g.FillRectangle(hb, a.X, a.Y, sq, sq);
            g.FillRectangle(hb, b.X, b.Y, sq, sq);
        }

        // selected square
        if (selR >= 0)
        {
            using var sb = new SolidBrush(Color.FromArgb(150, 70, 130, 255));
            var p = Pos(selR, selC);
            g.FillRectangle(sb, p.X, p.Y, sq, sq);
        }

        // king in check
        if (Game.InCheck(Game.WhiteToMove))
        {
            var k = Game.KingSquare(Game.WhiteToMove);
            if (k.r >= 0)
            {
                using var cb = new SolidBrush(Color.FromArgb(170, 255, 60, 60));
                var p = Pos(k.r, k.c);
                g.FillEllipse(cb, p.X + sq * 0.05f, p.Y + sq * 0.05f, sq * 0.9f, sq * 0.9f);
            }
        }

        // coordinates
        using (var cf = Ui.F(Math.Max(8, sq * 0.17f), FontStyle.Bold))
        using (var cbr = new SolidBrush(Color.FromArgb(235, 220, 190)))
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            for (int dc = 0; dc < 8; dc++)
            {
                int file = PlayerIsWhite ? dc : 7 - dc;
                g.DrawString(((char)('a' + file)).ToString(), cf, cbr, new RectangleF(ox + dc * sq, oy + sq * 8 + 6, sq, 18), sf);
            }
            for (int dr = 0; dr < 8; dr++)
            {
                int rank = PlayerIsWhite ? 8 - dr : dr + 1;
                g.DrawString(rank.ToString(), cf, cbr, new RectangleF(ox - 26, oy + dr * sq, 18, sq), sf);
            }
        }

        // pieces (3D-shaded)
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                if (Game.B[r, c] != '.')
                {
                    var p = Pos(r, c);
                    ChessArt.Draw(g, Game.B[r, c], new RectangleF(p.X, p.Y, sq, sq));
                }

        // legal move hints
        using (var db = new SolidBrush(Color.FromArgb(95, 0, 0, 0)))
        using (var dp = new Pen(Color.FromArgb(95, 0, 0, 0), Math.Max(2f, sq * 0.08f)))
            foreach (var m in selMoves)
            {
                var p = Pos(m.TR, m.TC);
                if (Game.B[m.TR, m.TC] == '.' && !(m.FC != m.TC && char.ToLower(Game.B[m.FR, m.FC]) == 'p'))
                    g.FillEllipse(db, p.X + sq * 0.36f, p.Y + sq * 0.36f, sq * 0.28f, sq * 0.28f);
                else
                    g.DrawEllipse(dp, p.X + sq * 0.08f, p.Y + sq * 0.08f, sq * 0.84f, sq * 0.84f);
            }
    }

    // ----- input -----
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Geo();
        bool hand = false;
        if (Active && Game.Result == null && Game.WhiteToMove == PlayerIsWhite && Hit(e.Location, out int r, out int c))
        {
            char p = Game.B[r, c];
            hand = (p != '.' && char.IsUpper(p) == PlayerIsWhite) || selMoves.Any(m => m.TR == r && m.TC == c);
        }
        Cursor = hand ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Active || Game.Result != null || Game.WhiteToMove != PlayerIsWhite) return;
        Geo();
        if (!Hit(e.Location, out int r, out int c)) return;

        var targets = selMoves.Where(m => m.TR == r && m.TC == c).ToList();
        if (selR >= 0 && targets.Count > 0)
        {
            Move m = targets[0];
            if (targets.Count > 1)   // promotion: ask which piece
            {
                char pc = PromoDialog.Ask(this, PlayerIsWhite);
                m = targets.First(x => x.Promo == pc);
            }
            ClearSel();
            Game.MakeMove(m);
            Invalidate();
            MoveMade?.Invoke(m);
            return;
        }

        char p = Game.B[r, c];
        if (p != '.' && char.IsUpper(p) == PlayerIsWhite)
        {
            selR = r; selC = c;
            selMoves = Game.LegalMoves().Where(m => m.FR == r && m.FC == c).ToList();
        }
        else ClearSel();
        Invalidate();
    }
}

/// <summary>Promotion picker that shows the real 3D piece images.</summary>
public static class PromoDialog
{
    public static char Ask(IWin32Window owner, bool white)
    {
        char result = 'q';
        using var f = new Form
        {
            Text = "Promote pawn to...",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(4 * 80 + 20, 110),
            MaximizeBox = false,
            MinimizeBox = false,
            ControlBox = false,
            BackColor = Ui.Card
        };
        char[] pieces = white ? new[] { 'Q', 'R', 'B', 'N' } : new[] { 'q', 'r', 'b', 'n' };
        for (int i = 0; i < 4; i++)
        {
            char pc = pieces[i];
            var panel = new Panel
            {
                Location = new Point(10 + i * 80, 20),
                Size = new Size(70, 70),
                BackColor = Ui.Card2,
                Cursor = Cursors.Hand
            };
            panel.Paint += (_, e) => ChessArt.Draw(e.Graphics, pc, new RectangleF(0, 0, 70, 70));
            panel.Click += (_, _) =>
            {
                result = char.ToLower(pc);
                f.DialogResult = DialogResult.OK;
            };
            f.Controls.Add(panel);
        }
        f.ShowDialog(owner);
        return result;
    }
}
