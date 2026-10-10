using Microsoft.Maui.Graphics;
using Puzzle2048.Core;

namespace Puzzle2048.Rendering;

/// <summary>
/// Draws the board, its tiles and the pop-up banner, and animates slides, merges and new tiles.
/// It has no dependency on the screen, so the same code runs in the app and in the preview tool.
/// </summary>
public sealed class BoardRenderer : IDrawable
{
    public const long SlideMilliseconds = 110;
    public const long PopMilliseconds = 160;
    public const long BannerMilliseconds = 1300;
    public const long RevealMilliseconds = 260;

    private MoveResult? _move;
    private long _moveStart;
    private string? _banner;
    private long _bannerStart;
    private long _revealStart = -1_000_000;

    /// <summary>The game to draw. Set it again after starting a new game or loading a saved one.</summary>
    public GameSession? Session { get; set; }

    /// <summary>Milliseconds clock. Tests and the preview tool replace it to freeze time.</summary>
    public Func<long> Clock { get; set; } = () => Environment.TickCount64;

    /// <summary>True while something is moving, so the screen should keep redrawing.</summary>
    public bool IsAnimating
    {
        get
        {
            long now = Clock();
            return (_move is not null && now - _moveStart < SlideMilliseconds + PopMilliseconds)
                || (_banner is not null && now - _bannerStart < BannerMilliseconds)
                || now - _revealStart < RevealMilliseconds;
        }
    }

    /// <summary>Starts the slide, merge and spawn animation for a move that was just played.</summary>
    public void BeginMove(MoveResult result)
    {
        _move = result;
        _moveStart = Clock();
        if (result.Banner is not null)
        {
            _banner = result.Banner;
            _bannerStart = _moveStart;
        }
    }

    /// <summary>Pops all tiles in, for a new game or a loaded one.</summary>
    public void BeginReveal()
    {
        _move = null;
        _banner = null;
        _revealStart = Clock();
    }

    public void ShowBanner(string text)
    {
        _banner = text;
        _bannerStart = Clock();
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        int size = Session?.Size ?? 4;
        float side = Math.Min(dirtyRect.Width, dirtyRect.Height);
        float left = dirtyRect.X + (dirtyRect.Width - side) / 2f;
        float top = dirtyRect.Y;
        BoardGeometry g = new(left, top, side, size);

        DrawBoardPlate(canvas, g);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                FillRounded(canvas, g.CellRect(x, y), g.CellRadius, Palette.EmptyCell);

        if (Session is not null)
            DrawTiles(canvas, g, Session);

        DrawEndTint(canvas, g);
        DrawBanner(canvas, g);
    }

    private void DrawTiles(ICanvas canvas, BoardGeometry g, GameSession session)
    {
        long now = Clock();
        long sinceMove = now - _moveStart;
        float reveal = Clamp01((now - _revealStart) / (float)RevealMilliseconds);

        if (_move is not null && sinceMove < SlideMilliseconds)
        {
            // Phase 1: every tile that was on the board slides toward its new place
            float t = EaseOutCubic(sinceMove / (float)SlideMilliseconds);
            foreach (TileSlide slide in _move.Outcome.Slides)
            {
                float x = Lerp(slide.FromX, slide.ToX, t);
                float y = Lerp(slide.FromY, slide.ToY, t);
                DrawTile(canvas, g, x, y, slide.Value, 1f);
            }
            return;
        }

        // Phase 2: the finished board; new and merged tiles pop
        float pop = _move is null ? 1f : Clamp01((sinceMove - SlideMilliseconds) / (float)PopMilliseconds);
        foreach (Tile tile in session.Board.Tiles)
        {
            float scale = 1f;
            if (_move is not null)
            {
                if (_move.Spawned is { } spawned && spawned.Id == tile.Id)
                    scale = EaseOutBack(pop);
                else if (_move.Outcome.Merged.Any(m => m.Id == tile.Id))
                    scale = 1f + 0.18f * MathF.Sin(MathF.PI * pop);
            }
            if (reveal < 1f)
                scale *= EaseOutBack(reveal);

            DrawTile(canvas, g, tile.X, tile.Y, tile.Value, scale);
        }
    }

