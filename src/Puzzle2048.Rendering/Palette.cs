using Microsoft.Maui.Graphics;

namespace Puzzle2048.Rendering;

/// <summary>Colours of one tile: a light top, the main colour, a darker lip and the number colour.</summary>
public readonly record struct TileStyle(Color Light, Color Main, Color Edge, Color Text);

/// <summary>The candy colours shared by the board and the rest of the app.</summary>
public static class Palette
{
    public static readonly Color Navy = Color.FromArgb("#2B2D6E");
    public static readonly Color White = Colors.White;

    public static readonly Color BoardLight = Color.FromArgb("#5A47B5");
    public static readonly Color BoardMain = Color.FromArgb("#4B3A9E");
    public static readonly Color BoardEdge = Color.FromArgb("#33266F");
    public static readonly Color EmptyCell = Color.FromArgb("#6C58C4");

    private static readonly TileStyle[] Styles =
    [
        Make("#FFF3A8", "#FFE066", "#DDB93D", dark: true),   // 2
        Make("#FFD38A", "#FFB347", "#D98C1F", dark: true),   // 4
        Make("#FFB86B", "#FF8F3D", "#D86A16", dark: false),  // 8
        Make("#FF8A80", "#FF5E57", "#D93A34", dark: false),  // 16
        Make("#FF8CB8", "#FF5C9A", "#D63675", dark: false),  // 32
        Make("#F08BE0", "#E04ACB", "#B82AA6", dark: false),  // 64
        Make("#C79BFF", "#A260FF", "#7A3CD6", dark: false),  // 128
        Make("#9AA0FF", "#6C73FF", "#484FD0", dark: false),  // 256
        Make("#7CC4FF", "#3DA0FF", "#1F78D6", dark: false),  // 512
        Make("#6EE3D6", "#1CC9B8", "#0E9E90", dark: false),  // 1024
        Make("#FFE45C", "#FFC400", "#D99A00", dark: true),   // 2048
    ];

    private static readonly TileStyle Giant = Make("#5A5FA8", "#2B2D6E", "#1A1B4A", dark: false);

    /// <summary>Style for a tile value; every value from 4096 up shares the dark "giant" style.</summary>
    public static TileStyle ForValue(int value)
    {
        int index = 0;
        for (int v = value; v > 2; v >>= 1)
            index++;
        return index < Styles.Length ? Styles[index] : Giant;
    }

    private static TileStyle Make(string light, string main, string edge, bool dark) =>
        new(Color.FromArgb(light), Color.FromArgb(main), Color.FromArgb(edge), dark ? Navy : White);
}
