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

    public static void Paint(ICanvas canvas, RectF rect, Color baseColor, float seconds)
    {
        Color top = Lighten(baseColor, 0.28f);
        Color bottom = Mix(baseColor, Purple, 0.42f);
        var paint = new LinearGradientPaint(
            [new PaintGradientStop(0f, top), new PaintGradientStop(0.55f, baseColor), new PaintGradientStop(1f, bottom)],
            new Point(0.5, 0), new Point(0.5, 1));
        canvas.SetFillPaint(paint, rect);
        canvas.FillRectangle(rect);

        float w = rect.Width;
        float h = rect.Height;
        var random = new Random(2048);

        // Light orbs drifting upwards
        for (int i = 0; i < 12; i++)
        {
            float radius = w * (0.05f + (float)random.NextDouble() * 0.12f);
            float x = rect.X + (float)random.NextDouble() * w;
            float speed = h * (0.012f + (float)random.NextDouble() * 0.02f);
            float phase = (float)random.NextDouble() * (h + radius * 2);
            float y = rect.Bottom + radius - ((phase + seconds * speed) % (h + radius * 2));
            x += MathF.Sin(seconds * 0.3f + i) * w * 0.02f;
            float alpha = 0.08f + (float)random.NextDouble() * 0.12f;
            var orb = new RadialGradientPaint(
                [new PaintGradientStop(0f, Colors.White.WithAlpha(alpha)), new PaintGradientStop(1f, Colors.White.WithAlpha(0f))]);
            var box = new RectF(x - radius, y - radius, radius * 2, radius * 2);
            canvas.SetFillPaint(orb, box);
            canvas.FillEllipse(box);
        }

        // Twinkling sparkles
        for (int i = 0; i < 14; i++)
        {
            float x = rect.X + (float)random.NextDouble() * w;
            float y = rect.Y + (float)random.NextDouble() * h;
            float size = w * (0.01f + (float)random.NextDouble() * 0.018f);
            float twinkle = 0.5f + 0.5f * MathF.Sin(seconds * (1.2f + (float)random.NextDouble() * 1.5f) + i * 1.7f);
            Sparkle(canvas, x, y, size, Colors.White.WithAlpha(0.15f + 0.55f * twinkle));
        }
    }
}
