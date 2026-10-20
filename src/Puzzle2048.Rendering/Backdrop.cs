using Microsoft.Maui.Graphics;
using static Puzzle2048.Rendering.Painter;

namespace Puzzle2048.Rendering;

/// <summary>
/// The animated background: a soft gradient from the player's colour, slowly drifting light orbs and twinkling sparkles.
/// Used behind the game screen and the menu.
/// </summary>
public sealed class Backdrop : IDrawable
{
    private static readonly Color Purple = Color.FromArgb("#8E5CF0");

    public Color BaseColor { get; set; } = Color.FromArgb("#7FD3FF");

    public Func<long> Clock { get; set; } = () => Environment.TickCount64;

    public void Draw(ICanvas canvas, RectF dirtyRect) => Paint(canvas, dirtyRect, BaseColor, Clock() / 1000f);

    // Where every orb and sparkle sits, as fractions of the screen. Drawn from a fixed seed once, not every frame.
    private static readonly (float Radius, float X, float Speed, float Phase, float Alpha)[] Orbs;
    private static readonly (float X, float Y, float Size, float Rate)[] Twinkles;

    static Backdrop()
    {
        var random = new Random(2048);
        Orbs = new (float, float, float, float, float)[12];
        for (int i = 0; i < Orbs.Length; i++)
        {
            float radius = 0.05f + (float)random.NextDouble() * 0.12f;
            float x = (float)random.NextDouble();
            float speed = 0.012f + (float)random.NextDouble() * 0.02f;
            float phase = (float)random.NextDouble();
            float alpha = 0.08f + (float)random.NextDouble() * 0.12f;
            Orbs[i] = (radius, x, speed, phase, alpha);
        }

        Twinkles = new (float, float, float, float)[14];
        for (int i = 0; i < Twinkles.Length; i++)
        {
            float x = (float)random.NextDouble();
            float y = (float)random.NextDouble();
            float size = 0.01f + (float)random.NextDouble() * 0.018f;
            float rate = 1.2f + (float)random.NextDouble() * 1.5f;
            Twinkles[i] = (x, y, size, rate);
        }
    }

    public static void Paint(ICanvas canvas, RectF rect, Color baseColor, float seconds)
    {
        Color top = Lighten(baseColor, 0.28f);
        Color bottom = Mix(baseColor, Purple, 0.42f);
        if (Sprites.Enabled)
        {
            // A thin strip of the gradient, stretched over the screen
            var key = new SpriteKey(Sprites.KindBackdrop, Sprites.Argb(baseColor), 0, 0, 0, 0, 0, 0);
            if (!Sprites.TryGet(key, out Sprite strip))
                strip = Sprites.Create(key, 4, 512, 1f, cv => FillBackground(cv, new RectF(0, 0, 4, 512), top, baseColor, bottom));
            strip.Draw(canvas, rect, 1f);
        }
        else
        {
            FillBackground(canvas, rect, top, baseColor, bottom);
        }

        float w = rect.Width;
        float h = rect.Height;

        // Light orbs drifting upwards
        for (int i = 0; i < Orbs.Length; i++)
        {
            var o = Orbs[i];
            float radius = w * o.Radius;
            float x = rect.X + o.X * w;
            float speed = h * o.Speed;
            float phase = o.Phase * (h + radius * 2);
            float y = rect.Bottom + radius - ((phase + seconds * speed) % (h + radius * 2));
            x += MathF.Sin(seconds * 0.3f + i) * w * 0.02f;
            var box = new RectF(x - radius, y - radius, radius * 2, radius * 2);
            Orb(canvas, box, o.Alpha);
        }

        // Twinkling sparkles
        for (int i = 0; i < Twinkles.Length; i++)
        {
            var t = Twinkles[i];
            float twinkle = 0.5f + 0.5f * MathF.Sin(seconds * t.Rate + i * 1.7f);
            Sparkle(canvas, rect.X + t.X * w, rect.Y + t.Y * h, w * t.Size, Colors.White.WithAlpha(0.15f + 0.55f * twinkle));
        }
    }

    private static void FillBackground(ICanvas canvas, RectF rect, Color top, Color middle, Color bottom)
    {
        var paint = new LinearGradientPaint(
            [new PaintGradientStop(0f, top), new PaintGradientStop(0.55f, middle), new PaintGradientStop(1f, bottom)],
            new Point(0.5, 0), new Point(0.5, 1));
        canvas.SetFillPaint(paint, rect);
        canvas.FillRectangle(rect);
    }

    /// <summary>A soft white glow fading to nothing at its edge.</summary>
    private static void Orb(ICanvas canvas, RectF box, float alpha)
    {
        if (Sprites.Enabled)
        {
            var key = new SpriteKey(Sprites.KindOrb, 0, 0, 0, 0, 0, 0, 0);
            if (!Sprites.TryGet(key, out Sprite orb))
                orb = Sprites.Create(key, 128, 128, 1f, cv => OrbVector(cv, new RectF(0, 0, 128, 128), 1f));
            orb.Draw(canvas, box, alpha);
            return;
        }
        OrbVector(canvas, box, alpha);
    }

    private static void OrbVector(ICanvas canvas, RectF box, float alpha)
    {
        var orb = new RadialGradientPaint(
            [new PaintGradientStop(0f, Colors.White.WithAlpha(alpha)), new PaintGradientStop(1f, Colors.White.WithAlpha(0f))]);
        canvas.SetFillPaint(orb, box);
        canvas.FillEllipse(box);
    }
}
