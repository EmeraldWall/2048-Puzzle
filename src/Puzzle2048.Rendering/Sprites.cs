using Microsoft.Maui.Graphics;

namespace Puzzle2048.Rendering;

/// <summary>A picture drawn once into a bitmap and then stamped onto the screen.</summary>
public abstract class Sprite
{
    public abstract int PixelWidth { get; }

    public abstract int PixelHeight { get; }

    /// <summary>Stamps the bitmap into <paramref name="dest"/> (canvas units) with smooth scaling.</summary>
    public abstract void Draw(ICanvas canvas, RectF dest, float alpha);
}

/// <summary>What a sprite shows. Equal keys always mean an identical picture.</summary>
public readonly record struct SpriteKey(int Kind, int A, int B, int C, int D, int E, int F, int G);

/// <summary>
/// Cache of pre-drawn pictures. Drawing a tile from vectors means a shadow blur, three gradients and a dozen font
/// outlines, and on Android every outline is rebuilt through JNI each frame. Stamping a cached bitmap is a single
/// GPU texture draw, which is what lets the game hold 120 fps.
///
/// The app plugs in a platform <see cref="Factory"/>. Without one (or while a sprite is being drawn) every caller
/// draws its vectors directly, so the pictures look the same either way.
/// </summary>
public static class Sprites
{
    public const int KindTile = 1;
    public const int KindBoard = 2;
    public const int KindGlyph = 3;
    public const int KindPanel = 4;
    public const int KindIcon = 5;
    public const int KindBackdrop = 6;
    public const int KindOrb = 7;
    public const int KindSparkle = 8;
    public const int KindBlob = 9;

    private const int Capacity = 700;

    private static readonly Dictionary<SpriteKey, LinkedListNode<(SpriteKey Key, Sprite Sprite)>> Map = [];
    private static readonly LinkedList<(SpriteKey Key, Sprite Sprite)> Order = new();

    [ThreadStatic]
    private static int _rendering;

    /// <summary>Makes a sprite of the given pixel size by running the draw action on a bitmap canvas.</summary>
    public static Func<int, int, Action<ICanvas>, Sprite>? Factory { get; set; }

    /// <summary>Device pixels per canvas unit (the screen density on Android).</summary>
    public static float PixelScale { get; set; } = 1f;

    /// <summary>True when callers should use sprites instead of drawing vectors.</summary>
    public static bool Enabled => Factory is not null && _rendering == 0;

    public static int Count => Map.Count;

    /// <summary>The keys currently cached, for diagnostics.</summary>
    public static IEnumerable<SpriteKey> Keys => Map.Keys;

    public static bool TryGet(in SpriteKey key, out Sprite sprite)
    {
        if (Map.TryGetValue(key, out var node))
        {
            // Most recently used stays at the front
            if (node != Order.First)
            {
                Order.Remove(node);
                Order.AddFirst(node);
            }
            sprite = node.Value.Sprite;
            return true;
        }
        sprite = null!;
        return false;
    }

    /// <summary>
    /// Draws a sprite of <paramref name="width"/> x <paramref name="height"/> canvas units. The action draws in canvas
    /// units with (0, 0) at the sprite's top-left corner.
    /// </summary>
    public static Sprite Create(in SpriteKey key, float width, float height, Action<ICanvas> draw) =>
        Create(key, Math.Max(1, (int)MathF.Ceiling(width * PixelScale)), Math.Max(1, (int)MathF.Ceiling(height * PixelScale)), PixelScale, draw);

    /// <summary>Draws a sprite of an exact pixel size; the action's canvas is scaled by <paramref name="unitScale"/>.</summary>
    public static Sprite Create(in SpriteKey key, int pixelWidth, int pixelHeight, float unitScale, Action<ICanvas> draw)
    {
        Func<int, int, Action<ICanvas>, Sprite> factory = Factory ?? throw new InvalidOperationException("No sprite factory");
        _rendering++;
        Sprite sprite;
        try
        {
            sprite = factory(pixelWidth, pixelHeight, canvas =>
            {
                canvas.Scale(unitScale, unitScale);
                draw(canvas);
            });
        }
        finally
        {
            _rendering--;
        }

        Map[key] = Order.AddFirst((key, sprite));
        if (Map.Count > Capacity)
        {
            // Old bitmaps are simply dropped; the platform frees them once nothing draws them any more
            var last = Order.Last!;
            Order.RemoveLast();
            Map.Remove(last.Value.Key);
        }
        return sprite;
    }

    /// <summary>Forgets every sprite, for example after the screen density changed.</summary>
    public static void Clear()
    {
        Map.Clear();
        Order.Clear();
    }

    /// <summary>Canvas units covered by a sprite made with <see cref="Create(in SpriteKey, float, float, Action{ICanvas})"/>.</summary>
    public static SizeF SizeOf(Sprite sprite) => new(sprite.PixelWidth / PixelScale, sprite.PixelHeight / PixelScale);

    public static int Argb(Color color) => color.ToInt();

    /// <summary>Quantises a ratio so slightly different floats still share a sprite.</summary>
    public static int Milli(float value) => (int)MathF.Round(value * 1000f);
}
