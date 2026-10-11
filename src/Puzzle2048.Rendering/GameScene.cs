using Microsoft.Maui.Graphics;
using Puzzle2048.Core;
using static Puzzle2048.Rendering.Painter;

namespace Puzzle2048.Rendering;

/// <summary>
/// The whole game screen in one drawing: background, top bar, score panels, goal or clock bar, the board,
/// power-ups, effects and the end-of-run card. Drawing everything here keeps the look identical on every phone,
/// lets the preview tool render real screenshots, and lets effects fly freely across the screen.
/// </summary>
public sealed class GameScene : IDrawable
{
    private static readonly Color Gold = Color.FromArgb("#FFD93D");
    private static readonly Color Orange = Color.FromArgb("#FF8F3D");
    private static readonly Color Plum = Color.FromArgb("#5B2A86");
    private static readonly Color ShadowText = Colors.Black.WithAlpha(0.25f);

    private readonly List<Burst> _bursts = [];
    private readonly List<FloatingText> _floaters = [];
    private readonly List<Praise> _praises = [];
    private readonly List<(RectF Rect, SceneAction Action)> _hits = [];
    private Confetti? _confetti;
    private (long Start, float Amplitude) _shake = (-1_000_000, 0);
    private (string Text, long Start)? _toast;
    private SceneAction _pressed;
    private long _pressStart = -1_000_000;
    private double _shownScore = -1;
    private long _lastFrame;
    private long _scoreBumpStart = -1_000_000;
    private long _resultStart;

    public GameScene()
    {
        Board.Clock = () => Clock();
    }

    public BoardRenderer Board { get; } = new();

    public GameSession? Session
    {
        get => Board.Session;
        set => Board.Session = value;
    }

    public HudState Hud { get; set; } = new();

    public ResultCard? Result { get; private set; }

    public Color BackgroundColor { get; set; } = Color.FromArgb("#7FD3FF");

    public Func<long> Clock { get; set; } = () => Environment.TickCount64;

    /// <summary>True while anything moves, so the page should redraw at full speed.</summary>
    public bool IsAnimating
    {
        get
        {
            long now = Clock();
            return Board.IsAnimating
                || _bursts.Count > 0 || _floaters.Count > 0 || _praises.Count > 0
                || _confetti is not null || now - _shake.Start < 300 || now - _pressStart < 200
                || (_toast is { } t && now - t.Start < 1600)
                || (Session is not null && Math.Abs(_shownScore - Hud.Score) > 0.5)
                || (Result is not null && now - _resultStart < 1500);
        }
    }

    // ------------------------------------------------------------------ events from the page

    /// <summary>Animates a move: slides, merge bursts, "+points", praise words and screen shake.</summary>
    public void OnMove(MoveResult result)
    {
        long now = Clock();
        Board.BeginMove(result);
        long mergeTime = now + BoardRenderer.SlideMilliseconds;

        foreach (Tile merged in result.Outcome.Merged)
        {
            TileStyle style = Palette.ForValue(merged.Value);
            _bursts.Add(new Burst(mergeTime, merged.X, merged.Y, style, merged.Id * 7919));
            _floaters.Add(new FloatingText(mergeTime, $"+{merged.Value}", merged.X, merged.Y, style));
        }

        if (result.Outcome.MergeCount > 0)
            _scoreBumpStart = mergeTime;

        int biggest = result.Outcome.BiggestMerge;
        if (result.GoalJustMet)
        {
            Praise("GOAL COMPLETE!", null, Gold, Orange, Plum, mergeTime);
            Celebrate(2600);
        }
        else if (biggest >= GameSession.MilestoneTile)
        {
            TileStyle style = Palette.ForValue(biggest);
            Praise($"{biggest}!", "NEW TILE", Lighten(style.Light, 0.2f), style.Main, Darken(style.Edge, 0.35f), mergeTime);
        }
        else if (result.ComboBonus > 0)
        {
            Praise(PraiseWord(result.Combo), $"x{result.Combo} COMBO  +{result.ComboBonus}", Gold, Orange, Plum, mergeTime);
        }

        if (biggest >= 512)
            _shake = (mergeTime, biggest >= 2048 ? 1.4f : biggest >= 1024 ? 1.0f : 0.7f);
    }

