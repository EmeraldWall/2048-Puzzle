using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Skia;
using Puzzle2048.Core;
using Puzzle2048.Rendering;

// Renders the game screen in several states to PNG files, at the size of a typical phone (412 x 892 dp).
// Usage: dotnet run --project tools/Puzzle2048.Preview -- <output folder>
string outDir = args.Length > 0 ? args[0] : "preview";
Directory.CreateDirectory(outDir);

const float Width = 412f, Height = 892f, Scale = 2.625f, AdHeight = 60f;

void Save(string name, IDrawable drawable)
{
    var context = new SkiaBitmapExportContext((int)(Width * Scale), (int)(Height * Scale), 1f);
    context.Canvas.Scale(Scale, Scale);
    drawable.Draw(context.Canvas, new RectF(0, 0, Width, Height - AdHeight));

    // Where the banner ad sits in the app (it is a separate Android view, not part of the drawing)
    context.Canvas.FillColor = Color.FromArgb("#E9E9EE");
    context.Canvas.FillRectangle(0, Height - AdHeight, Width, AdHeight);
    GlyphFont.DrawCentered(context.Canvas, "Ad banner", Width / 2f, Height - AdHeight / 2f, 11f, Color.FromArgb("#9A9AA8"));
    context.WriteToFile(Path.Combine(outDir, name));
    Console.WriteLine($"wrote {name}");
}

GameSession Session(GameMode mode, int size, int[] values, int day = 20371)
{
    var session = new GameSession(mode, size, dayNumber: day, randomFactory: () => new Random(4));
    session.Board.LoadValues(values);
    return session;
}

long now = 10_000_000;
GameScene NewScene(GameSession session, HudState hud, string background = "#7FD3FF")
{
    var scene = new GameScene { Session = session, Hud = hud, BackgroundColor = Color.FromArgb(background), Clock = () => now };
    return scene;
}

// 1. Classic mid-game with power-ups
var classic = Session(GameMode.Classic, 4, [2, 4, 8, 16, 32, 64, 128, 0, 256, 512, 0, 4, 1024, 2048, 2, 0]);
var classicScene = NewScene(classic, new HudState
{
    Score = 12480, Best = 20480, ModeChip = "CLASSIC 4x4",
    ShowPowerUps = true, CanUndo = true, CanTrash = true, TrashCharges = 2,
});
Save("01_classic.png", classicScene);

// 2. A combo move: praise word, merge bursts and floating points caught mid-flight
var daily = Session(GameMode.Daily, 4, [64, 64, 8, 8, 2, 4, 2, 0, 16, 0, 4, 0, 0, 2, 0, 0], day: 20370);
var dailyScene = NewScene(daily, new HudState
{
    Score = 1840, Best = 2310, ModeChip = "DAILY  OCT 10",
    StripText = "Make a 512 tile", StripFraction = 0.62f, BottomHint = "Same puzzle for everyone today",
});
MoveResult move = daily.Move(Direction.Left)!;
dailyScene.OnMove(move);
dailyScene.Praise("GREAT!", "x3 COMBO  +120", Color.FromArgb("#FFD93D"), Color.FromArgb("#FF8F3D"), Color.FromArgb("#5B2A86"));
dailyScene.Hud.Score = 1840 + move.PointsEarned;
now += 330;
Save("02_combo.png", dailyScene);

// 3. Time Attack in the last seconds
var timeAttack = Session(GameMode.TimeAttack, 4, [4, 8, 16, 2, 32, 64, 8, 4, 2, 128, 16, 2, 0, 4, 2, 0]);
var taScene = NewScene(timeAttack, new HudState
{
    Score = 3120, Best = 4080, ModeChip = "TIME ATTACK",
    StripText = "0:07", StripFraction = 0.08f, StripUrgent = true, BottomHint = "Every merge adds time",
}, "#FFC1DE");
Save("03_time_attack.png", taScene);

// 4. Daily result with three stars and confetti
var won = Session(GameMode.Daily, 4, [512, 128, 64, 8, 16, 32, 8, 2, 4, 2, 4, 0, 2, 0, 0, 0], day: 20370);
var resultScene = NewScene(won, new HudState { Score = 4210, Best = 4210, ModeChip = "DAILY  OCT 10", StripText = "Make a 512 tile", StripFraction = 1f, StripDone = true });
resultScene.ShowResult(new ResultCard
{
    Title = "Goal complete!",
    Stars = 3,
    Celebrate = true,
    Lines = ["Score 4,210", "Best today 4,210", "Streak 6 days"],
    Primary = "Play again", Secondary = "Share", Tertiary = "Menu",
});
now += 1600;
Save("04_result_daily.png", resultScene);

// 5. 6x6 board on mint
var big = Session(GameMode.Classic, 6, [2, 4, 8, 0, 16, 32, 64, 128, 0, 0, 256, 512, 0, 1024, 2, 4, 8, 16, 2048, 0, 4, 2, 0, 0, 0, 8, 16, 32, 64, 0, 0, 0, 2, 0, 4, 2]);
Save("05_classic_6x6.png", NewScene(big, new HudState
{
    Score = 26880, Best = 31200, ModeChip = "CLASSIC 6x6", ShowPowerUps = true, CanUndo = false, CanTrash = true, TrashCharges = 1,
}, "#B9F6CA"));

// 6. Game over card
var lost = Session(GameMode.Classic, 4, [2, 4, 2, 4, 4, 2, 4, 2, 2, 4, 2, 8, 4, 2, 16, 32]);
var lostScene = NewScene(lost, new HudState { Score = 2950, Best = 20480, ModeChip = "CLASSIC 4x4", ShowPowerUps = true, TrashCharges = 0 });
lostScene.ShowResult(new ResultCard { Title = "Game over", Lines = ["Score 2,950", "Best 20,480"], Primary = "Try again", Secondary = "Share", Tertiary = "Menu" });
now += 1000;
Save("06_game_over.png", lostScene);

// 7. Menu backdrop alone
Save("07_backdrop.png", new Backdrop { Clock = () => now });
