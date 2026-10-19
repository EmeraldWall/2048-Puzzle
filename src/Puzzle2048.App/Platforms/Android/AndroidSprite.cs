using System.Runtime.CompilerServices;
using Android.Graphics;
using Paint = Android.Graphics.Paint;
using Microsoft.Maui.Graphics.Platform;
using Puzzle2048.Rendering;
using ICanvas = Microsoft.Maui.Graphics.ICanvas;
using RectF = Microsoft.Maui.Graphics.RectF;
using ScalingCanvas = Microsoft.Maui.Graphics.ScalingCanvas;

namespace Puzzle2048.App.Platforms.Android;

/// <summary>
/// The Android side of the sprite cache: a bitmap drawn once in software, then stamped straight onto the hardware
/// canvas with bilinear filtering. One paint and one rectangle are reused for every stamp, so a frame creates
/// no Java objects at all.
/// </summary>
internal sealed class AndroidSprite : Sprite
{
    private static readonly Paint StampPaint = new(PaintFlags.FilterBitmap | PaintFlags.AntiAlias);
    private static readonly global::Android.Graphics.RectF Dest = new();

    private readonly Bitmap _bitmap;
    private readonly PlatformImage _image;

    private AndroidSprite(Bitmap bitmap)
    {
        _bitmap = bitmap;
        _image = new PlatformImage(bitmap);
        PixelWidth = bitmap.Width;
        PixelHeight = bitmap.Height;
    }

    public override int PixelWidth { get; }

    public override int PixelHeight { get; }

    /// <summary>Turns the sprite cache on for this phone's screen density.</summary>
    public static void Install(float density)
    {
        Sprites.Clear();
        Sprites.PixelScale = density;
        Sprites.Factory = (width, height, draw) =>
        {
            using var context = new PlatformBitmapExportContext(width, height, 1f, disposeBitmap: false);
            draw(context.Canvas);
            Bitmap bitmap = context.Bitmap;
            // Upload to the GPU now rather than on the first frame that shows it
            bitmap.PrepareToDraw();
            return new AndroidSprite(bitmap);
        };
    }

    public override void Draw(ICanvas canvas, RectF dest, float alpha)
    {
        if (alpha <= 0.004f)
            return;

        // The game view hands out a ScalingCanvas over the platform canvas. It scales coordinates itself and keeps
        // translations and rotations on the Android canvas, so the stamp goes in at the scaled coordinates.
        if (canvas is ScalingCanvas { ParentCanvas: PlatformCanvas { Canvas: { } native } } scaling)
        {
            // MAUI rounds the view to whole dp and stretches x and y by slightly different amounts to fill it
            float sx = scaling.GetScale(), sy = ScaleY(scaling);
            float left = dest.X * sx, top = dest.Y * sy, width = dest.Width * sx, height = dest.Height * sy;

            // Within 1 % of its own size a sprite is stamped pixel for pixel on whole pixels, centred where it
            // belongs, so it stays exactly as sharp as vector drawing
            if (MathF.Abs(width / PixelWidth - 1f) < 0.01f && MathF.Abs(height / PixelHeight - 1f) < 0.01f)
            {
                left = MathF.Round(left + (width - PixelWidth) / 2f);
                top = MathF.Round(top + (height - PixelHeight) / 2f);
                width = PixelWidth;
                height = PixelHeight;
            }

            Dest.Set(left, top, left + width, top + height);
            StampPaint.Alpha = (int)MathF.Round(Math.Clamp(alpha, 0f, 1f) * 255f);
            native.DrawBitmap(_bitmap, null, Dest, StampPaint);
            return;
        }

        canvas.Alpha = alpha;
        canvas.DrawImage(_image, dest.X, dest.Y, dest.Width, dest.Height);
        canvas.Alpha = 1f;
    }

    // ScalingCanvas only exposes its x scale. This reads the y scale without reflection, and survives trimming.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_scaleY")]
    private static extern ref float ScaleYField(ScalingCanvas canvas);

    private static float ScaleY(ScalingCanvas canvas)
    {
        try
        {
            return ScaleYField(canvas);
        }
        catch (MissingFieldException)
        {
            return canvas.GetScale();
        }
    }
}