    /// <summary>Pops all tiles in, for a new or loaded game.</summary>
    public void Reveal()
    {
        Board.BeginReveal();
        _bursts.Clear();
        _floaters.Clear();
        _praises.Clear();
        _shownScore = -1;
    }

    public void Praise(string word, string? sub, Color top, Color bottom, Color outline, long? start = null) =>
        _praises.Add(new Praise(start ?? Clock(), word, sub, top, bottom, outline));

    /// <summary>A small message pill under the board, for example "Need more tiles".</summary>
    public void Toast(string text) => _toast = (text, Clock());

    public void Celebrate(long milliseconds) => _confetti = new Confetti(Clock(), milliseconds, Loop: false);

    public void ShowResult(ResultCard card)
    {
        Result = card;
        _resultStart = Clock();
        _confetti = card.Celebrate ? new Confetti(_resultStart, 0, Loop: true) : null;
    }

    public void HideResult()
    {
        Result = null;
        if (_confetti is { Loop: true })
            _confetti = null;
    }

    /// <summary>Which button is at this point, using the layout of the last frame.</summary>
    public SceneAction HitTest(PointF point)
    {
        for (int i = _hits.Count - 1; i >= 0; i--)
            if (_hits[i].Rect.Contains(point))
                return _hits[i].Action;
        return SceneAction.None;
    }

    /// <summary>Shows the press animation on a button.</summary>
    public void Press(SceneAction action)
    {
        _pressed = action;
        _pressStart = Clock();
    }

    public static string PraiseWord(int combo) => combo switch
    {
        <= 2 => "NICE!",
        3 => "GREAT!",
        4 => "SUPER!",
        5 or 6 => "AWESOME!",
        _ => "UNSTOPPABLE!",
    };

    // ------------------------------------------------------------------ drawing

    public void Draw(ICanvas canvas, RectF rect)
    {
        long now = Clock();
        Prune(now);
        _hits.Clear();

        Backdrop.Paint(canvas, rect, BackgroundColor, now / 1000f);

        var layout = new Layout(rect, Hud, Session?.Size ?? 4);
        UpdateShownScore(now);

        DrawTopBar(canvas, layout, now);
        DrawScores(canvas, layout, now);
        if (layout.Strip is { } strip)
            DrawStrip(canvas, strip, now);

        // Board and its effects shake together on big merges
        PointF shake = ShakeOffset(now, layout.U);
        canvas.SaveState();
        canvas.Translate(shake.X, shake.Y);
        Board.DrawBoard(canvas, layout.Board);
        DrawBoardEffects(canvas, layout, now);
        canvas.RestoreState();

        DrawBottom(canvas, layout, now);

        foreach (Praise praise in _praises)
            praise.Draw(canvas, layout.Board.Center.X, layout.Board.Y + layout.Board.Height * 0.34f, rect.Width, now);

        DrawToast(canvas, layout, now);

        if (Result is not null)
            DrawResult(canvas, rect, layout, now);

        _confetti?.Draw(canvas, rect, now);
        _lastFrame = now;
    }

    private void DrawTopBar(ICanvas canvas, Layout l, long now)
    {
        RoundButton(canvas, l.BackButton, SceneAction.Back, IconKind.Back,
            Color.FromArgb("#FF8CC4"), Color.FromArgb("#FF4F9A"), Color.FromArgb("#C42872"), true, now);
        RoundButton(canvas, l.RestartButton, SceneAction.Restart, IconKind.Restart,
            Color.FromArgb("#FFB04D"), Color.FromArgb("#FF8A3D"), Color.FromArgb("#C8581A"), true, now);

        DrawLogo(canvas, l.Logo, now);

        // Mode chip: a frosted pill with the mode name or the date
        RectF chip = l.Chip;
        FillRounded(canvas, chip, chip.Height / 2f, Colors.White.WithAlpha(0.28f));
        canvas.StrokeColor = Colors.White.WithAlpha(0.55f);
        canvas.StrokeSize = MathF.Max(1f, l.U * 0.35f);
        canvas.DrawRoundedRectangle(chip, chip.Height / 2f);
        float size = GlyphFont.Fit(Hud.ModeChip, l.U * 3.6f, chip.Width - l.U * 5f);
        GlyphFont.DrawStyled(canvas, Hud.ModeChip, chip.Center.X, chip.Center.Y, size,
            Colors.White, Colors.White, Palette.Navy.WithAlpha(0.55f), size * 0.12f, null);
    }

