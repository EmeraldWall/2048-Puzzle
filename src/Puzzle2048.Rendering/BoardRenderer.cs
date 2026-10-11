using Microsoft.Maui.Graphics;
using Puzzle2048.Core;
using static Puzzle2048.Rendering.Painter;

namespace Puzzle2048.Rendering;

/// <summary>
/// Draws the board and its tiles and animates slides, merges, new tiles and the opening reveal.
/// It has no dependency on the screen, so the same code runs in the app and in the preview tool.
/// </summary>
public sealed class BoardRenderer : IDrawable
{
    public const long SlideMilliseconds = 115;
    public const long PopMilliseconds = 180;
    public const long RevealMilliseconds = 420;

    private MoveResult? _move;
    private long _moveStart = -1_000_000;
    private long _revealStart = -1_000_000;

    /// <summary>The game to draw.</summary>
    public GameSession? Session { get; set; }

    /// <summary>Milliseconds clock. The preview tool replaces it to freeze time.</summary>
    public Func<long> Clock { get; set; } = () => Environment.TickCount64;

    /// <summary>The move currently animating, if any.</summary>
    public MoveResult? CurrentMove => _move;

    /// <summary>True while tiles are still moving, popping or revealing.</summary>
    public bool IsAnimating
    {
        get
        {
            long now = Clock();
            return (_move is not null && now - _moveStart < SlideMilliseconds + PopMilliseconds)
                || now - _revealStart < RevealMilliseconds + 300;
        }
    }

    public void BeginMove(MoveResult result)
    {
        _move = result;
        _moveStart = Clock();
    }

    /// <summary>Pops all tiles in one after another, for a new or loaded game.</summary>
    public void BeginReveal()
    {
        _move = null;
        _revealStart = Clock();
    }