    private static void DrawTile(ICanvas canvas, BoardGeometry g, float cellX, float cellY, int value, float scale)
    {
        if (scale <= 0.01f)
            return;

        RectF rect = g.CellRect(cellX, cellY);
        float cx = rect.Center.X;
        float cy = rect.Center.Y;
        float w = rect.Width * scale;
        float h = rect.Height * scale;
        var scaled = new RectF(cx - w / 2f, cy - h / 2f, w, h);

        TileStyle style = Palette.ForValue(value);
        float radius = g.CellRadius * scale;
        float lip = scaled.Height * 0.07f;

        // Lip first, then the gradient face above it
        FillRounded(canvas, new RectF(scaled.X, scaled.Y + lip, scaled.Width, scaled.Height - lip), radius, style.Edge);
        var face = new RectF(scaled.X, scaled.Y, scaled.Width, scaled.Height - lip);
        FillGradient(canvas, face, radius, style.Light, style.Main);

        // Soft gloss on the top edge
        var gloss = new RectF(face.X + face.Width * 0.1f, face.Y + face.Height * 0.07f, face.Width * 0.8f, face.Height * 0.2f);
        FillRounded(canvas, gloss, gloss.Height / 2f, Colors.White.WithAlpha(0.22f));

        // Number: shrinks as it gets longer so it always fits
        string text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        float fontSize = face.Height * text.Length switch { 1 => 0.46f, 2 => 0.44f, 3 => 0.36f, 4 => 0.29f, _ => 0.24f };
        Color shadow = style.Text == Palette.White ? Colors.Black.WithAlpha(0.28f) : Colors.White.WithAlpha(0.35f);
        GlyphFont.DrawCenteredWithShadow(canvas, text, face.Center.X, face.Center.Y, fontSize, style.Text, shadow);
    }

    private static void DrawBoardPlate(ICanvas canvas, BoardGeometry g)
    {
        float lip = g.Side * 0.012f;
        FillRounded(canvas, new RectF(g.Left, g.Top + lip, g.Side, g.Side - lip), g.PlateRadius, Palette.BoardEdge);
        FillGradient(canvas, new RectF(g.Left, g.Top, g.Side, g.Side - lip), g.PlateRadius, Palette.BoardLight, Palette.BoardMain);
    }

    private void DrawEndTint(ICanvas canvas, BoardGeometry g)
    {
        if (Session is null || Session.State == RunState.Playing)
            return;

        Color tint = Session.State == RunState.Lost ? Color.FromArgb("#E4DCFF").WithAlpha(0.55f) : Color.FromArgb("#FFE45C").WithAlpha(0.35f);
        FillRounded(canvas, new RectF(g.Left, g.Top, g.Side, g.Side), g.PlateRadius, tint);
    }

    private void DrawBanner(ICanvas canvas, BoardGeometry g)
    {
        if (_banner is null)
            return;

        long elapsed = Clock() - _bannerStart;
        if (elapsed >= BannerMilliseconds)
            return;

        // Pops in quickly, stays, then fades over the last 400 ms
        float alpha = Clamp01((BannerMilliseconds - elapsed) / 400f);
        float pop = EaseOutBack(Clamp01(elapsed / 160f));

        float textSize = g.Side * 0.058f;
        float maxWidth = g.Side * 0.86f;
        float textWidth = GlyphFont.Measure(_banner, textSize);
        if (textWidth > maxWidth - textSize * 1.6f)
        {
            textSize *= (maxWidth - textSize * 1.6f) / textWidth;
            textWidth = GlyphFont.Measure(_banner, textSize);
        }

        float pillWidth = (textWidth + textSize * 1.6f) * pop;
        float pillHeight = textSize * 2.1f * pop;
        float cx = g.Left + g.Side / 2f;
        float top = g.Top + g.Side * 0.03f;
        var pill = new RectF(cx - pillWidth / 2f, top, pillWidth, pillHeight);

        FillRounded(canvas, new RectF(pill.X, pill.Y + pillHeight * 0.12f, pill.Width, pill.Height), pillHeight / 2f, Colors.Black.WithAlpha(0.3f * alpha));
        FillRounded(canvas, pill, pillHeight / 2f, Colors.White.WithAlpha(0.96f * alpha));
        GlyphFont.DrawCentered(canvas, _banner, cx, pill.Center.Y, textSize * pop, Palette.Navy.WithAlpha(alpha));
    }

    private static void FillRounded(ICanvas canvas, RectF rect, float radius, Color color)
    {
        canvas.FillColor = color;
        canvas.FillRoundedRectangle(rect, radius);
    }

    private static void FillGradient(ICanvas canvas, RectF rect, float radius, Color top, Color bottom)
    {
        var paint = new LinearGradientPaint(new[]
        {
            new PaintGradientStop(0f, top),
            new PaintGradientStop(1f, bottom),
        }, new Point(0.5, 0), new Point(0.5, 1));
        canvas.SetFillPaint(paint, rect);
        canvas.FillRoundedRectangle(rect, radius);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
    }

    /// <summary>Where the plate and the cells sit inside the drawing area.</summary>
    private readonly struct BoardGeometry
    {
        public BoardGeometry(float left, float top, float side, int size)
        {
            Left = left;
            Top = top;
            Side = side;
            Size = size;
            // The gap is a fixed share of the cell so 4x4, 5x5 and 6x6 all look balanced
            Gap = side * 0.028f;
            Cell = (side - Gap * (size + 1)) / size;
            PlateRadius = side * 0.045f;
            CellRadius = Cell * 0.16f;
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