    /// <summary>The game's logo: four candy tiles spelling 2048, gently bobbing.</summary>
    private static void DrawLogo(ICanvas canvas, RectF area, long now)
    {
        (string Digit, int Value)[] tiles = [("2", 8), ("0", 32), ("4", 512), ("8", 2048)];
        float[] tilt = [-7f, 5f, -4f, 7f];
        float tile = area.Height * 0.86f;
        float gap = area.Height * 0.12f;
        float total = tile * 4 + gap * 3;
        float x = area.Center.X - total / 2f;
        for (int i = 0; i < 4; i++)
        {
            float bob = MathF.Sin(now * 0.0025f + i * 0.9f) * area.Height * 0.04f;
            var rect = new RectF(x, area.Center.Y - tile / 2f + bob, tile, tile);
            TileStyle style = Palette.ForValue(tiles[i].Value);
            canvas.SaveState();
            canvas.Rotate(tilt[i], rect.Center.X, rect.Center.Y);
            RectF face = CandyPanel(canvas, rect, tile * 0.24f, style.Light, style.Main, style.Edge, lipRatio: 0.1f);
            float size = face.Height * 0.52f;
            GlyphFont.DrawStyled(canvas, tiles[i].Digit, face.Center.X, face.Center.Y, size,
                Colors.White, Color.FromArgb("#FFF2D6"), Darken(style.Edge, 0.35f), size * 0.12f, ShadowText);
            canvas.RestoreState();
            x += tile + gap;
        }
    }

    private void DrawScores(ICanvas canvas, Layout l, long now)
    {
        float bump = 1f + 0.06f * MathF.Sin(MathF.PI * Clamp01((now - _scoreBumpStart) / 260f));
        Panel(canvas, l.ScorePanel, "SCORE", FormatNumber((long)Math.Round(_shownScore < 0 ? Hud.Score : _shownScore)),
            Color.FromArgb("#FFB36B"), Color.FromArgb("#FF7A3D"), Color.FromArgb("#D4541A"), bump, l.U);

        bool record = Hud.Score > 0 && Hud.Score >= Hud.Best;
        Panel(canvas, l.BestPanel, record ? "NEW BEST" : "BEST", FormatNumber(Math.Max(Hud.Best, Hud.Score)),
            Color.FromArgb("#C29BFF"), Color.FromArgb("#8E5CF0"), Color.FromArgb("#6535C4"), 1f, l.U);
    }

    private static void Panel(ICanvas canvas, RectF rect, string label, string value, Color light, Color main, Color edge, float scale, float u)
    {
        RectF scaled = Scale(rect, scale);
        RectF face = CandyPanel(canvas, scaled, u * 3.2f, light, main, edge, lipRatio: 0.1f);
        GlyphFont.DrawStyled(canvas, label, face.Center.X, face.Y + face.Height * 0.27f, u * 2.5f,
            Colors.White.WithAlpha(0.9f), Colors.White.WithAlpha(0.9f), null, 0, null);
        float size = GlyphFont.Fit(value, u * 5.6f, face.Width - u * 5f);
        GlyphFont.DrawStyled(canvas, value, face.Center.X, face.Y + face.Height * 0.64f, size,
            Colors.White, Color.FromArgb("#FFF2D6"), Darken(edge, 0.3f), size * 0.1f, ShadowText);
    }

