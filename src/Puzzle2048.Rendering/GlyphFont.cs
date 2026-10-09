using Microsoft.Maui.Graphics;

namespace Puzzle2048.Rendering;

/// <summary>
/// Draws text as vector shapes taken from the Fredoka Bold font. The game uses it for the tile numbers and
/// the pop-up banner, so they look identical on every phone and do not depend on font loading.
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

    /// <summary>Draws the text centred on (centerX, centerY). <paramref name="size"/> is the height of a capital letter or digit.</summary>
    public static void DrawCentered(ICanvas canvas, string text, float centerX, float centerY, float size, Color color)
    {
        float scale = size / GlyphData.CapHeight;
        float x = centerX - Measure(text, size) / 2f;
        // Font units grow upwards, the canvas grows downwards, so flip and put the baseline half a cap-height below centre
        float baseline = centerY + size / 2f;

        canvas.SaveState();
        canvas.FillColor = color;
        foreach (char c in text)
        {
            GlyphShape glyph = Lookup(c);
            canvas.SaveState();
            canvas.Translate(x, baseline);
            canvas.Scale(scale, -scale);
            canvas.FillPath(glyph.Path);
            canvas.RestoreState();
            x += glyph.Advance * scale;
        }
        canvas.RestoreState();
    }

    /// <summary>Draws the text with a soft drop shadow underneath.</summary>
    public static void DrawCenteredWithShadow(ICanvas canvas, string text, float centerX, float centerY, float size, Color color, Color shadow)
    {
        float offset = size * 0.07f;
        DrawCentered(canvas, text, centerX, centerY + offset, size, shadow);
        DrawCentered(canvas, text, centerX, centerY, size, color);
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
