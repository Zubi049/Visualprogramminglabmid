namespace LanArena;

public enum RollResult { Rolled, Passed, Forfeit }

/// <summary>
/// Ludo rules only (no UI, no networking). Each token's progress is:
///   -1      in base (yard)
///   0..50   on the main ring, counted from its colour's start square
///   51..55  home column
///   56      finished
/// Colours are seat numbers: 0 Red, 1 Green, 2 Yellow, 3 Blue.
/// </summary>
public class LudoGame
{
    public const int Colours = 4, Tokens = 4;
    public const int MainLast = 50, HomeStart = 51, Finish = 56;

    /// <summary>Start square of each colour on the 52-square ring.</summary>
    public static readonly int[] StartIdx = { 1, 14, 27, 40 };

    /// <summary>Start squares and the star 8 steps after them are safe from capture.</summary>
    public static readonly bool[] Safe = BuildSafe();

    public readonly int[,] Pos = new int[Colours, Tokens];
    public readonly bool[] Active = new bool[Colours];
    public readonly List<int> Ranking = new();   // colours in finishing order
    public int Turn, Dice, Sixes;
    public bool Started, Over;

    public class MoveInfo
    {
        public bool Captured, Home, Finished, Extra;
    }

    public LudoGame()
    {
        for (int c = 0; c < Colours; c++)
            for (int k = 0; k < Tokens; k++) Pos[c, k] = -1;
    }

    static bool[] BuildSafe()
    {
        var s = new bool[52];
        foreach (int st in StartIdx) { s[st] = true; s[(st + 8) % 52] = true; }
        return s;
    }

    public static int Square(int c, int p) => (StartIdx[c] + p) % 52;

    public bool IsFinished(int c)
    {
        for (int k = 0; k < Tokens; k++)
            if (Pos[c, k] != Finish) return false;
        return true;
    }

    public void Start(bool[] active)
    {
        for (int c = 0; c < Colours; c++)
        {
            Active[c] = active[c];
            for (int k = 0; k < Tokens; k++) Pos[c, k] = -1;
        }
        Ranking.Clear();
        Dice = 0;
        Sixes = 0;
        Over = false;
        Started = true;
        var options = Enumerable.Range(0, Colours).Where(c => Active[c]).ToList();
        Turn = options[Random.Shared.Next(options.Count)];   // random first player
    }

    public bool CanMove(int token)
    {
        int c = token / Tokens, k = token % Tokens, p = Pos[c, k];
        if (Over || Dice == 0 || c != Turn || !Active[c] || p == Finish) return false;
        return p < 0 ? Dice == 6 : p + Dice <= Finish;
    }

    public List<int> Movable()
    {
        var list = new List<int>();
        for (int k = 0; k < Tokens; k++)
            if (CanMove(Turn * Tokens + k)) list.Add(Turn * Tokens + k);
        return list;
    }

    /// <summary>Applies a dice value for the player whose turn it is.</summary>
    public RollResult Roll(int value)
    {
        Dice = value;
        Sixes = value == 6 ? Sixes + 1 : 0;
        if (Sixes >= 3) { NextTurn(); return RollResult.Forfeit; }
        if (Movable().Count == 0) { NextTurn(); return RollResult.Passed; }
        return RollResult.Rolled;
    }

    public MoveInfo Move(int token)
    {
        int c = token / Tokens, k = token % Tokens;
        var info = new MoveInfo();
        int p = Pos[c, k] < 0 ? 0 : Pos[c, k] + Dice;
        Pos[c, k] = p;

        // capture: any other colour's token sitting on this ring square goes back to base
        if (p <= MainLast && !Safe[Square(c, p)])
        {
            int sq = Square(c, p);
            for (int d = 0; d < Colours; d++)
            {
                if (d == c || !Active[d]) continue;
                for (int j = 0; j < Tokens; j++)
                    if (Pos[d, j] >= 0 && Pos[d, j] <= MainLast && Square(d, Pos[d, j]) == sq)
                    {
                        Pos[d, j] = -1;
                        info.Captured = true;
                    }
            }
        }

        info.Home = p >= HomeStart && p < Finish;
        info.Finished = p == Finish;
        bool extra = Dice == 6 && !Ranking.Contains(c);
        if (info.Finished && IsFinished(c)) Ranking.Add(c);

        CheckOver();
        Dice = 0;
        if (Over) { Sixes = 0; return info; }
        if (extra && !Ranking.Contains(c)) { info.Extra = true; return info; }   // a six gives another roll
        NextTurn();
        return info;
    }

    /// <summary>A player left mid-game: their colour is skipped from now on.</summary>
    public void Drop(int c)
    {
        if (!Active[c]) return;
        Active[c] = false;
        if (Over) return;
        if (c == Turn) NextTurn();
        CheckOver();
    }

    void CheckOver()
    {
        var left = new List<int>();
        for (int c = 0; c < Colours; c++)
            if (Active[c] && !Ranking.Contains(c)) left.Add(c);
        if (left.Count <= 1)
        {
            Ranking.AddRange(left);   // the last player standing finishes last
            Over = true;
        }
    }

    void NextTurn()
    {
        Dice = 0;
        Sixes = 0;
        for (int i = 1; i <= Colours; i++)
        {
            int c = (Turn + i) % Colours;
            if (Active[c] && !Ranking.Contains(c)) { Turn = c; return; }
        }
    }

    /// <summary>Compact state for the network: started|turn|dice|sixes|activeMask|positions|ranking|over</summary>
    public string Serialize()
    {
        int mask = 0;
        for (int c = 0; c < Colours; c++)
            if (Active[c]) mask |= 1 << c;
        var pos = new List<string>();
        for (int c = 0; c < Colours; c++)
            for (int k = 0; k < Tokens; k++) pos.Add(Pos[c, k].ToString());
        string rank = Ranking.Count == 0 ? "-" : string.Join(",", Ranking);
        return $"{(Started ? 1 : 0)}|{Turn}|{Dice}|{Sixes}|{mask}|{string.Join(",", pos)}|{rank}|{(Over ? 1 : 0)}";
    }

    /// <summary>Loads a state produced by <see cref="Serialize"/>. Throws on malformed input.</summary>
    public void Load(string[] p)
    {
        Started = p[0] == "1";
        Turn = int.Parse(p[1]);
        Dice = int.Parse(p[2]);
        Sixes = int.Parse(p[3]);
        int mask = int.Parse(p[4]);
        for (int c = 0; c < Colours; c++) Active[c] = (mask & (1 << c)) != 0;
        var pos = p[5].Split(',');
        for (int i = 0; i < Colours * Tokens; i++)
            Pos[i / Tokens, i % Tokens] = int.Parse(pos[i]);
        Ranking.Clear();
        if (p[6] != "-")
            foreach (var s in p[6].Split(',')) Ranking.Add(int.Parse(s));
        Over = p[7] == "1";
    }
}
