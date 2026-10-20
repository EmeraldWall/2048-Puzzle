using Microsoft.Maui.Graphics;
using static Puzzle2048.Rendering.Painter;

namespace Puzzle2048.Rendering;

/// <summary>A burst of candy pieces flying out of a merged tile. Positions are a pure function of time.</summary>
internal sealed record Burst(long Start, float CellX, float CellY, TileStyle Style, int Seed)
{
    public const long Duration = 650;
    private const int Pieces = 14;

    // Each piece's direction, speed, size, shape and colour, drawn from the seed once instead of every frame
    private (float Angle, float Speed, float Size, int Shape, Color Color)[]? _pieces;

    public bool Alive(long now) => now - Start < Duration;

    public void Draw(ICanvas canvas, PointF center, float cell, long now)
    {
        float t = (now - Start) / 1000f;
        if (t < 0 || t * 1000 >= Duration)
            return;

        float life = t * 1000f / Duration;
        var pieces = _pieces ??= MakePieces();
        for (int i = 0; i < pieces.Length; i++)
        {
            var p = pieces[i];
            float speed = cell * p.Speed;
            float size = cell * p.Size;
            float x = center.X + MathF.Cos(p.Angle) * speed * t;
            float y = center.Y + MathF.Sin(p.Angle) * speed * t + 0.5f * cell * 9f * t * t;
            float alpha = 1f - life * life;
            float s = size * (1f - life * 0.5f);

            switch (p.Shape)
            {
                case 0:
                    Blob(canvas, new RectF(x - s, y - s, s * 2, s * 2), s, p.Color.WithAlpha(alpha));
                    break;
                case 1:
                    Blob(canvas, new RectF(x - s, y - s, s * 2, s * 2), s * 0.4f, p.Color.WithAlpha(alpha), t * 540f + i * 40f);
                    break;
                default:
                    Sparkle(canvas, x, y, s * 1.8f, Colors.White.WithAlpha(alpha));
                    break;
            }
        }
    }

    private (float, float, float, int, Color)[] MakePieces()
    {
        var random = new Random(Seed);
        Color[] colors = [Style.Light, Style.Main, Colors.White, Style.Main];
        var pieces = new (float, float, float, int, Color)[Pieces];
        for (int i = 0; i < Pieces; i++)
        {
            float angle = (float)(random.NextDouble() * Math.PI * 2);
            float speed = 1.6f + (float)random.NextDouble() * 2.6f;
            float size = 0.05f + (float)random.NextDouble() * 0.07f;
            int shape = random.Next(3);
            Color color = colors[random.Next(colors.Length)];
            pieces[i] = (angle, speed, size, shape, color);
        }
        return pieces;
    }
}

/// <summary>"+16" rising from a merged tile.</summary>
internal sealed record FloatingText(long Start, string Text, float CellX, float CellY, TileStyle Style)
{
    public const long Duration = 750;

    public bool Alive(long now) => now - Start < Duration;

    public void Draw(ICanvas canvas, PointF center, float cell, long now)
    {
        float life = (now - Start) / (float)Duration;
        if (life < 0 || life >= 1)
            return;

        float rise = EaseOutCubic(life) * cell * 0.7f;
        float alpha = life < 0.6f ? 1f : 1f - (life - 0.6f) / 0.4f;
        float size = cell * 0.22f * (0.8f + 0.2f * EaseOutBack(MathF.Min(1f, life * 4f)));
        GlyphFont.DrawStyled(canvas, Text, center.X, center.Y - cell * 0.35f - rise, size,
            Colors.White.WithAlpha(alpha), Hex("#FFF2B3").WithAlpha(alpha),
            Darken(Style.Edge, 0.3f).WithAlpha(alpha), size * 0.14f, null);
    }
}

/// <summary>A big praise word over the board, for example "GREAT!", with an optional line under it.</summary>
internal sealed record Praise(long Start, string Word, string? Sub, Color Top, Color Bottom, Color Outline)
{
    public const long Duration = 1250;

    public bool Alive(long now) => now - Start < Duration;

