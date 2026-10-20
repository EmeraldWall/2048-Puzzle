using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Skia;
using Puzzle2048.Rendering;

/// <summary>Sprite backend for the preview tool, so it can draw exactly like the app does with its sprite cache.</summary>
internal sealed class SkiaSprite : Sprite
{
    private readonly IImage _image;

    private SkiaSprite(IImage image, int width, int height)
    {
        _image = image;
        PixelWidth = width;
        PixelHeight = height;
    }

    public override int PixelWidth { get; }

    public override int PixelHeight { get; }

    public static void Install(float pixelScale)
    {
        Sprites.Clear();
        Sprites.PixelScale = pixelScale;
        Sprites.Factory = (width, height, draw) =>
        {
            var context = new SkiaBitmapExportContext(width, height, 1f);
            draw(context.Canvas);
            return new SkiaSprite(context.Image, width, height);
        };
    }

    public static void Uninstall()
    {
        Sprites.Factory = null;
        Sprites.Clear();
    }

    /// <summary>For the benchmark: skip the Skia stamp, to see what the game's own code allocates.</summary>
    public static bool StampsOff { get; set; }

    public override void Draw(ICanvas canvas, RectF dest, float alpha)
    {
        if (StampsOff)
            return;
        canvas.Alpha = alpha;
        canvas.DrawImage(_image, dest.X, dest.Y, dest.Width, dest.Height);
        canvas.Alpha = 1f;
    }
}
