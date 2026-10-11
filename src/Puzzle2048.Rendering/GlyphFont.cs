using Microsoft.Maui.Graphics;

namespace Puzzle2048.Rendering;

/// <summary>
/// Draws text as vector shapes taken from the Fredoka Bold font, with optional outline, gradient and shadow.
/// The game screen uses it for every word and number, so text looks identical on every phone.
/// </summary>
public static class GlyphFont
{
    /// <summary>Width of the text in pixels at the given size (size = height of a capital letter).</summary>
    public static float Measure(string text, float size)
    {
        float scale = size / GlyphData.CapHeight;
        float width = 0;
        foreach (char c in text)
            width += Lookup(c).Advance * scale;
        return width;
    }

    /// <summary>Largest size up to <paramref name="size"/> at which the text fits <paramref name="maxWidth"/>.</summary>
    public static float Fit(string text, float size, float maxWidth)
    {
        float width = Measure(text, size);
        return width > maxWidth && width > 0 ? size * maxWidth / width : size;
    }

    /// <summary>Draws plain text centred on (centerX, centerY). <paramref name="size"/> is the height of a capital letter.</summary>
    public static void DrawCentered(ICanvas canvas, string text, float centerX, float centerY, float size, Color color) =>
        DrawStyled(canvas, text, centerX, centerY, size, color, color, null, 0, null);

    /// <summary>Plain text with a soft drop shadow underneath.</summary>
    public static void DrawCenteredWithShadow(ICanvas canvas, string text, float centerX, float centerY, float size, Color color, Color shadow) =>
        DrawStyled(canvas, text, centerX, centerY, size, color, color, null, 0, shadow);

    /// <summary>
    /// Game-style text: a vertical gradient fill, an outline of <paramref name="outlineWidth"/> pixels and a drop shadow.
    /// </summary>
    public static void DrawStyled(ICanvas canvas, string text, float centerX, float centerY, float size,
        Color top, Color bottom, Color? outline, float outlineWidth, Color? shadow, HorizontalAlignment align = HorizontalAlignment.Center)
    {
        if (string.IsNullOrEmpty(text) || size <= 0.5f)
            return;

        float scale = size / GlyphData.CapHeight;
        float width = Measure(text, size);
        float startX = align switch
        {
            HorizontalAlignment.Left => centerX,
            HorizontalAlignment.Right => centerX - width,
            _ => centerX - width / 2f,
        };
        float baseline = centerY + size / 2f;

        if (shadow is not null)
            DrawRun(canvas, text, startX, baseline + size * 0.09f, scale, shadow, shadow, outline is null ? null : shadow, outlineWidth);

        DrawRun(canvas, text, startX, baseline, scale, top, bottom, outline, outlineWidth);
    }

    private static void DrawRun(ICanvas canvas, string text, float x, float baseline, float scale, Color top, Color bottom, Color? outline, float outlineWidth)
    {
        bool gradient = !top.Equals(bottom);
        canvas.SaveState();
        foreach (char c in text)
        {
            GlyphShape glyph = Lookup(c);
            canvas.SaveState();
            canvas.Translate(x, baseline);
            canvas.Scale(scale, -scale);

            if (outline is not null && outlineWidth > 0)
            {
                canvas.StrokeColor = outline;
                canvas.StrokeSize = outlineWidth * 2f / scale;
                canvas.StrokeLineJoin = LineJoin.Round;
                canvas.DrawPath(glyph.Path);
            }

            if (gradient)
            {
                // Font units grow upwards, so the "top" colour sits at the cap height
                var paint = new LinearGradientPaint(
                    [new PaintGradientStop(0f, bottom), new PaintGradientStop(1f, top)],
                    new Point(0.5, 0), new Point(0.5, 1));
                canvas.SetFillPaint(paint, new RectF(0, 0, glyph.Advance, GlyphData.CapHeight));
            }
            else
            {
                canvas.FillColor = top;
            }

            canvas.FillPath(glyph.Path);
            canvas.RestoreState();
            x += glyph.Advance * scale;
        }
        canvas.RestoreState();
    }

    private static GlyphShape Lookup(char c)
    {
        if (GlyphData.Shapes.TryGetValue(c, out GlyphShape? shape))
            return shape;
        if (GlyphData.Shapes.TryGetValue(char.ToUpperInvariant(c), out shape))
            return shape;
        return GlyphData.Shapes[' '];
    }
}