    private void DrawStrip(ICanvas canvas, RectF strip, long now)
    {
        float u = strip.Height / 7.5f;
        FillRounded(canvas, strip, strip.Height / 2f, Palette.Navy.WithAlpha(0.35f));

        if (Hud.StripFraction is { } fraction)
        {
            float f = Clamp01(fraction);
            var fill = new RectF(strip.X + u * 0.8f, strip.Y + u * 0.8f, MathF.Max(strip.Height - u * 1.6f, (strip.Width - u * 1.6f) * f), strip.Height - u * 1.6f);
            Color light, main;
            if (Hud.StripUrgent)
            {
                float pulse = 0.5f + 0.5f * MathF.Sin(now * 0.012f);
                light = Mix(Color.FromArgb("#FF8A80"), Color.FromArgb("#FFC1B8"), pulse);
                main = Color.FromArgb("#E53935");
            }
            else if (Hud.StripDone)
            {
                light = Color.FromArgb("#9BEF7A");
                main = Color.FromArgb("#2FB344");
            }
            else
            {
                light = Color.FromArgb("#7FE7FF");
                main = Color.FromArgb("#2E9BFF");
            }
            FillGradient(canvas, fill, fill.Height / 2f, light, main);
            var gloss = new RectF(fill.X + fill.Height * 0.3f, fill.Y + fill.Height * 0.12f, MathF.Max(0, fill.Width - fill.Height * 0.6f), fill.Height * 0.32f);
            FillRounded(canvas, gloss, gloss.Height / 2f, Colors.White.WithAlpha(0.35f));
        }

        if (Hud.StripText is { } text)
        {
            float size = GlyphFont.Fit(text, strip.Height * 0.42f, strip.Width - strip.Height);
            GlyphFont.DrawStyled(canvas, text, strip.Center.X, strip.Center.Y, size,
                Colors.White, Colors.White, Palette.Navy.WithAlpha(0.8f), size * 0.13f, null);
        }
    }

    private void DrawBoardEffects(ICanvas canvas, Layout l, long now)
    {
        int size = Session?.Size ?? 4;
        float cell = BoardRenderer.CellSize(l.Board, size);
        foreach (Burst burst in _bursts)
            burst.Draw(canvas, BoardRenderer.CellCenter(l.Board, size, burst.CellX, burst.CellY), cell, now);
        foreach (FloatingText floater in _floaters)
            floater.Draw(canvas, BoardRenderer.CellCenter(l.Board, size, floater.CellX, floater.CellY), cell, now);
    }

    private void DrawBottom(ICanvas canvas, Layout l, long now)
    {
        if (Hud.ShowPowerUps)
        {
            RoundButton(canvas, l.UndoButton, SceneAction.Undo, IconKind.Undo,
                Color.FromArgb("#6FE0FF"), Color.FromArgb("#2EB4F2"), Color.FromArgb("#1388C2"), Hud.CanUndo, now);
            RoundButton(canvas, l.TrashButton, SceneAction.Trash, IconKind.Trash,
                Color.FromArgb("#9BEF7A"), Color.FromArgb("#43C455"), Color.FromArgb("#2A912F"), Hud.CanTrash, now);

            float labelY = l.UndoButton.Bottom + l.U * 2.6f;
            GlyphFont.DrawStyled(canvas, "UNDO", l.UndoButton.Center.X, labelY, l.U * 2.6f,
                Colors.White, Colors.White, Palette.Navy.WithAlpha(0.6f), l.U * 0.35f, null);
            GlyphFont.DrawStyled(canvas, "CLEAR", l.TrashButton.Center.X, labelY, l.U * 2.6f,
                Colors.White, Colors.White, Palette.Navy.WithAlpha(0.6f), l.U * 0.35f, null);

            // Charges badge on the power-up
            float r = l.U * 3f;
            var badge = new PointF(l.TrashButton.Right - r * 0.5f, l.TrashButton.Y + r * 0.5f);
            canvas.FillColor = Darken(Color.FromArgb("#E53935"), 0.25f);
            canvas.FillCircle(badge.X, badge.Y + l.U * 0.4f, r);
            canvas.FillColor = Color.FromArgb("#FF5252");
            canvas.FillCircle(badge, r);
            GlyphFont.DrawCentered(canvas, Hud.TrashCharges.ToString(System.Globalization.CultureInfo.InvariantCulture), badge.X, badge.Y, r * 0.9f, Colors.White);
        }
        else if (Hud.BottomHint is { } hint)
        {
            float size = GlyphFont.Fit(hint, l.U * 3.3f, l.Board.Width);
            GlyphFont.DrawStyled(canvas, hint, l.Board.Center.X, l.HintY, size,
                Colors.White, Colors.White, Palette.Navy.WithAlpha(0.55f), size * 0.12f, null);
        }
    }

