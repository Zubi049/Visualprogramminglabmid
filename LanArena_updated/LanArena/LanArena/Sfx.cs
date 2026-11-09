using System.Media;
using System.Text;

namespace LanArena;

/// <summary>Sound effects. The WAV data is synthesised in code, so no audio files are needed.</summary>
public static class Sfx
{
    public const string Click = "click", Roll = "roll", Move = "move", Capture = "capture", Home = "home",
        Pass = "pass", Win = "win", Lose = "lose", Chat = "chat";

    const int Rate = 22050;
    public static bool Enabled = true;
    static readonly Dictionary<string, SoundPlayer> players = new();

    readonly record struct Note(double Freq, double Dur, double Amp, double Decay, bool Noise = false);

    static Sfx()
    {
        Add(Click, new Note(1800, .04, .30, 90));
        Add(Pass, new Note(330, .12, .30, 22));
        Add(Move, new Note(700, .08, .55, 40), new Note(520, .06, .25, 60));          // wooden tap
        Add(Roll, Enumerable.Range(0, 14).Select(_ => new Note(0, .035, .45, 28, true)).ToArray()); // rattle
        Add(Capture, new Note(170, .30, .75, 9), new Note(0, .05, .35, 60, true));   // thud
        Add(Home, new Note(523, .10, .35, 14), new Note(659, .10, .35, 14), new Note(784, .10, .35, 14), new Note(1046, .26, .40, 8));
        Add(Win, new Note(523, .12, .35, 10), new Note(659, .12, .35, 10), new Note(784, .12, .35, 10), new Note(1046, .40, .40, 5));
        Add(Lose, new Note(392, .20, .40, 7), new Note(330, .20, .40, 7), new Note(262, .40, .40, 5));
        Add(Chat, new Note(988, .08, .25, 25), new Note(1319, .14, .25, 18));
    }

    static void Add(string name, params Note[] notes) =>
        players[name] = new SoundPlayer(new MemoryStream(Render(notes)));

    public static void Play(string name)
    {
        if (!Enabled || !players.TryGetValue(name, out var sp)) return;
        try { sp.Play(); } catch { /* no audio device: stay silent */ }
    }

    static byte[] Render(Note[] notes)
    {
        var rnd = new Random(7);
        var pcm = new MemoryStream();
        using (var w = new BinaryWriter(pcm))
        {
            foreach (var n in notes)
            {
                int len = (int)(n.Dur * Rate);
                for (int i = 0; i < len; i++)
                {
                    double t = i / (double)Rate;
                    double env = Math.Min(1, t / .004) * Math.Exp(-n.Decay * t);
                    double v = n.Noise ? rnd.NextDouble() * 2 - 1 : Math.Sin(2 * Math.PI * n.Freq * t);
                    w.Write((short)(v * env * n.Amp * 30000));
                }
            }
        }

        byte[] data = pcm.ToArray();
        var ms = new MemoryStream();
        using (var h = new BinaryWriter(ms, Encoding.ASCII))
        {
            h.Write(Encoding.ASCII.GetBytes("RIFF"));
            h.Write(36 + data.Length);
            h.Write(Encoding.ASCII.GetBytes("WAVE"));
            h.Write(Encoding.ASCII.GetBytes("fmt "));
            h.Write(16);
            h.Write((short)1);       // PCM
            h.Write((short)1);       // mono
            h.Write(Rate);
            h.Write(Rate * 2);       // bytes per second
            h.Write((short)2);       // block align
            h.Write((short)16);      // bits per sample
            h.Write(Encoding.ASCII.GetBytes("data"));
            h.Write(data.Length);
            h.Write(data);
        }
        return ms.ToArray();
    }
}
