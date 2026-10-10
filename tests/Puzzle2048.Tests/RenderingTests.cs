using Puzzle2048.Rendering;

namespace Puzzle2048.Tests;

public class RenderingTests
{
    [Fact]
    public void TextWidthGrowsWithLengthAndSize()
    {
        float one = GlyphFont.Measure("2", 40);
        float four = GlyphFont.Measure("2048", 40);

        Assert.True(one > 0);
        Assert.True(four > one * 3);
        Assert.Equal(GlyphFont.Measure("2048", 40) * 2, GlyphFont.Measure("2048", 80), 0.01f);
    }

    [Fact]
    public void EveryCharacterTheGameDrawsHasAGlyph()
    {
        // Tile numbers and the banner texts the game can show
        string all = "0123456789 COMBO x3 +120 NEW TILE 256! GOAL COMPLETE! NO CHARGES LEFT NEED MORE TILES";
        foreach (char c in all)
            Assert.True(GlyphFont.Measure(c.ToString(), 10) > 0, $"No glyph for '{c}'");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(2048)]
    [InlineData(4096)]
    [InlineData(131072)]
    public void EveryTileValueHasAStyle(int value)
    {
        TileStyle style = Palette.ForValue(value);
        Assert.NotEqual(style.Main, style.Edge);
    }

    [Fact]
    public void BigTilesShareTheDarkStyleAndSmallOnesDiffer()
    {
        Assert.Equal(Palette.ForValue(4096), Palette.ForValue(8192));
        Assert.NotEqual(Palette.ForValue(2), Palette.ForValue(4));
        Assert.NotEqual(Palette.ForValue(2048), Palette.ForValue(4096));
    }
}
