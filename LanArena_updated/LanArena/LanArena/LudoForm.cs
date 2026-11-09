using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace LanArena;

/// <summary>
/// Ludo room + board for 2 to 4 players on one LAN.
/// The host (seat 0) runs the rules and broadcasts the state; every other player sends their
/// rolls and moves to the host. Protocol (one message per line):
///   host -> player : START|hostName|Ludo|seat   LOBBY|n0|n1|n2|n3|started   STATE|...   EV|code   CHAT|name|text   BYE
///   player -> host : HELLO|name   ROLL   MOVE|token   CHAT|text   BYE
/// </summary>
public class LudoForm : Form
{
    const int Seats = 4;

    readonly bool isHost;
    readonly string myName;
    readonly int mySeat;
    readonly string[] names = new string[Seats];     // null = empty seat
    readonly NetPeer[] peers = new NetPeer[Seats];   // host: remote seats; player: [0] = link to host
    readonly LudoGame game = new();
    readonly Random rng = new();
    readonly Label[] seatLbl = new Label[Seats];
    TcpListener listener;
    Broadcaster broadcaster;
    bool hostGone, closing;
    int lastDice;

    LudoBoardControl board;
    DiceControl dice;
    Label lblStatus;
    TextBox txtLog, txtMsg;
    Button btnStart, btnLeave;

    /// <summary>Host window: takes ownership of the listening socket and the room broadcast.</summary>
    public LudoForm(string hostName, TcpListener listener, Broadcaster broadcaster)
    {
        isHost = true;
        mySeat = 0;
        myName = names[0] = Clean(hostName);
        this.listener = listener;
        this.broadcaster = broadcaster;

        BuildUi();
        Text = "Ludo - hosting a room";
        Shown += (_, _) => AcceptLoop();
        FormClosing += (_, _) => Shutdown();
        Log("Room open. Players on this network can see it. Press Start when 2-4 players are in.");
        UpdateView();
    }

    /// <summary>Joined player: the host has already accepted us and given us a seat.</summary>
    public LudoForm(string name, NetPeer host, int seat, string hostName)
    {
        isHost = false;
        myName = Clean(name);
        mySeat = seat;
        names[0] = Clean(hostName);
        names[seat] = myName;
        peers[0] = host;

        BuildUi();
        Text = $"Ludo - {myName} ({LudoBoardControl.ColorNames[seat]})";
        host.LineReceived += OnHostLine;
        host.Disconnected += () => OnHostGone();
        FormClosing += (_, _) => Shutdown();
        host.StartReceiving();
        Log($"Joined the room. You are {LudoBoardControl.ColorNames[seat]}.");
        UpdateView();
    }