    private void DrawToast(ICanvas canvas, Layout l, long now)
    {
        if (_toast is not { } toast)
            return;

        float life = (now - toast.Start) / 1600f;
        if (life >= 1f)
            return;

        float alpha = life < 0.8f ? 1f : 1f - (life - 0.8f) / 0.2f;
        float pop = EaseOutBack(MathF.Min(1f, life * 6f));
        float size = l.U * 3.6f * pop;
        float width = GlyphFont.Measure(toast.Text, size) + l.U * 8f;
        var pill = new RectF(l.Board.Center.X - width / 2f, l.Board.Bottom - l.U * 14f, width, l.U * 8f * pop);
        FillRounded(canvas, new RectF(pill.X, pill.Y + l.U * 0.8f, pill.Width, pill.Height), pill.Height / 2f, Colors.Black.WithAlpha(0.25f * alpha));
        FillRounded(canvas, pill, pill.Height / 2f, Colors.White.WithAlpha(0.96f * alpha));
        GlyphFont.DrawCentered(canvas, toast.Text, pill.Center.X, pill.Center.Y, size, Palette.Navy.WithAlpha(alpha));
    }

    private void DrawResult(ICanvas canvas, RectF rect, Layout l, long now)
    {
        ResultCard card = Result!;
        float u = l.U;
        float appear = EaseOutCubic((now - _resultStart) / 250f);
        float pop = EaseOutBack((now - _resultStart) / 380f);

        // Everything behind the card is dimmed and no longer clickable
        _hits.Clear();
        canvas.FillColor = Color.FromArgb("#1B1446").WithAlpha(0.62f * appear);
        canvas.FillRectangle(rect);

        float cardWidth = MathF.Min(rect.Width - u * 10f, u * 88f);
        float starsHeight = card.Stars >= 0 ? u * 17f : 0f;
        float linesHeight = card.Lines.Count * u * 6.2f;
        int buttons = card.Secondary is null ? 2 : 3;
        float buttonsHeight = u * 15f + (buttons - 1) * u * 13f;
        float cardHeight = u * 15f + starsHeight + linesHeight + u * 4f + buttonsHeight + u * 5f;
        var cardRect = new RectF(rect.Center.X - cardWidth / 2f, rect.Center.Y - cardHeight / 2f, cardWidth, cardHeight);

        canvas.SaveState();
        canvas.Translate(cardRect.Center.X, cardRect.Center.Y);
        canvas.Scale(pop, pop);
        canvas.Translate(-cardRect.Center.X, -cardRect.Center.Y);

        // Card body
        canvas.SaveState();
        canvas.SetShadow(new SizeF(0, u * 1.6f), u * 4f, Colors.Black.WithAlpha(0.4f));
        FillRounded(canvas, new RectF(cardRect.X, cardRect.Y + u * 1.2f, cardRect.Width, cardRect.Height), u * 7f, Color.FromArgb("#C9B8F5"));
        canvas.RestoreState();
        FillGradient(canvas, cardRect, u * 7f, Colors.White, Color.FromArgb("#F2ECFF"));

        // Ribbon with the title
        var ribbon = new RectF(cardRect.X - u * 3f, cardRect.Y - u * 4f, cardRect.Width + u * 6f, u * 15f);
        Color ribbonLight = card.Celebrate ? Color.FromArgb("#FFE45C") : Color.FromArgb("#FF8CC4");
        Color ribbonMain = card.Celebrate ? Color.FromArgb("#FFB300") : Color.FromArgb("#FF4F9A");
        Color ribbonEdge = card.Celebrate ? Color.FromArgb("#C77C00") : Color.FromArgb("#C42872");
        RectF ribbonFace = CandyPanel(canvas, ribbon, u * 6f, ribbonLight, ribbonMain, ribbonEdge, lipRatio: 0.1f);
        float titleSize = GlyphFont.Fit(card.Title, u * 6.6f, ribbonFace.Width - u * 8f);
        GlyphFont.DrawStyled(canvas, card.Title, ribbonFace.Center.X, ribbonFace.Center.Y, titleSize,
            Colors.White, Color.FromArgb("#FFF2D6"), Darken(ribbonEdge, 0.3f), titleSize * 0.11f, ShadowText);

        float y = cardRect.Y + u * 15f;

        // Stars pop in one after another
        if (card.Stars >= 0)
        {
            for (int i = 0; i < 3; i++)
            {
                float cx = cardRect.Center.X + (i - 1) * u * 15f;
                float cy = y + u * 7f - (i == 1 ? u * 2f : 0f);
                float radius = u * (i == 1 ? 7.5f : 6.2f);
                Star(canvas, cx, cy + u * 0.8f, radius, Color.FromArgb("#B9A6E8"));
                Star(canvas, cx, cy, radius, Color.FromArgb("#D9CCF7"));
                if (i < card.Stars)
                {
                    float starPop = EaseOutBack((now - _resultStart - 350 - i * 260) / 320f);
                    if (starPop > 0f)
                    {
                        Star(canvas, cx, cy + u * 0.8f, radius * starPop, Color.FromArgb("#D99A00"));
                        Star(canvas, cx, cy, radius * starPop, Gold);
                        Sparkle(canvas, cx - radius * 0.3f, cy - radius * 0.25f, radius * 0.25f * starPop, Colors.White.WithAlpha(0.9f));
                    }
                }
            }
            y += starsHeight;
        }

        foreach (string line in card.Lines)
        {
            float size = GlyphFont.Fit(line, u * 3.9f, cardRect.Width - u * 10f);
            GlyphFont.DrawCentered(canvas, line, cardRect.Center.X, y + u * 3f, size, Color.FromArgb("#4A3F7A"));
            y += u * 6.2f;
        }
        y += u * 4f;

        float buttonWidth = cardRect.Width - u * 14f;
        var primary = new RectF(cardRect.Center.X - buttonWidth / 2f, y, buttonWidth, u * 13f);
        TextButton(canvas, primary, SceneAction.Primary, card.Primary, Color.FromArgb("#9BEF7A"), Color.FromArgb("#43C455"), Color.FromArgb("#2A912F"), u, now, big: true);
        y += u * 15f;

        if (card.Secondary is not null)
        {
            var secondary = new RectF(cardRect.Center.X - buttonWidth / 2f, y, buttonWidth, u * 11f);
            TextButton(canvas, secondary, SceneAction.Secondary, card.Secondary, Color.FromArgb("#7FCBFF"), Color.FromArgb("#3E9CFF"), Color.FromArgb("#1F6FCC"), u, now);
            y += u * 13f;
        }

        var tertiary = new RectF(cardRect.Center.X - buttonWidth / 2f, y, buttonWidth, u * 11f);
        TextButton(canvas, tertiary, SceneAction.Tertiary, card.Tertiary, Color.FromArgb("#FF8CC4"), Color.FromArgb("#FF4F9A"), Color.FromArgb("#C42872"), u, now);

        canvas.RestoreState();
    }