    public void Draw(ICanvas canvas, float cx, float cy, float width, long now)
    {
        float life = (now - Start) / (float)Duration;
        if (life < 0 || life >= 1)
            return;

        float pop = EaseOutBack(MathF.Min(1f, life * 5f));
        if (pop < 0.02f)
            return;
        float alpha = life < 0.75f ? 1f : 1f - (life - 0.75f) / 0.25f;
        float lift = life > 0.75f ? (life - 0.75f) * width * 0.12f : 0f;

        // Drawn at full size and zoomed as a whole, so the letters stay the same cached pictures while it pops in
        canvas.SaveState();
        canvas.Translate(cx, cy - lift);
        canvas.Scale(pop, pop);

        float size = GlyphFont.Fit(Word, width * 0.13f, width * 0.86f);
        GlyphFont.DrawStyled(canvas, Word, 0, 0, size,
            Top.WithAlpha(alpha), Bottom.WithAlpha(alpha), Outline.WithAlpha(alpha), size * 0.12f, Colors.Black.WithAlpha(0.25f * alpha));

        if (Sub is not null)
        {
            float subSize = GlyphFont.Fit(Sub, width * 0.045f, width * 0.8f);
            GlyphFont.DrawStyled(canvas, Sub, 0, size * 0.95f, subSize,
                Colors.White.WithAlpha(alpha), Colors.White.WithAlpha(alpha), Outline.WithAlpha(alpha), subSize * 0.16f, null);
        }
        canvas.RestoreState();
    }
}

/// <summary>Falling confetti for celebrations. Loops while <see cref="Loop"/> is set.</summary>
internal sealed record Confetti(long Start, long Duration, bool Loop)
{
    private static readonly Color[] Colors_ =
    [
        Hex("#FF5C9A"), Hex("#FFC400"), Hex("#3DA0FF"),
        Hex("#43C455"), Hex("#A260FF"), Hex("#FF8F3D"), Colors.White,
    ];

    // Where each piece starts, how it falls and its colour, drawn from a fixed seed once
    private static readonly (float X, float Speed, float Delay, float Sway, float Spin, float Width, Color Color)[] Pieces = MakePieces();

    public bool Alive(long now) => Loop || now - Start < Duration;

    public void Draw(ICanvas canvas, RectF area, long now)
    {
        float t = (now - Start) / 1000f;
        if (t < 0)
            return;

        float fade = Loop ? 1f : Clamp01((Duration - (now - Start)) / 500f);
        float u = area.Width / 100f;
        for (int i = 0; i < Pieces.Length; i++)
        {
            var p = Pieces[i];
            float x0 = area.X + p.X * area.Width;
            float speed = area.Height * p.Speed;
            float sway = u * p.Sway;
            float w = u * p.Width;
            float h = w * 0.5f;

            float local = t - p.Delay;
            if (local < 0)
                continue;
            float travel = area.Height + u * 10f;
            float y = area.Y - u * 5f + local * speed;
            if (Loop)
                y = area.Y - u * 5f + (local * speed) % travel;
            else if (y > area.Bottom + u * 5f)
                continue;

            float x = x0 + MathF.Sin(local * 2.2f + i) * sway;
            Blob(canvas, new RectF(x - w / 2f, y - h / 2f, w, h), h * 0.3f, p.Color.WithAlpha(0.95f * fade), local * p.Spin + i * 23f);
        }
    }

    private static (float, float, float, float, float, float, Color)[] MakePieces()
    {
        var random = new Random(77);
        var pieces = new (float, float, float, float, float, float, Color)[90];
        for (int i = 0; i < pieces.Length; i++)
        {
            float x = (float)random.NextDouble();
            float speed = 0.18f + (float)random.NextDouble() * 0.22f;
            float delay = (float)random.NextDouble() * 1.2f;
            float sway = 1.5f + (float)random.NextDouble() * 3f;
            float spin = 180f + (float)random.NextDouble() * 360f;
            float w = 1.0f + (float)random.NextDouble() * 1.2f;
            Color color = Colors_[random.Next(Colors_.Length)];
            pieces[i] = (x, speed, delay, sway, spin, w, color);
        }
        return pieces;
    }
}
