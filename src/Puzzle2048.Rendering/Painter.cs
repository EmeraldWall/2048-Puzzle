using Microsoft.Maui.Graphics;

namespace Puzzle2048.Rendering;

/// <summary>Small drawing helpers shared by the board and the screen: candy panels, gradients and easing.</summary>
public static class Painter
{
    private static readonly Dictionary<string, Color> HexColors = [];

    /// <summary>A colour from "#RRGGBB" text, parsed once and then reused, so drawing a frame allocates nothing for it.</summary>
    public static Color Hex(string hex)
    {
        if (!HexColors.TryGetValue(hex, out Color? color))
            HexColors[hex] = color = Color.FromArgb(hex);
        return color;
    }

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
    /// <paramref name="zoom"/> scales the whole panel about its centre, for bounces, without making a new sprite.
    /// </summary>
    public static RectF CandyPanel(ICanvas canvas, RectF rect, float radius, Color light, Color main, Color edge,
        bool pressed = false, float lipRatio = 0.09f, bool shadow = true, float alpha = 1f, float zoom = 1f)
    {
        if (!Sprites.Enabled)
        {
            RectF zoomed = Zoom(rect, zoom);
            return CandyPanelVector(canvas, zoomed, radius * zoom, light, main, edge, pressed, lipRatio, shadow, alpha);
        }

        // The sprite holds the panel at full opacity with room for the shadow around it
        float lip = LipOf(rect, lipRatio);
        float pad = shadow ? lip * 2.6f + 1f : 1f;
        int flags = (pressed ? 1 : 0) | (shadow ? 2 : 0) | (Sprites.Milli(lipRatio) << 2);
        var key = new SpriteKey(Sprites.KindPanel, Px(rect.Width), Px(rect.Height), Px(radius * 10f),
            Sprites.Argb(light), Sprites.Argb(main), Sprites.Argb(edge), flags);
        if (!Sprites.TryGet(key, out Sprite sprite))
        {
            var local = new RectF(pad, pad, rect.Width, rect.Height);
            sprite = Sprites.Create(key, rect.Width + pad * 2, rect.Height + pad * 2, cv =>
                CandyPanelVector(cv, local, radius, light, main, edge, pressed, lipRatio, shadow, 1f));
        }

        SizeF size = Sprites.SizeOf(sprite);
        RectF dest = Zoom(new RectF(rect.X - pad, rect.Y - pad, size.Width, size.Height), zoom, rect.Center);
        sprite.Draw(canvas, dest, alpha);
        return Zoom(PanelFace(rect, lipRatio, pressed), zoom, rect.Center);
    }

    private static float LipOf(RectF rect, float lipRatio) => MathF.Max(2f, rect.Height * lipRatio);

    /// <summary>The face rectangle <see cref="CandyPanel"/> returns for a panel, without drawing it.</summary>
    public static RectF PanelFace(RectF rect, float lipRatio, bool pressed = false)
    {
        float lip = LipOf(rect, lipRatio);
        float sink = pressed ? lip * 0.7f : 0f;
        return new RectF(rect.X, rect.Y + sink, rect.Width, rect.Height - lip);
    }

    private static int Px(float units) => (int)MathF.Round(units * Sprites.PixelScale);

    /// <summary>Scales a rectangle about its own centre, or about <paramref name="about"/>.</summary>
    public static RectF Zoom(RectF rect, float zoom, PointF? about = null)
    {
        if (zoom == 1f)
            return rect;
        PointF c = about ?? rect.Center;
        return new RectF(c.X + (rect.X - c.X) * zoom, c.Y + (rect.Y - c.Y) * zoom, rect.Width * zoom, rect.Height * zoom);
    }

