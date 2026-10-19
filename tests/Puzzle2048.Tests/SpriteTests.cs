using Microsoft.Maui.Graphics;
using Puzzle2048.Core;
using Puzzle2048.Rendering;

namespace Puzzle2048.Tests;

[CollectionDefinition("Sprites", DisableParallelization = true)]
public class SpriteCollection;

/// <summary>The sprite cache is shared, so these tests run on their own.</summary>
[Collection("Sprites")]
public sealed class SpriteTests : IDisposable
{
    private sealed class FakeSprite(int width, int height) : Sprite
    {
        public override int PixelWidth => width;
        public override int PixelHeight => height;
        public List<RectF> Stamps { get; } = [];
        public override void Draw(ICanvas canvas, RectF dest, float alpha) => Stamps.Add(dest);
    }

    private int _created;

    public SpriteTests()
    {
        Sprites.Clear();
        Sprites.PixelScale = 2f;
        Sprites.Factory = (w, h, draw) =>
        {
            _created++;
            draw(new PictureCanvas(0, 0, w, h));
            return new FakeSprite(w, h);
        };
    }

    public void Dispose()
    {
        Sprites.Factory = null;
        Sprites.Clear();
        Sprites.PixelScale = 1f;
    }

    private static GameScene Scene(long now)
    {
        var session = new GameSession(GameMode.Classic, 4, randomFactory: () => new Random(3));
        session.Board.LoadValues([2, 4, 8, 16, 32, 64, 128, 256, 2, 4, 8, 16, 0, 0, 0, 0]);
        return new GameScene
        {
            Session = session,
            Clock = () => now,
            Hud = new HudState { Score = 1234, Best = 5678, ModeChip = "CLASSIC 4x4", ShowPowerUps = true },
        };
    }

    [Fact]
    public void ASteadyScreenMakesItsSpritesOnceAndThenOnlyStamps()
    {
        long now = 5_000_000;
        GameScene scene = Scene(now);
        var area = new RectF(0, 0, 400, 800);

        scene.Draw(new PictureCanvas(0, 0, 400, 800), area);
        int afterFirstFrame = _created;
        for (int i = 0; i < 30; i++)
            scene.Draw(new PictureCanvas(0, 0, 400, 800), area);

        Assert.True(afterFirstFrame > 0);
        Assert.Equal(afterFirstFrame, _created);
    }

    [Fact]
    public void EqualTilesShareOneSprite()
    {
        var board = new BoardRenderer
        {
            Session = new GameSession(GameMode.Classic, 4, randomFactory: () => new Random(1)),
            Clock = () => 9_000_000,
        };
        board.Session.Board.LoadValues([2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0]);
        board.DrawBoard(new PictureCanvas(0, 0, 400, 400), new RectF(0, 0, 400, 400));

        // The empty board, one "2" tile and the letters inside it
        Assert.Single(Sprites.Keys, k => k.Kind == Sprites.KindTile);
        Assert.Single(Sprites.Keys, k => k.Kind == Sprites.KindBoard);
    }

    [Fact]
    public void TheCacheStaysWithinItsLimit()
    {
        for (int i = 0; i < 2000; i++)
            Sprites.Create(new SpriteKey(99, i, 0, 0, 0, 0, 0, 0), 4, 4, 1f, _ => { });

        Assert.InRange(Sprites.Count, 1, 700);
        Assert.True(Sprites.TryGet(new SpriteKey(99, 1999, 0, 0, 0, 0, 0, 0), out _));
        Assert.False(Sprites.TryGet(new SpriteKey(99, 0, 0, 0, 0, 0, 0, 0), out _));
    }

    [Fact]
    public void ABlobStampCoversItsRectangle()
    {
        var rect = new RectF(10, 20, 30, 15);
        Painter.Blob(new PictureCanvas(0, 0, 100, 100), rect, 3, Colors.Red);

        Assert.True(Sprites.TryGet(Sprites.Keys.Single(k => k.Kind == Sprites.KindBlob), out Sprite sprite));
        RectF stamp = Assert.Single(((FakeSprite)sprite).Stamps);
        // The stamp's 1 px clear border sits just outside the rectangle on every side
        Assert.InRange(rect.Left - stamp.Left, 0.01f, 1f);
        Assert.InRange(stamp.Right - rect.Right, 0.01f, 1f);
        Assert.InRange(rect.Top - stamp.Top, 0.01f, 1f);
        Assert.InRange(stamp.Bottom - rect.Bottom, 0.01f, 1f);
    }
}