    // ------------------------------------------------------------------ helpers

    private void RoundButton(ICanvas canvas, RectF rect, SceneAction action, IconKind icon, Color light, Color main, Color edge, bool enabled, long now)
    {
        _hits.Add((rect, action));
        float alpha = enabled ? 1f : 0.5f;
        RectF face = CandyPanel(canvas, rect, rect.Width / 2f, light, main, edge, IsPressed(action, now), lipRatio: 0.1f, alpha: alpha);
        float inset = face.Width * 0.24f;
        Icons.Draw(canvas, icon, face.Inflate(-inset, -inset), Colors.White.WithAlpha(alpha));
    }

    private void TextButton(ICanvas canvas, RectF rect, SceneAction action, string text, Color light, Color main, Color edge, float u, long now, bool big = false)
    {
        _hits.Add((rect, action));
        RectF face = CandyPanel(canvas, rect, rect.Height / 2f, light, main, edge, IsPressed(action, now), lipRatio: 0.12f);
        float size = GlyphFont.Fit(text, u * (big ? 5f : 4.3f), face.Width - u * 6f);
        GlyphFont.DrawStyled(canvas, text, face.Center.X, face.Center.Y, size,
            Colors.White, Color.FromArgb("#FFF6E6"), Darken(edge, 0.3f), size * 0.11f, ShadowText);
    }

    private bool IsPressed(SceneAction action, long now) => _pressed == action && now - _pressStart < 160;

