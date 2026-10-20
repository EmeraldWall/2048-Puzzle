using System.Diagnostics;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Skia;
using Puzzle2048.Core;
using Puzzle2048.Rendering;

/// <summary>
/// Draws the game screen frame after frame while moves animate, and reports the time and memory each frame costs.
/// It runs on the CPU with Skia, not on a phone, so use it to compare versions of the drawing code, not as a phone fps.
/// </summary>
internal static class Bench
{
    public static void Run()
    {
        Console.WriteLine("vector drawing");
        RunOnce();
        SkiaSprite.Install(2.625f);
        Console.WriteLine("sprite cache");
        RunOnce();
        Console.WriteLine($"  sprites cached    {Sprites.Count}");
        SkiaSprite.StampsOff = true;
        Console.WriteLine("sprite cache, stamps skipped (allocations of the game's own code)");
        RunOnce();
        SkiaSprite.StampsOff = false;
        foreach (var group in Sprites.Keys.GroupBy(k => k.Kind))
            Console.WriteLine($"    kind {group.Key}: {group.Count()}");
        foreach (var k in Sprites.Keys.Where(k => k.Kind == Sprites.KindGlyph).GroupBy(k => (char)k.A).OrderByDescending(g => g.Count()).Take(4))
            Console.WriteLine($"    glyph '{k.Key}': {k.Count()}  e.g. {k.First()}");
        SkiaSprite.Uninstall();
    }

    private static void RunOnce()
    {
        const float Width = 412f, Height = 832f, Scale = 2.625f;
        int Frames = int.TryParse(Environment.GetEnvironmentVariable("BENCH_FRAMES"), out int n) ? n : 240;
        const long FrameMs = 8; // a 120 Hz frame

        var session = new GameSession(GameMode.Classic, 4, randomFactory: () => new Random(7));
        session.Board.LoadValues([2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4, 2, 2, 4, 8]);
        long now = 1_000_000;
        var scene = new GameScene
        {
            Session = session,
            BackgroundColor = Color.FromArgb("#7FD3FF"),
            Clock = () => now,
            Hud = new HudState { Score = 12480, Best = 20480, ModeChip = "CLASSIC 4x4", ShowPowerUps = true, CanUndo = true, CanTrash = true, TrashCharges = 2 },
        };

        using var context = new SkiaBitmapExportContext((int)(Width * Scale), (int)(Height * Scale), 1f);
        context.Canvas.Scale(Scale, Scale);
        var area = new RectF(0, 0, Width, Height);
        Direction[] cycle = [Direction.Left, Direction.Down, Direction.Right, Direction.Up];

        void Frame(int i)
        {
            // A new swipe every 30 frames (240 ms), so most frames show tiles sliding or popping
            if (i % 30 == 0)
            {
                if (session.State != RunState.Playing)
                    session.StartFresh();
                if (session.Move(cycle[i / 30 % 4]) is { } move)
                    scene.OnMove(move);
            }
            scene.Draw(context.Canvas, area);
            now += FrameMs;
        }

        for (int i = 0; i < 60; i++)
            Frame(i); // warm up

        GC.Collect();
        long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        int gcBefore = GC.CollectionCount(0);
        var times = new double[Frames];
        var watch = new Stopwatch();
        for (int i = 0; i < Frames; i++)
        {
            watch.Restart();
            Frame(i);
            times[i] = watch.Elapsed.TotalMilliseconds;
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
        Array.Sort(times);

        Console.WriteLine($"frames            {Frames}");
        Console.WriteLine($"median ms/frame   {times[Frames / 2]:F2}");
        Console.WriteLine($"p95 ms/frame      {times[(int)(Frames * 0.95)]:F2}");
        Console.WriteLine($"KB alloc/frame    {bytes / 1024.0 / Frames:F1}");
        Console.WriteLine($"gen0 collections  {GC.CollectionCount(0) - gcBefore}");

        // Where the time goes
        Layer("backdrop", () => Backdrop.Paint(context.Canvas, area, Color.FromArgb("#7FD3FF"), now / 1000f));
        float side = Width - 24;
        Layer("board", () => scene.Board.DrawBoard(context.Canvas, new RectF(12, 200, side, side)));
        Layer("one tile panel", () => Painter.CandyPanel(context.Canvas, new RectF(40, 40, 90, 90), 18, Colors.Orange, Colors.Red, Colors.DarkRed));
        Layer("one 4-digit number", () => GlyphFont.DrawStyled(context.Canvas, "2048", 100, 100, 26, Colors.White, Colors.Wheat, Colors.Brown, 2.4f, Colors.Black.WithAlpha(0.2f)));
    }

    private static void Layer(string name, Action draw)
    {
        for (int i = 0; i < 5; i++)
            draw();
        var watch = Stopwatch.StartNew();
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        const int n = 40;
        for (int i = 0; i < n; i++)
            draw();
        Console.WriteLine($"  {name,-20} {watch.Elapsed.TotalMilliseconds / n,8:F2} ms  {(GC.GetAllocatedBytesForCurrentThread() - bytes) / 1024.0 / n,8:F1} KB");
    }
}
