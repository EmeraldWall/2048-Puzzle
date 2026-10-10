using Microsoft.Maui.Graphics;

namespace Puzzle2048.Rendering;

/// <summary>One character of the font as a vector outline, in font units (y points up).</summary>
internal sealed class GlyphShape
{
    private PathF? _path;

    public GlyphShape(int advance, string outline)
    {
        Advance = advance;
        Outline = outline;
    }

    public int Advance { get; }

    public string Outline { get; }

    public PathF Path => _path ??= Parse(Outline);

    private static PathF Parse(string outline)
    {
        var path = new PathF();
        int i = 0;
        while (i < outline.Length)
        {
            char command = outline[i++];
            if (command == 'Z')
            {
                path.Close();
                continue;
            }

            int count = command switch { 'M' or 'L' => 2, 'Q' => 4, 'C' => 6, _ => throw new FormatException($"Unknown path command {command}") };
            var numbers = new float[count];
            for (int n = 0; n < count; n++)
                numbers[n] = ReadNumber(outline, ref i);

            switch (command)
            {
                case 'M': path.MoveTo(numbers[0], numbers[1]); break;
                case 'L': path.LineTo(numbers[0], numbers[1]); break;
                case 'Q': path.QuadTo(numbers[0], numbers[1], numbers[2], numbers[3]); break;
                case 'C': path.CurveTo(numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5]); break;
            }
        }
        return path;
    }

    private static float ReadNumber(string s, ref int i)
    {
        while (i < s.Length && s[i] == ' ')
            i++;
        int start = i;
        if (i < s.Length && s[i] == '-')
            i++;
        while (i < s.Length && char.IsDigit(s[i]))
            i++;
        return float.Parse(s.AsSpan(start, i - start), System.Globalization.CultureInfo.InvariantCulture);
    }
}
