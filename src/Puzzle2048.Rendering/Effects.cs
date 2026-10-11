using Microsoft.Maui.Graphics;
using static Puzzle2048.Rendering.Painter;

namespace Puzzle2048.Rendering;

/// <summary>A burst of candy pieces flying out of a merged tile. Positions are a pure function of time.</summary>
internal sealed record Burst(long Start, float CellX, float CellY, TileStyle Style, int Seed)
{
    public const long Duration = 650;

    public bool Alive(long now) => now - Start < Duration;

    public void Draw(ICanvas canvas, PointF center, float cell, long now)
    {
        float t = (now - Start) / 1000f;
        if (t < 0 || t * 1000 >= Duration)
            return;

        float life = t * 1000f / Duration;
        var random = new Random(Seed);
        Color[] colors = [Style.Light, Style.Main, Colors.White, Style.Main];
        for (int i = 0; i < 14; i++)
        {
            float angle = (float)(random.NextDouble() * Math.PI * 2);
            float speed = cell * (1.6f + (float)random.NextDouble() * 2.6f);
            float size = cell * (0.05f + (float)random.NextDouble() * 0.07f);
            int shape = random.Next(3);
            Color color = colors[random.Next(colors.Length)];
            float x = center.X + MathF.Cos(angle) * speed * t;
            float y = center.Y + MathF.Sin(angle) * speed * t + 0.5f * cell * 9f * t * t;
            float alpha = 1f - life * life;
            float s = size * (1f - life * 0.5f);

            switch (shape)
            {
                case 0:
                    canvas.FillColor = color.WithAlpha(alpha);
                    canvas.FillCircle(x, y, s);
                    break;
                case 1:
                    canvas.SaveState();
                    canvas.Rotate(t * 540f + i * 40f, x, y);
                    FillRounded(canvas, new RectF(x - s, y - s, s * 2, s * 2), s * 0.4f, color.WithAlpha(alpha));
                    canvas.RestoreState();
                    break;
                default:
                    Sparkle(canvas, x, y, s * 1.8f, Colors.White.WithAlpha(alpha));
                    break;
            }
        }
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
            Colors.White.WithAlpha(alpha), Color.FromArgb("#FFF2B3").WithAlpha(alpha),
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
        float alpha = life < 0.75f ? 1f : 1f - (life - 0.75f) / 0.25f;
        float lift = life > 0.75f ? (life - 0.75f) * width * 0.12f : 0f;

        float size = GlyphFont.Fit(Word, width * 0.13f, width * 0.86f) * pop;
        GlyphFont.DrawStyled(canvas, Word, cx, cy - lift, size,
            Top.WithAlpha(alpha), Bottom.WithAlpha(alpha), Outline.WithAlpha(alpha), size * 0.12f, Colors.Black.WithAlpha(0.25f * alpha));

        if (Sub is not null)
        {
            float subSize = GlyphFont.Fit(Sub, width * 0.045f, width * 0.8f) * pop;
            GlyphFont.DrawStyled(canvas, Sub, cx, cy + size * 0.95f - lift, subSize,
                Colors.White.WithAlpha(alpha), Colors.White.WithAlpha(alpha), Outline.WithAlpha(alpha), subSize * 0.16f, null);
        }
    }
}

/// <summary>Falling confetti for celebrations. Loops while <see cref="Loop"/> is set.</summary>
internal sealed record Confetti(long Start, long Duration, bool Loop)
{
    private static readonly Color[] Colors_ =
    [
        Color.FromArgb("#FF5C9A"), Color.FromArgb("#FFC400"), Color.FromArgb("#3DA0FF"),
        Color.FromArgb("#43C455"), Color.FromArgb("#A260FF"), Color.FromArgb("#FF8F3D"), Colors.White,
    ];

    public bool Alive(long now) => Loop || now - Start < Duration;

    public void Draw(ICanvas canvas, RectF area, long now)
    {
        float t = (now - Start) / 1000f;
        if (t < 0)
            return;

        float fade = Loop ? 1f : Clamp01((Duration - (now - Start)) / 500f);
        var random = new Random(77);
        float u = area.Width / 100f;
        for (int i = 0; i < 90; i++)
        {
            float x0 = area.X + (float)random.NextDouble() * area.Width;
            float speed = area.Height * (0.18f + (float)random.NextDouble() * 0.22f);
            float delay = (float)random.NextDouble() * 1.2f;
            float sway = u * (1.5f + (float)random.NextDouble() * 3f);
            float spin = 180f + (float)random.NextDouble() * 360f;
            float w = u * (1.0f + (float)random.NextDouble() * 1.2f);
            float h = w * 0.5f;
            Color color = Colors_[random.Next(Colors_.Length)];

            float local = t - delay;
            if (local < 0)
                continue;
            float travel = area.Height + u * 10f;
            float y = area.Y - u * 5f + local * speed;
            if (Loop)
                y = area.Y - u * 5f + (local * speed) % travel;
            else if (y > area.Bottom + u * 5f)
                continue;

            float x = x0 + MathF.Sin(local * 2.2f + i) * sway;
            canvas.SaveState();
            canvas.Rotate(local * spin + i * 23f, x, y);
            FillRounded(canvas, new RectF(x - w / 2f, y - h / 2f, w, h), h * 0.3f, color.WithAlpha(0.95f * fade));
            canvas.RestoreState();
        }
    }
}
