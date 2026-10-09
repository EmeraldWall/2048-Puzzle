using Puzzle2048.Core;

namespace Puzzle2048.Tests;

public class BoardTests
{
    private static GameBoard Board(int size, params int[] values)
    {
        var board = new GameBoard(size);
        board.LoadValues(values);
        return board;
    }

    private static int[] Row(GameBoard board, int y) =>
        Enumerable.Range(0, board.Size).Select(x => board[x, y]?.Value ?? 0).ToArray();

    [Fact]
    public void SlidesLeftAndMergesPairs()
    {
        var board = Board(4,
            2, 2, 0, 0,
            2, 0, 2, 0,
            4, 2, 2, 0,
            0, 0, 0, 0);

        MoveOutcome outcome = board.Move(Direction.Left);

        Assert.True(outcome.Moved);
        Assert.Equal([4, 0, 0, 0], Row(board, 0));
        Assert.Equal([4, 0, 0, 0], Row(board, 1));
        Assert.Equal([4, 4, 0, 0], Row(board, 2));
        Assert.Equal(12, outcome.Points);
        Assert.Equal(3, outcome.MergeCount);
    }

    [Fact]
    public void ATileMergesOnlyOncePerMove()
    {
        var board = Board(4,
            2, 2, 2, 2,
            4, 4, 4, 0,
            2, 2, 4, 0,
            0, 0, 0, 0);

        board.Move(Direction.Left);

        Assert.Equal([4, 4, 0, 0], Row(board, 0));
        Assert.Equal([8, 4, 0, 0], Row(board, 1));
        Assert.Equal([4, 4, 0, 0], Row(board, 2));
    }

    [Theory]
    [InlineData(Direction.Left, 0, 1)]
    [InlineData(Direction.Right, 3, 1)]
    [InlineData(Direction.Up, 1, 0)]
    [InlineData(Direction.Down, 1, 3)]
    public void SingleTileTravelsToTheWall(Direction direction, int expectedX, int expectedY)
    {
        var board = new GameBoard(4);
        board.Place(1, 1, 2);

        board.Move(direction);

        Tile tile = Assert.Single(board.Tiles);
        Assert.Equal((expectedX, expectedY), (tile.X, tile.Y));
    }

    [Fact]
    public void ReportsNoMoveWhenNothingCanChange()
    {
        var board = Board(4,
            2, 4, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0);

        MoveOutcome outcome = board.Move(Direction.Left);

        Assert.False(outcome.Moved);
        Assert.Equal(0, outcome.Points);
        Assert.Equal([2, 4, 0, 0], Row(board, 0));
    }

    [Fact]
    public void SlidesDescribeEveryTileAndLinkMerges()
    {
        var board = Board(4,
            2, 2, 0, 4,
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0);

        MoveOutcome outcome = board.Move(Direction.Left);

        Assert.Equal(3, outcome.Slides.Count);
        Tile merged = Assert.Single(outcome.Merged);
        Assert.Equal(4, merged.Value);
        Assert.Equal((0, 0), (merged.X, merged.Y));

        var mergingSlides = outcome.Slides.Where(s => s.MergedIntoId == merged.Id).ToList();
        Assert.Equal(2, mergingSlides.Count);
        Assert.All(mergingSlides, s => Assert.Equal((0, 0), (s.ToX, s.ToY)));

        TileSlide fourSlide = outcome.Slides.Single(s => s.Value == 4);
        Assert.Null(fourSlide.MergedIntoId);
        Assert.Equal((1, 0), (fourSlide.ToX, fourSlide.ToY));
    }

    [Fact]
    public void HasMovesIsFalseOnlyWhenFullWithNoEqualNeighbours()
    {
        var stuck = Board(4,
            2, 4, 2, 4,
            4, 2, 4, 2,
            2, 4, 2, 4,
            4, 2, 4, 2);
        Assert.False(stuck.HasMoves());

        var mergeable = Board(4,
            2, 4, 2, 4,
            4, 2, 4, 2,
            2, 4, 2, 4,
            4, 2, 4, 4);
        Assert.True(mergeable.HasMoves());

        Assert.True(new GameBoard(4).HasMoves());
    }

    [Fact]
    public void SeededSpawnsAreRepeatable()
    {
        var a = new GameBoard(4);
        var b = new GameBoard(4);
        var randomA = new Random(1234);
        var randomB = new Random(1234);

        for (int i = 0; i < 10; i++)
        {
            a.SpawnRandom(randomA);
            b.SpawnRandom(randomB);
            a.Move((Direction)(i % 4));
            b.Move((Direction)(i % 4));
        }

        Assert.Equal(a.ToValues(), b.ToValues());
    }

    [Fact]
    public void SpawnReturnsNullOnAFullBoard()
    {
        var board = Board(2, 2, 4, 4, 2);
        Assert.Null(board.SpawnRandom(new Random(1)));
    }
}
