using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Skia;
using Puzzle2048.Core;
using Puzzle2048.Rendering;

// Usage: dotnet run --project tools/Puzzle2048.Preview -- <output folder>
string outDir = args.Length > 0 ? args[0] : "preview";
Directory.CreateDirectory(outDir);

Color sky = Color.FromArgb("#7FD3FF");

void Save(string name, BoardRenderer renderer, int width = 1080, int height = 1180)
{
    var context = new SkiaBitmapExportContext(width, height, 1f);
    context.Canvas.FillColor = sky;
    context.Canvas.FillRectangle(0, 0, width, height);
    renderer.Draw(context.Canvas, new RectF(0, 40, width, width));
    context.WriteToFile(Path.Combine(outDir, name));
    Console.WriteLine($"wrote {name}");
}

BoardRenderer Frozen(GameSession session, long now)
{
    return new BoardRenderer { Session = session, Clock = () => now };
}

// 1. Every tile colour
var showcase = new GameSession(GameMode.Classic, 4);
showcase.Board.LoadValues([2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 0, 2]);
Save("board_all_tiles.png", Frozen(showcase, 1_000_000));

// 2. Mid game with the combo banner
var daily = new GameSession(GameMode.Daily, dayNumber: 20000);
daily.Board.LoadValues(new int[daily.Size * daily.Size]);
int[] cells = new int[daily.Size * daily.Size];
int[] sample = [2, 4, 8, 16, 0, 64, 128, 256, 4, 0, 32, 0, 0, 0, 2, 0];
for (int i = 0; i < Math.Min(sample.Length, cells.Length); i++) cells[i] = sample[i];
daily.Board.LoadValues(cells);
long t = 1_000_000;
var renderer = new BoardRenderer { Session = daily, Clock = () => t };
renderer.ShowBanner("COMBO x3  +120");
t += 400;
Save("board_banner.png", renderer);

// 3. A frame in the middle of a slide
var moving = new GameSession(GameMode.Classic, 4, randomFactory: () => new Random(11));
moving.Board.LoadValues([2, 2, 0, 4, 0, 8, 8, 0, 16, 0, 0, 16, 0, 0, 0, 2]);
long clock = 2_000_000;
var slideRenderer = new BoardRenderer { Session = moving, Clock = () => clock };
MoveResult move = moving.Move(Direction.Left)!;
slideRenderer.BeginMove(move);
clock += 50;
Save("board_mid_slide.png", slideRenderer);
clock += 200;
Save("board_after_slide.png", slideRenderer);

// 4. Bigger boards
var big = new GameSession(GameMode.Classic, 6);
big.Board.LoadValues([2, 4, 8, 0, 16, 32, 64, 128, 0, 0, 256, 512, 0, 1024, 2, 4, 8, 16, 2048, 0, 4, 2, 0, 0, 0, 8, 16, 32, 64, 0, 0, 0, 2, 0, 4, 2]);
Save("board_6x6.png", Frozen(big, 3_000_000));

// 5. Lost board dims
var lost = new GameSession(GameMode.Classic, 4);
lost.Board.LoadValues([2, 4, 2, 4, 4, 2, 4, 2, 2, 4, 2, 8, 4, 2, 16, 32]);
SetLost(lost);
Save("board_lost.png", Frozen(lost, 4_000_000));

static void SetLost(GameSession s)
{
    // Force the end state the same way a locked board gets there
    typeof(GameSession).GetProperty(nameof(GameSession.State))!.SetValue(s, RunState.Lost);
}
