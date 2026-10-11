using Microsoft.Maui.Graphics;

namespace Puzzle2048.Rendering;

/// <summary>Small drawing helpers shared by the board and the screen: candy panels, gradients and easing.</summary>
public static class Painter
{
    public static void FillRounded(ICanvas canvas, RectF rect, float radius, Color color)
    {
        canvas.FillColor = color;
        canvas.FillRoundedRectangle(rect, radius);
    }

    public static void FillGradient(ICanvas canvas, RectF rect, float radius, Color top, Color bottom)
    {
        var paint = new LinearGradientPaint(
            [new PaintGradientStop(0f, top), new PaintGradientStop(1f, bottom)],
            new Point(0.5, 0), new Point(0.5, 1));
        canvas.SetFillPaint(paint, rect);
        canvas.FillRoundedRectangle(rect, radius);
    }

    /// <summary>
    /// A chunky candy shape: soft shadow, darker lip, gradient face, inner rim light and a glossy top.
    /// Returns the face rectangle (where content goes). <paramref name="pressed"/> sinks the face onto the lip.
    /// </summary>
    public static RectF CandyPanel(ICanvas canvas, RectF rect, float radius, Color light, Color main, Color edge,
        bool pressed = false, float lipRatio = 0.09f, bool shadow = true, float alpha = 1f)
    {
        float lip = MathF.Max(2f, rect.Height * lipRatio);
        float sink = pressed ? lip * 0.7f : 0f;

        if (shadow)
        {
            canvas.SaveState();
            canvas.SetShadow(new SizeF(0, lip * 0.9f), lip * 1.6f, Colors.Black.WithAlpha(0.28f * alpha));
            FillRounded(canvas, new RectF(rect.X, rect.Y + lip, rect.Width, rect.Height - lip), radius, edge.WithAlpha(alpha));
            canvas.RestoreState();
        }
        else
        {
            FillRounded(canvas, new RectF(rect.X, rect.Y + lip, rect.Width, rect.Height - lip), radius, edge.WithAlpha(alpha));
        }

        var face = new RectF(rect.X, rect.Y + sink, rect.Width, rect.Height - lip);
        FillGradient(canvas, face, radius, light.WithAlpha(alpha), main.WithAlpha(alpha));

        // Rim light along the top edge
        float rim = MathF.Max(1f, face.Height * 0.025f);
        canvas.StrokeColor = Colors.White.WithAlpha(0.35f * alpha);
        canvas.StrokeSize = rim;
        canvas.DrawRoundedRectangle(face.Inflate(-rim / 2f, -rim / 2f), radius);

        // Glossy highlight across the top third
        var gloss = new RectF(face.X + face.Width * 0.08f, face.Y + face.Height * 0.06f, face.Width * 0.84f, face.Height * 0.38f);
        var paint = new LinearGradientPaint(
            [new PaintGradientStop(0f, Colors.White.WithAlpha(0.42f * alpha)), new PaintGradientStop(1f, Colors.White.WithAlpha(0f))],
            new Point(0.5, 0), new Point(0.5, 1));
        canvas.SetFillPaint(paint, gloss);
        canvas.FillRoundedRectangle(gloss, MathF.Min(radius, gloss.Height / 2f));

        return face;
    }

    /// <summary>A four-point sparkle star.</summary>
    public static void Sparkle(ICanvas canvas, float cx, float cy, float radius, Color color)
    {
        var path = new PathF();
        for (int k = 0; k < 8; k++)
        {
            double angle = Math.PI / 4 * k - Math.PI / 2;
            float r = k % 2 == 0 ? radius : radius * 0.28f;
            float x = cx + (float)(r * Math.Cos(angle));
            float y = cy + (float)(r * Math.Sin(angle));
            if (k == 0) path.MoveTo(x, y); else path.LineTo(x, y);
        }
        path.Close();
        canvas.FillColor = color;
        canvas.FillPath(path);
    }

    /// <summary>A five-point star, filled or as an empty slot.</summary>
    public static void Star(ICanvas canvas, float cx, float cy, float radius, Color fill, Color? outline = null, float outlineWidth = 0)
    {
        var path = new PathF();
        for (int k = 0; k < 10; k++)
        {
            double angle = -Math.PI / 2 + k * Math.PI / 5;
            float r = k % 2 == 0 ? radius : radius * 0.48f;
            float x = cx + (float)(r * Math.Cos(angle));
            float y = cy + (float)(r * Math.Sin(angle));
            if (k == 0) path.MoveTo(x, y); else path.LineTo(x, y);
        }
        path.Close();

        if (outline is not null && outlineWidth > 0)
        {
            canvas.StrokeColor = outline;
            canvas.StrokeSize = outlineWidth * 2f;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.DrawPath(path);
        }
        canvas.FillColor = fill;
        canvas.FillPath(path);
    }

    public static Color Lighten(Color c, float amount) =>
        new(c.Red + (1 - c.Red) * amount, c.Green + (1 - c.Green) * amount, c.Blue + (1 - c.Blue) * amount, c.Alpha);

    public static Color Darken(Color c, float amount) =>
        new(c.Red * (1 - amount), c.Green * (1 - amount), c.Blue * (1 - amount), c.Alpha);

    public static Color Mix(Color a, Color b, float t) =>
        new(a.Red + (b.Red - a.Red) * t, a.Green + (b.Green - a.Green) * t, a.Blue + (b.Blue - a.Blue) * t, a.Alpha + (b.Alpha - a.Alpha) * t);

    public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    public static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - Clamp01(t), 3f);

    public static float EaseOutBack(float t)
    {
        t = Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
    }
}