    private void UpdateShownScore(long now)
    {
        long target = Hud.Score;
        if (_shownScore < 0 || target < _shownScore || _lastFrame == 0)
        {
            _shownScore = target;
            return;
        }

        float dt = Math.Clamp(now - _lastFrame, 0, 100);
        _shownScore += (target - _shownScore) * Math.Min(1.0, dt / 110.0);
        if (Math.Abs(target - _shownScore) < 0.5)
            _shownScore = target;
    }

    private PointF ShakeOffset(long now, float u)
    {
        float life = (now - _shake.Start) / 280f;
        if (life < 0 || life >= 1)
            return PointF.Zero;
        float decay = 1f - life;
        return new PointF(MathF.Sin(now * 0.09f) * u * _shake.Amplitude * decay, MathF.Cos(now * 0.11f) * u * _shake.Amplitude * decay);
    }

    private void Prune(long now)
    {
        _bursts.RemoveAll(b => !b.Alive(now));
        _floaters.RemoveAll(f => !f.Alive(now));
        _praises.RemoveAll(p => !p.Alive(now));
        if (_confetti is not null && !_confetti.Alive(now))
            _confetti = null;
    }

    private static RectF Scale(RectF rect, float scale)
    {
        if (Math.Abs(scale - 1f) < 0.001f)
            return rect;
        float w = rect.Width * scale, h = rect.Height * scale;
        return new RectF(rect.Center.X - w / 2f, rect.Center.Y - h / 2f, w, h);
    }

    private static string FormatNumber(long value) => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Where everything sits for a given screen size. All sizes come from u = 1% of the width.</summary>
    private readonly struct Layout
    {
        public Layout(RectF rect, HudState hud, int boardSize)
        {
            U = rect.Width / 100f;
            float u = U;
            float margin = u * 4.5f;

            // Header: round buttons in the corners, the candy logo between them, the mode chip under the logo
            float top = rect.Y + u * 3f;
            float button = u * 13f;
            BackButton = new RectF(rect.X + margin, top, button, button);
            RestartButton = new RectF(rect.Right - margin - button, top, button, button);
            Logo = new RectF(rect.Center.X - u * 25f, top - u * 0.5f, u * 50f, u * 13f);
            Chip = new RectF(rect.Center.X - u * 23f, top + button + u * 2.5f, u * 46f, u * 8f);

            float scoresTop = Chip.Bottom + u * 3.5f;
            float panelWidth = (rect.Width - margin * 2 - u * 4f) / 2f;
            ScorePanel = new RectF(rect.X + margin, scoresTop, panelWidth, u * 17f);
            BestPanel = new RectF(rect.X + margin + panelWidth + u * 4f, scoresTop, panelWidth, u * 17f);

            float headerBottom = scoresTop + u * 17f;
            if (hud.StripText is not null || hud.StripFraction is not null)
            {
                Strip = new RectF(rect.X + margin, headerBottom + u * 3.5f, rect.Width - margin * 2, u * 7.5f);
                headerBottom = Strip.Value.Bottom;
            }
            else
            {
                Strip = null;
            }

            // The board takes the width and sits in the middle of the space that is left
            float bottomArea = hud.ShowPowerUps ? u * 25f : u * 11f;
            float spaceTop = headerBottom + u * 4f;
            float spaceBottom = rect.Bottom - bottomArea;
            float available = spaceBottom - spaceTop;
            float side = MathF.Max(u * 40f, MathF.Min(rect.Width - margin * 2, available));
            float boardY = spaceTop + MathF.Max(0f, (available - side) * 0.45f);
            Board = new RectF(rect.Center.X - side / 2f, boardY, side, side);

            float power = u * 15f;
            float powerTop = Board.Bottom + u * 4.5f;
            UndoButton = new RectF(rect.Center.X - u * 12f - power, powerTop, power, power);
            TrashButton = new RectF(rect.Center.X + u * 12f, powerTop, power, power);
            HintY = Board.Bottom + u * 6.5f;
        }

        public RectF Logo { get; }
        public float U { get; }
        public RectF BackButton { get; }
        public RectF RestartButton { get; }
        public RectF Chip { get; }
        public RectF ScorePanel { get; }
        public RectF BestPanel { get; }
        public RectF? Strip { get; }
        public RectF Board { get; }
        public RectF UndoButton { get; }
        public RectF TrashButton { get; }
        public float HintY { get; }
    }
}