    private static RectF CandyPanelVector(ICanvas canvas, RectF rect, float radius, Color light, Color main, Color edge,
        bool pressed, float lipRatio, bool shadow, float alpha)
    {
        float lip = LipOf(rect, lipRatio);
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

    /// <summary>
    /// A filled rounded rectangle (or circle) in one colour, optionally turned about its centre, for particles,
    /// confetti and glows. On Android, drawing these as shapes creates native objects every time, and turning
    /// them needs a saved canvas state (three more); a stamped sprite turned and turned back needs none.
    /// </summary>
    public static void Blob(ICanvas canvas, RectF rect, float radius, Color color, float degrees = 0f)
    {
        if (rect.Width <= 0.05f || rect.Height <= 0.05f || color.Alpha <= 0.004f)
            return;

        if (!Sprites.Enabled)
        {
            if (degrees != 0f)
            {
                canvas.SaveState();
                canvas.Rotate(degrees, rect.Center.X, rect.Center.Y);
            }
            FillRounded(canvas, rect, radius, color);
            if (degrees != 0f)
                canvas.RestoreState();
            return;
        }

        // One sprite per colour, shape and power-of-two size; a 1 px clear border keeps the edges smooth
        float longest = MathF.Max(rect.Width, rect.Height);
        int px = (int)Math.Clamp(System.Numerics.BitOperations.RoundUpToPowerOf2((uint)MathF.Ceiling(longest * Sprites.PixelScale + 2f)), 8u, 256u);
        float aspect = rect.Width / rect.Height;
        float roundness = Math.Clamp(radius / MathF.Min(rect.Width, rect.Height), 0f, 0.5f);
        Color solid = color.WithAlpha(1f);
        var key = new SpriteKey(Sprites.KindBlob, px, Sprites.Milli(aspect), Sprites.Milli(roundness), Sprites.Argb(solid), 0, 0, 0);
        int w = aspect >= 1f ? px : Math.Max(4, (int)MathF.Round(px * aspect));
        int h = aspect >= 1f ? Math.Max(4, (int)MathF.Round(px / aspect)) : px;
        if (!Sprites.TryGet(key, out Sprite sprite))
        {
            sprite = Sprites.Create(key, w, h, 1f, cv =>
            {
                var inner = new RectF(1, 1, w - 2, h - 2);
                FillRounded(cv, inner, roundness * MathF.Min(inner.Width, inner.Height), solid);
            });
        }

        // The sprite's clear border maps onto the rectangle's edge
        float bx = rect.Width / (w - 2), by = rect.Height / (h - 2);
        var dest = new RectF(rect.X - bx, rect.Y - by, rect.Width + 2 * bx, rect.Height + 2 * by);
        if (degrees != 0f)
            canvas.Rotate(degrees, rect.Center.X, rect.Center.Y);
        sprite.Draw(canvas, dest, color.Alpha);
        if (degrees != 0f)
            canvas.Rotate(-degrees, rect.Center.X, rect.Center.Y);
    }

    /// <summary>A four-point sparkle star.</summary>
    public static void Sparkle(ICanvas canvas, float cx, float cy, float radius, Color color)
    {
        if (radius <= 0.05f || color.Alpha <= 0.004f)
            return;

        // White sparkles (nearly all of them) come from a sprite per power-of-two size, stamped at any opacity
        if (Sprites.Enabled && color.Red >= 0.999f && color.Green >= 0.999f && color.Blue >= 0.999f)
        {
            int px = (int)Math.Clamp(System.Numerics.BitOperations.RoundUpToPowerOf2((uint)MathF.Ceiling(radius * 2f * Sprites.PixelScale + 2f)), 8u, 256u);
            var key = new SpriteKey(Sprites.KindSparkle, px, 0, 0, 0, 0, 0, 0);
            float unit = px / 2f;
            if (!Sprites.TryGet(key, out Sprite sprite))
                sprite = Sprites.Create(key, px, px, 1f, cv => SparkleVector(cv, unit, unit, unit - 1f, Colors.White));
            float half = radius * unit / (unit - 1f);
            sprite.Draw(canvas, new RectF(cx - half, cy - half, half * 2f, half * 2f), color.Alpha);
            return;
        }

        SparkleVector(canvas, cx, cy, radius, color);
    }

    private static void SparkleVector(ICanvas canvas, float cx, float cy, float radius, Color color)
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