    /// <summary>Draws the board as large as fits, at the top of the area.</summary>
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float side = Math.Min(dirtyRect.Width, dirtyRect.Height);
        DrawBoard(canvas, new RectF(dirtyRect.X + (dirtyRect.Width - side) / 2f, dirtyRect.Y, side, side));
    }

    /// <summary>Centre of a cell (fractional cells allowed) inside a board square.</summary>
    public static PointF CellCenter(RectF square, int size, float x, float y)
    {
        var g = new Geometry(square, size);
        return g.CellRect(x, y).Center;
    }

    /// <summary>Size of one cell in pixels for a board square.</summary>
    public static float CellSize(RectF square, int size) => new Geometry(square, size).Cell;

    public void DrawBoard(ICanvas canvas, RectF square)
    {
        int size = Session?.Size ?? 4;
        var g = new Geometry(square, size);

        DrawPlate(canvas, g);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                DrawEmptyCell(canvas, g.CellRect(x, y), g.CellRadius);

        if (Session is not null)
            DrawTiles(canvas, g, Session);

        DrawEndTint(canvas, g);
    }

    private void DrawTiles(ICanvas canvas, Geometry g, GameSession session)
    {
        long now = Clock();
        long sinceMove = now - _moveStart;

        if (_move is not null && sinceMove < SlideMilliseconds)
        {
            // Every tile that was on the board slides toward its new place; merging pairs overlap
            float t = EaseOutCubic(sinceMove / (float)SlideMilliseconds);
            foreach (TileSlide slide in _move.Outcome.Slides.OrderBy(s => s.MergedIntoId is null ? 1 : 0))
                DrawTile(canvas, g, Lerp(slide.FromX, slide.ToX, t), Lerp(slide.FromY, slide.ToY, t), slide.Value, 1f, now);
            return;
        }

        float pop = _move is null ? 1f : Clamp01((sinceMove - SlideMilliseconds) / (float)PopMilliseconds);
        int index = 0;
        foreach (Tile tile in session.Board.Tiles)
        {
            float scale = 1f;
            if (_move is not null && pop < 1f)
            {
                if (_move.Spawned is { } spawned && spawned.Id == tile.Id)
                    scale = EaseOutBack(pop);
                else if (_move.Outcome.Merged.Any(m => m.Id == tile.Id))
                    scale = 1f + 0.2f * MathF.Sin(MathF.PI * pop);
            }

            // Opening reveal: tiles pop in one after another
            long revealAt = _revealStart + index * 45L;
            float reveal = Clamp01((now - revealAt) / (float)RevealMilliseconds);
            if (reveal < 1f)
                scale *= EaseOutBack(reveal);

            DrawTile(canvas, g, tile.X, tile.Y, tile.Value, scale, now);
            index++;
        }
    }

    private static void DrawTile(ICanvas canvas, Geometry g, float cellX, float cellY, int value, float scale, long now)
    {
        if (scale <= 0.02f)
            return;

        RectF cell = g.CellRect(cellX, cellY);
        float w = cell.Width * scale;
        var rect = new RectF(cell.Center.X - w / 2f, cell.Center.Y - w / 2f, w, w);
        TileStyle style = Palette.ForValue(value);
        float radius = g.CellRadius * scale;

        // Big tiles glow, and the very biggest pulse
        if (value >= 128)
        {
            float strength = value >= 2048 ? 1f : value >= 1024 ? 0.8f : value >= 512 ? 0.6f : 0.4f;
            float pulse = value >= 1024 ? 0.75f + 0.25f * MathF.Sin(now * 0.005f) : 1f;
            for (int ring = 3; ring >= 1; ring--)
            {
                float grow = w * 0.045f * ring;
                FillRounded(canvas, rect.Inflate(grow, grow), radius + grow, style.Light.WithAlpha(0.13f * strength * pulse));
            }
        }

        RectF face = CandyPanel(canvas, rect, radius, style.Light, style.Main, style.Edge, lipRatio: 0.075f);

        string text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        float fontSize = face.Height * text.Length switch { 1 => 0.47f, 2 => 0.44f, 3 => 0.36f, 4 => 0.29f, _ => 0.24f };
        if (style.Text == Palette.White)
        {
            GlyphFont.DrawStyled(canvas, text, face.Center.X, face.Center.Y, fontSize,
                Colors.White, Color.FromArgb("#FFF4E0"), Darken(style.Edge, 0.35f), fontSize * 0.09f, Colors.Black.WithAlpha(0.22f));
        }
        else
        {
            GlyphFont.DrawStyled(canvas, text, face.Center.X, face.Center.Y, fontSize,
                Palette.Navy, Palette.Navy, null, 0, Colors.White.WithAlpha(0.55f));
        }

        // 2048 and up: twinkling sparkles
        if (value >= 2048)
        {
            for (int i = 0; i < 3; i++)
            {
                float phase = now * 0.004f + i * 2.1f;
                float a = MathF.Max(0f, MathF.Sin(phase));
                float sx = face.X + face.Width * (0.18f + 0.32f * i);
                float sy = face.Y + face.Height * (i == 1 ? 0.82f : 0.2f);
                Sparkle(canvas, sx, sy, face.Width * 0.09f * (0.6f + 0.4f * a), Colors.White.WithAlpha(0.9f * a));
            }
        }
    }

    private static void DrawEmptyCell(ICanvas canvas, RectF rect, float radius)
    {
        // Slightly sunken: darker top edge, lighter bottom
        FillRounded(canvas, rect, radius, Palette.EmptyCell);
        float stroke = MathF.Max(1f, rect.Height * 0.03f);
        canvas.StrokeColor = Colors.Black.WithAlpha(0.12f);
        canvas.StrokeSize = stroke;
        canvas.DrawRoundedRectangle(rect.Inflate(-stroke / 2f, -stroke / 2f), radius);
    }

    private static void DrawPlate(ICanvas canvas, Geometry g)
    {
        var rect = new RectF(g.Left, g.Top, g.Side, g.Side);
        float lip = g.Side * 0.014f;

        canvas.SaveState();
        canvas.SetShadow(new SizeF(0, g.Side * 0.02f), g.Side * 0.05f, Colors.Black.WithAlpha(0.32f));
        FillRounded(canvas, new RectF(rect.X, rect.Y + lip, rect.Width, rect.Height - lip), g.PlateRadius, Palette.BoardEdge);
        canvas.RestoreState();

        var face = new RectF(rect.X, rect.Y, rect.Width, rect.Height - lip);
        FillGradient(canvas, face, g.PlateRadius, Palette.BoardLight, Palette.BoardMain);
        float rim = MathF.Max(1.5f, g.Side * 0.006f);
        canvas.StrokeColor = Colors.White.WithAlpha(0.18f);
        canvas.StrokeSize = rim;
        canvas.DrawRoundedRectangle(face.Inflate(-rim, -rim), g.PlateRadius);
    }

    private void DrawEndTint(ICanvas canvas, Geometry g)
    {
        if (Session is null || Session.State == RunState.Playing)
            return;

        Color tint = Session.State == RunState.Lost ? Color.FromArgb("#2B2D6E").WithAlpha(0.35f) : Color.FromArgb("#FFE45C").WithAlpha(0.3f);
        FillRounded(canvas, new RectF(g.Left, g.Top, g.Side, g.Side), g.PlateRadius, tint);
    }

    /// <summary>Where the plate and the cells sit inside the board square.</summary>
    private readonly struct Geometry
    {
        public Geometry(RectF square, int size)
        {
            Left = square.X;
            Top = square.Y;
            Side = square.Width;
            Size = size;
            Gap = Side * 0.026f;
            Cell = (Side - Gap * (size + 1)) / size;
            PlateRadius = Side * 0.05f;
            CellRadius = Cell * 0.2f;
        }

        public float Left { get; }
        public float Top { get; }
        public float Side { get; }
        public int Size { get; }
        public float Gap { get; }
        public float Cell { get; }
        public float PlateRadius { get; }
        public float CellRadius { get; }

        public RectF CellRect(float x, float y) =>
            new(Left + Gap + x * (Cell + Gap), Top + Gap + y * (Cell + Gap), Cell, Cell);
    }
}