    // ================================================================ UI
    void BuildUi()
    {
        ClientSize = new Size(1080, 700);
        MinimumSize = new Size(920, 620);
        BackColor = Ui.Bg;
        ForeColor = Ui.Txt;
        Font = Ui.F(10);
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(14), BackColor = Ui.Bg };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        board = new LudoBoardControl { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0) };
        board.TokenClicked += OnTokenClicked;
        root.Controls.Add(board, 0, 0);

        var side = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, Margin = new Padding(0), BackColor = Ui.Bg };
        side.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));    // 0 status
        for (int s = 0; s < Seats; s++) side.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));   // 1-4 players
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));   // 5 dice
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // 6 chat log
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));    // 7 chat input
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));    // 8 buttons

        lblStatus = new Label
        {
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, BackColor = Ui.Card2,
            ForeColor = Ui.Txt, Font = Ui.F(10.5f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 6)
        };
        side.Controls.Add(lblStatus, 0, 0);

        for (int s = 0; s < Seats; s++)
        {
            seatLbl[s] = new Label
            {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, BackColor = Ui.Card,
                ForeColor = Ui.Txt, Font = Ui.F(9.5f), Padding = new Padding(12, 0, 0, 0), Margin = new Padding(0, 0, 0, 6)
            };
            side.Controls.Add(seatLbl[s], 0, s + 1);
        }

        dice = new DiceControl { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
        dice.Clicked += OnRollClicked;
        side.Controls.Add(dice, 0, 5);

        txtLog = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            BackColor = Ui.Card, ForeColor = Ui.Txt, BorderStyle = BorderStyle.None, Font = Ui.F(9.5f),
            Margin = new Padding(0, 0, 0, 6)
        };
        side.Controls.Add(txtLog, 0, 6);

        txtMsg = new TextBox
        {
            Dock = DockStyle.Fill, BackColor = Ui.Card2, ForeColor = Ui.Txt, BorderStyle = BorderStyle.FixedSingle,
            Font = Ui.F(10.5f), PlaceholderText = "Chat with the players - press Enter", Margin = new Padding(0, 0, 0, 6)
        };
        txtMsg.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SendChat(); }
        };
        side.Controls.Add(txtMsg, 0, 7);

        var btns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        btns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        btns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        btnStart = Ui.Btn("Start game", Ui.Accent2);
        btnStart.ForeColor = Color.FromArgb(10, 25, 30);
        btnStart.Click += (_, _) => HostStart();
        btnStart.Visible = isHost;
        btnLeave = Ui.Btn("Leave", Ui.Danger);
        btnLeave.Click += (_, _) => Close();
        btns.Controls.Add(btnStart, 0, 0);
        btns.Controls.Add(btnLeave, 1, 0);
        side.Controls.Add(btns, 0, 8);

        root.Controls.Add(side, 1, 0);
        Controls.Add(root);
    }

    bool MyTurn => game.Started && !game.Over && !hostGone && game.Turn == mySeat;

    string NameOf(int c) => names[c] ?? LudoBoardControl.ColorNames[c];

    int HomeCount(int s)
    {
        int n = 0;
        for (int k = 0; k < LudoGame.Tokens; k++)
            if (game.Pos[s, k] == LudoGame.Finish) n++;
        return n;
    }

    /// <summary>Redraws everything from the current game state.</summary>
    void UpdateView()
    {
        if (IsDisposed || lblStatus == null) return;

        // the dice flicker when a new value arrives
        if (game.Dice != lastDice)
        {
            if (game.Dice > 0) dice.Animate(game.Dice);
            else dice.SetFace(0);
            lastDice = game.Dice;
        }

        bool myTurn = MyTurn;
        dice.CanRoll = myTurn && game.Dice == 0;
        board.Game = game;
        board.Seated = names.Select(n => n != null).ToArray();
        board.Clickable = myTurn && game.Dice > 0 ? new HashSet<int>(game.Movable()) : new HashSet<int>();
        board.Invalidate();

        for (int s = 0; s < Seats; s++)
        {
            string nm = names[s];
            string tag = nm == null ? "empty seat" : (s == mySeat ? nm + " (you)" : nm);
            string info;
            if (!game.Started) info = nm == null ? "waiting for a player" : "in the room";
            else if (!game.Active[s]) info = "left the game";
            else info = $"{HomeCount(s)}/4 tokens home";
            bool turn = game.Started && !game.Over && game.Active[s] && game.Turn == s;
            if (turn) info += "   <- their turn";
            seatLbl[s].Text = $"{LudoBoardControl.ColorNames[s]}:  {tag}\n{info}";
            seatLbl[s].BackColor = turn ? Ui.Accent : Ui.Card;
            seatLbl[s].ForeColor = nm == null ? Ui.Muted : Ui.Txt;
        }

        int joined = names.Count(n => n != null);
        string status;
        if (hostGone) status = "The host closed the room.";
        else if (!game.Started)
            status = isHost
                ? (joined >= 2 ? $"{joined} players ready - press Start when everyone is in." : "Waiting for players to join...")
                : "Waiting for the host to start the game...";
        else if (game.Over)
            status = "Game over!  Winner: " + NameOf(game.Ranking[0]);
        else if (myTurn)
            status = game.Dice == 0 ? "Your turn - click the dice to roll" : "Click a glowing token to move it";
        else
            status = NameOf(game.Turn) + "'s turn...";
        lblStatus.Text = status;
        lblStatus.ForeColor = myTurn ? Ui.Good : Ui.Txt;
        btnStart.Enabled = isHost && !game.Started && joined >= 2;
    }

    void Log(string s)
    {
        if (txtLog != null) txtLog.AppendText(s + "\r\n");
    }

    static string Clean(string s)
    {
        s = (s ?? "").Replace("|", "").Trim();
        if (s.Length > 20) s = s.Substring(0, 20);
        return s == "" ? "Player" : s;
    }

    static void Split(string line, out string cmd, out string arg)
    {
        int i = line.IndexOf('|');
        cmd = i < 0 ? line : line.Substring(0, i);
        arg = i < 0 ? "" : line.Substring(i + 1);
    }

    static async Task<string> ReadWithTimeout(NetPeer p, int ms)
    {
        var t = p.ReadLineAsync();
        return await Task.WhenAny(t, Task.Delay(ms)) == t ? await t : null;
    }

    // ================================================================ host: lobby
    async void AcceptLoop()
    {
        while (!closing && listener != null)
        {
            TcpClient tc;
            try { tc = await listener.AcceptTcpClientAsync(); }
            catch { return; }     // listener was stopped

            var p = new NetPeer(tc);
            string hello = await ReadWithTimeout(p, 4000);
            if (hello == null || !hello.StartsWith("HELLO|")) { p.Dispose(); continue; }

            int seat = FreeSeat();
            if (game.Started || seat < 0)
            {
                p.Send("NOPE|" + (game.Started ? "That game has already started." : "The room is full (4 players)."));
                p.Dispose();
                continue;
            }

            int s = seat;
            names[s] = Clean(hello.Substring(6));
            peers[s] = p;
            p.LineReceived += line => OnRemoteLine(s, line);
            p.Disconnected += () => { if (peers[s] == p) OnRemoteGone(s); };
            p.StartReceiving();
            p.Send($"START|{names[0]}|Ludo|{s}");
            Log($"{names[s]} joined as {LudoBoardControl.ColorNames[s]}.");
            BroadcastLobby();
            UpdateView();
        }
    }

    int FreeSeat()
    {
        for (int s = 1; s < Seats; s++)
            if (names[s] == null) return s;
        return -1;
    }

    void BroadcastLobby()
    {
        string line = "LOBBY|" + string.Join("|", names.Select(n => n ?? "-")) + "|" + (game.Started ? "1" : "0");
        for (int s = 1; s < Seats; s++) peers[s]?.Send(line);
    }

    void BroadcastState()
    {
        string line = "STATE|" + game.Serialize();
        for (int s = 1; s < Seats; s++) peers[s]?.Send(line);
    }

    void HostStart()
    {
        if (!isHost || game.Started) return;
        var active = new bool[Seats];
        for (int s = 0; s < Seats; s++) active[s] = names[s] != null;
        int count = active.Count(a => a);
        if (count < 2) return;

        game.Start(active);
        Log($"Game on with {count} players. {NameOf(game.Turn)} moves first.");
        BroadcastLobby();
        BroadcastState();
        UpdateView();
    }

    // ================================================================ host: game rules
    void HostRoll(int seat)
    {
        int value = rng.Next(1, 7);
        var res = game.Roll(value);
        if (res == RollResult.Forfeit) Log($"{NameOf(seat)} rolled three 6s in a row - turn lost.");
        else if (res == RollResult.Passed) Log($"{NameOf(seat)} rolled {value} - no move possible.");
        else Log($"{NameOf(seat)} rolled {value}.");
        Announce(res == RollResult.Forfeit ? "F" : res == RollResult.Passed ? "P" : "R");
        BroadcastState();
        UpdateView();
    }

    void HostMove(int token)
    {
        int seat = token / LudoGame.Tokens;
        var info = game.Move(token);
        if (info.Captured) Log($"{NameOf(seat)} captured a piece!");
        if (info.Finished) Log($"{NameOf(seat)} brought a token home.");
        if (game.Over)
        {
            string order = string.Join(", ", game.Ranking.Select((c, i) => $"{i + 1}. {NameOf(c)}"));
            Log("Game over!  Final order: " + order);
        }

        string code = game.Over ? "W" : info.Captured ? "C" : (info.Finished || info.Home) ? "H" : "M";
        Announce(code);
        BroadcastState();
        UpdateView();
    }

    /// <summary>Tells every player what just happened so they can play the matching sound.</summary>
    void Announce(string code)
    {
        for (int s = 1; s < Seats; s++) peers[s]?.Send("EV|" + code);
        PlayEvent(code);
    }

    static void PlayEvent(string code)
    {
        switch (code)
        {
            case "R": Sfx.Play(Sfx.Roll); break;
            case "M": Sfx.Play(Sfx.Move); break;
            case "C": Sfx.Play(Sfx.Capture); break;
            case "H": Sfx.Play(Sfx.Home); break;
            case "P": Sfx.Play(Sfx.Pass); break;
            case "F": Sfx.Play(Sfx.Lose); break;
            case "W": Sfx.Play(Sfx.Win); break;
        }
    }

    // ================================================================ local actions (this player clicked)
    void OnRollClicked()
    {
        if (!MyTurn || game.Dice != 0) return;
        if (isHost) HostRoll(mySeat);
        else peers[0]?.Send("ROLL");
    }

    void OnTokenClicked(int token)
    {
        if (!MyTurn || game.Dice == 0 || !board.Clickable.Contains(token)) return;
        board.Clickable = new HashSet<int>();    // ignore double clicks while the move is on its way
        board.Invalidate();
        if (isHost) HostMove(token);
        else peers[0]?.Send("MOVE|" + token);
    }

    // ================================================================ chat
    void SendChat()
    {
        string t = txtMsg.Text.Trim().Replace("\r", " ").Replace("\n", " ");
        if (t == "" || hostGone) return;
        txtMsg.Clear();
        if (isHost) Relay(0, t);
        else
        {
            peers[0]?.Send("CHAT|" + t);
            Log($"{myName}: {t}");
        }
    }

    /// <summary>Host only: show a chat line and forward it to everyone except the sender.</summary>
    void Relay(int from, string text)
    {
        Log($"{NameOf(from)}: {text}");
        if (from != 0) Sfx.Play(Sfx.Chat);
        for (int s = 1; s < Seats; s++)
            if (s != from) peers[s]?.Send($"CHAT|{NameOf(from)}|{text}");
    }

    // ================================================================ network: host side
    void OnRemoteLine(int seat, string line)
    {
        Split(line, out string cmd, out string arg);
        switch (cmd)
        {
            case "ROLL":
                if (game.Started && !game.Over && game.Turn == seat && game.Dice == 0) HostRoll(seat);
                break;
            case "MOVE":
                if (int.TryParse(arg, out int token) && token / LudoGame.Tokens == seat && game.CanMove(token))
                    HostMove(token);
                break;
            case "CHAT":
                if (arg.Trim() != "") Relay(seat, arg.Trim());
                break;
            case "BYE":
                OnRemoteGone(seat);
                break;
        }
    }

    void OnRemoteGone(int seat)
    {
        var p = peers[seat];
        if (p == null) return;
        peers[seat] = null;
        p.Dispose();
        string who = names[seat] ?? "A player";
        names[seat] = null;
        Log($"{who} left the room.");
        if (game.Started)
        {
            game.Drop(seat);
            BroadcastState();
        }
        BroadcastLobby();
        UpdateView();
    }

    // ================================================================ network: player side
    void OnHostLine(string line)
    {
        Split(line, out string cmd, out string arg);
        try
        {
            switch (cmd)
            {
                case "LOBBY":
                    {
                        var p = arg.Split('|');
                        for (int s = 0; s < Seats; s++) names[s] = p[s] == "-" ? null : p[s];
                        break;
                    }
                case "STATE":
                    game.Load(arg.Split('|'));
                    break;
                case "EV":
                    PlayEvent(arg);
                    break;
                case "CHAT":
                    {
                        int i = arg.IndexOf('|');
                        if (i > 0)
                        {
                            Log($"{arg.Substring(0, i)}: {arg.Substring(i + 1)}");
                            Sfx.Play(Sfx.Chat);
                        }
                        break;
                    }
                case "BYE":
                    OnHostGone();
                    break;
            }
        }
        catch { /* malformed message: ignore */ }
        UpdateView();
    }

    void OnHostGone()
    {
        if (hostGone) return;
        hostGone = true;
        Log("The host closed the room or the connection was lost.");
        peers[0]?.Dispose();
        peers[0] = null;
        UpdateView();
    }

    // ================================================================ closing
    void Shutdown()
    {
        if (closing) return;
        closing = true;
        if (isHost)
        {
            for (int s = 1; s < Seats; s++)
            {
                if (peers[s] == null) continue;
                peers[s].Send("BYE");
                peers[s].Dispose();
                peers[s] = null;
            }
            try { listener?.Stop(); } catch { }
            broadcaster?.Dispose();
        }
        else if (peers[0] != null && !hostGone)
        {
            peers[0].Send("BYE");
            peers[0].Dispose();
        }
    }
}

/// <summary>Clickable dice: flickers through faces when rolled, then shows the result.</summary>
public class DiceControl : Control
{
    static readonly Random rnd = new();
    readonly System.Windows.Forms.Timer anim;
    int shown = 1, ticks, value;
    bool canRoll;

    public bool CanRoll
    {
        get => canRoll;
        set
        {
            canRoll = value;
            Cursor = value ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    public event Action Clicked;

    public DiceControl()
    {
        DoubleBuffered = true;
        anim = new System.Windows.Forms.Timer { Interval = 55 };
        anim.Tick += (_, _) =>
        {
            shown = rnd.Next(1, 7);
            if (--ticks <= 0)
            {
                anim.Stop();
                shown = value;
            }
            Invalidate();
        };
    }

    public void Animate(int result)
    {
        value = result;
        ticks = 10;
        anim.Start();
        Invalidate();
    }

    public void SetFace(int v)
    {
        anim.Stop();
        value = shown = v;
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (CanRoll) Clicked?.Invoke();
    }

    static (int x, int y)[] PipsFor(int v) => v switch
    {
        1 => new[] { (1, 1) },
        2 => new[] { (0, 0), (2, 2) },
        3 => new[] { (0, 0), (1, 1), (2, 2) },
        4 => new[] { (0, 0), (2, 0), (0, 2), (2, 2) },
        5 => new[] { (0, 0), (2, 0), (1, 1), (0, 2), (2, 2) },
        6 => new[] { (0, 0), (2, 0), (0, 1), (2, 1), (0, 2), (2, 2) },
        _ => Array.Empty<(int, int)>()
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Ui.Bg);
        int side = Math.Min(Width, Height) - 14;
        if (side < 20) return;

        var box = new RectangleF((Width - side) / 2f, (Height - side) / 2f, side, side);
        bool rolling = anim.Enabled;
        bool highlight = CanRoll && !rolling;

        using (var path = Ui.Round(Rectangle.Round(box), (int)(side * .18f)))
        {
            using (var body = new System.Drawing.Drawing2D.LinearGradientBrush(box, Color.White, Color.FromArgb(200, 200, 212), 60f))
                g.FillPath(body, path);
            using (var rim = new Pen(highlight ? Color.FromArgb(255, 210, 60) : Color.FromArgb(120, 120, 140), highlight ? 3f : 1.5f))
                g.DrawPath(rim, path);
        }

        if (value == 0 && !rolling)
        {
            using var tf = Ui.F(15, FontStyle.Bold);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var tb = new SolidBrush(highlight ? Color.FromArgb(40, 40, 60) : Color.Gray);
            g.DrawString(CanRoll ? "ROLL" : "...", tf, tb, box, sf);
            return;
        }

        float pr = side * .075f;
        using var pb = new SolidBrush(Color.FromArgb(28, 28, 40));
        foreach (var (gx, gy) in PipsFor(shown))
        {
            float px = box.X + box.Width * (.27f + .23f * gx);
            float py = box.Y + box.Height * (.27f + .23f * gy);
            g.FillEllipse(pb, px - pr, py - pr, pr * 2, pr * 2);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) anim.Dispose();
        base.Dispose(disposing);
    }
}
