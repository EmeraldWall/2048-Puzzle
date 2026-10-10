using Puzzle2048.Core;

namespace Puzzle2048.Tests;

public class SessionTests
{
    /// <summary>A session whose board is set by hand, so the rules can be tested without luck.</summary>
    private static GameSession Setup(GameMode mode, int size, params int[] values)
    {
        var session = new GameSession(mode, size, dayNumber: 20000, randomFactory: () => new Random(5));
        session.Board.LoadValues(values);
        return session;
    }

    private static int[] Empty4() => new int[16];

    [Fact]
    public void NewGameStartsWithTwoTiles()
    {
        var session = new GameSession(GameMode.Classic, 4);
        Assert.Equal(2, session.Board.TileCount);
        Assert.Equal(RunState.Playing, session.State);
        Assert.Equal(0, session.Score);
    }

    [Fact]
    public void MovingScoresMergesAndSpawnsATile()
    {
        int[] cells = Empty4();
        cells[0] = 2;
        cells[1] = 2;
        var session = Setup(GameMode.Classic, 4, cells);

        MoveResult? result = session.Move(Direction.Left);

        Assert.NotNull(result);
        Assert.Equal(4, session.Score);
        Assert.NotNull(result!.Spawned);
        Assert.Equal(2, session.Board.TileCount);
        Assert.Equal(1, session.Moves);
    }

    [Fact]
    public void ABlockedMoveReturnsNullAndChangesNothing()
    {
        int[] cells = Empty4();
        cells[0] = 2;
        var session = Setup(GameMode.Classic, 4, cells);

        Assert.Null(session.Move(Direction.Left));
        Assert.Equal(0, session.Moves);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void ComboBonusGrowsWithConsecutiveMerges()
    {
        // Two separate pairs per row so every move merges
        int[] cells = Empty4();
        cells[0] = 2; cells[1] = 2; cells[2] = 2; cells[3] = 2;
        cells[4] = 4; cells[5] = 4; cells[6] = 4; cells[7] = 4;
        var session = Setup(GameMode.Classic, 4, cells);

        MoveResult first = session.Move(Direction.Left)!;
        Assert.Equal(1, first.Combo);
        Assert.Equal(0, first.ComboBonus);
        Assert.Equal(4 + 4 + 8 + 8, first.PointsEarned);

        // Put the board back to a mergeable state and merge again
        session.Board.LoadValues(new[] { 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        MoveResult second = session.Move(Direction.Left)!;
        Assert.Equal(2, second.Combo);
        Assert.Equal(2, second.ComboBonus); // points 4 * (2 - 1) / 2
        Assert.Equal("COMBO x2  +2", second.Banner);
    }

    [Fact]
    public void ComboResetsWhenAMoveDoesNotMerge()
    {
        int[] cells = Empty4();
        cells[0] = 2; cells[1] = 2;
        var session = Setup(GameMode.Classic, 4, cells);
        session.Move(Direction.Left);
        Assert.Equal(1, session.Combo);

        session.Board.LoadValues(new[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        session.Move(Direction.Right);
        Assert.Equal(0, session.Combo);
    }

    [Fact]
    public void ClassicWinsAtTwentyFortyEightAndCanContinue()
    {
        int[] cells = Empty4();
        cells[0] = 1024; cells[1] = 1024;
        var session = Setup(GameMode.Classic, 4, cells);

        MoveResult result = session.Move(Direction.Left)!;

        Assert.True(result.BecameWon);
        Assert.Equal(RunState.Won, session.State);
        Assert.Null(session.Move(Direction.Right));

        session.ContinueEndless();
        Assert.Equal(RunState.Playing, session.State);
        Assert.True(session.Endless);
        Assert.NotNull(session.Move(Direction.Right));
    }

    [Fact]
    public void LosesWhenTheBoardLocks()
    {
        // After sliding the last row left, the only gap is the corner. Its neighbours are 8 and 16,
        // so whether a 2 or a 4 spawns there nothing can merge any more.
        var session = Setup(GameMode.Classic, 4,
            2, 4, 2, 4,
            4, 2, 4, 2,
            2, 4, 2, 8,
            0, 4, 2, 16);

        MoveResult? result = session.Move(Direction.Left);

        Assert.NotNull(result);
        Assert.True(result!.BecameLost);
        Assert.Equal(RunState.Lost, session.State);
        Assert.Null(session.Move(Direction.Up));
    }

    [Fact]
    public void SprintIsWonAtFiveTwelve()
    {
        int[] cells = Empty4();
        cells[0] = 256; cells[1] = 256;
        var session = Setup(GameMode.Sprint, 4, cells);

        MoveResult result = session.Move(Direction.Left)!;

        Assert.True(result.BecameWon);
        Assert.Equal(RunState.Won, session.State);
    }

    [Fact]
    public void TimeAttackClockStartsOnFirstMoveAndRunsOut()
    {
        int[] cells = Empty4();
        cells[0] = 2; cells[1] = 2;
        var session = Setup(GameMode.TimeAttack, 4, cells);

        Assert.False(session.Tick(TimeSpan.FromSeconds(10)));
        Assert.Equal(GameSession.TimeAttackStart, session.TimeLeft);

        session.Move(Direction.Left);
        Assert.True(session.ClockRunning);

        Assert.False(session.Tick(TimeSpan.FromSeconds(30)));
        Assert.Equal(TimeSpan.FromSeconds(30) + GameSession.TimeAttackBonusPerMerge, session.TimeLeft);

        Assert.True(session.Tick(TimeSpan.FromSeconds(40)));
        Assert.Equal(RunState.Lost, session.State);
        Assert.Equal(TimeSpan.Zero, session.TimeLeft);
    }

    [Fact]
    public void TimeAttackBonusIsCapped()
    {
        int[] cells = Empty4();
        cells[0] = 2; cells[1] = 2; cells[4] = 2; cells[5] = 2;
        var session = Setup(GameMode.TimeAttack, 4, cells);

        session.Move(Direction.Left);

        Assert.True(session.TimeLeft <= GameSession.TimeAttackMax);
        Assert.Equal(GameSession.TimeAttackStart + GameSession.TimeAttackBonusPerMerge * 2, session.TimeLeft);
    }

    [Fact]
    public void UndoRestoresTheBoardAndScore()
    {
        int[] cells = Empty4();
        cells[0] = 2; cells[1] = 2;
        var session = Setup(GameMode.Classic, 4, cells);
        int[] before = session.Board.ToValues();

        session.Move(Direction.Left);
        Assert.True(session.CanUndo);

        Assert.True(session.Undo());
        Assert.Equal(before, session.Board.ToValues());
        Assert.Equal(0, session.Score);
        Assert.Equal(0, session.Moves);
        Assert.False(session.CanUndo);
    }

    [Theory]
    [InlineData(GameMode.Daily)]
    [InlineData(GameMode.TimeAttack)]
    [InlineData(GameMode.Sprint)]
    public void UndoAndTrashAreClassicOnly(GameMode mode)
    {
        var session = new GameSession(mode, 4, dayNumber: 20000);
        session.Move(Direction.Left);
        session.Move(Direction.Up);

        Assert.False(session.CanUndo);
        Assert.False(session.Undo());
        Assert.False(session.CanTrash);
        Assert.Empty(session.RemoveSmallTiles());
    }

    [Fact]
    public void TrashRemovesSmallTilesTwicePerGame()
    {
        var session = Setup(GameMode.Classic, 4,
            2, 4, 8, 16,
            32, 2, 4, 8,
            16, 64, 128, 2,
            0, 0, 0, 0);

        Assert.True(session.CanTrash);
        IReadOnlyList<Tile> removed = session.RemoveSmallTiles();
        Assert.Equal(4, removed.Count);
        Assert.Equal(8, session.Board.TileCount);
        Assert.Equal(1, session.TrashCharges);
        Assert.All(removed, t => Assert.True(t.Value <= 8));
    }

    [Fact]
    public void TrashWillNotEmptyANearlyEmptyBoard()
    {
        int[] cells = Empty4();
        cells[0] = 2; cells[1] = 4; cells[2] = 8;
        var session = Setup(GameMode.Classic, 4, cells);
        Assert.False(session.CanTrash);
    }

    [Fact]
    public void DailyReplaysTheSamePuzzleForEveryone()
    {
        var a = new GameSession(GameMode.Daily, dayNumber: 20500);
        var b = new GameSession(GameMode.Daily, dayNumber: 20500);
        var other = new GameSession(GameMode.Daily, dayNumber: 20501);

        Assert.Equal(a.Board.ToValues(), b.Board.ToValues());
        Assert.Equal(DailyChallenge.GoalFor(20500).Size, a.Size);

        Direction[] moves = [Direction.Left, Direction.Up, Direction.Right, Direction.Down, Direction.Left, Direction.Up];
        foreach (Direction direction in moves)
        {
            a.Move(direction);
            b.Move(direction);
        }

        Assert.Equal(a.Board.ToValues(), b.Board.ToValues());
        Assert.Equal(a.Score, b.Score);

        // Restarting gives the identical opening again
        int[] opening = new GameSession(GameMode.Daily, dayNumber: 20500).Board.ToValues();
        a.StartFresh();
        Assert.Equal(opening, a.Board.ToValues());
        Assert.NotNull(other);
    }

    [Fact]
    public void DailyGoalIsDetectedOnce()
    {
        int day = Enumerable.Range(20000, 20).First(d => DailyChallenge.GoalFor(d) is { Type: GoalType.Tile, Target: 512 });
        var session = new GameSession(GameMode.Daily, dayNumber: day, randomFactory: () => new Random(3));
        int[] cells = new int[session.Size * session.Size];
        cells[0] = 256; cells[1] = 256;
        session.Board.LoadValues(cells);

        MoveResult first = session.Move(Direction.Left)!;
        Assert.True(first.GoalJustMet);
        Assert.True(session.GoalMet);
        Assert.Equal("GOAL COMPLETE!", first.Banner);

        MoveResult? second = session.Move(Direction.Down);
        Assert.False(second?.GoalJustMet ?? false);
    }

    [Fact]
    public void MilestoneTilesGetABanner()
    {
        int[] cells = Empty4();
        cells[0] = 128; cells[1] = 128;
        var session = Setup(GameMode.Classic, 4, cells);

        Assert.Equal("NEW TILE  256!", session.Move(Direction.Left)!.Banner);
    }

    [Fact]
    public void SnapshotRoundTripKeepsTheGame()
    {
        int[] cells = Empty4();
        cells[0] = 2; cells[1] = 2; cells[15] = 8;
        var session = Setup(GameMode.Classic, 4, cells);
        session.Move(Direction.Left);

        string json = session.ToSnapshot().ToJson();
        GameSnapshot? parsed = GameSnapshot.TryParse(json);
        Assert.NotNull(parsed);

        GameSession restored = GameSession.FromSnapshot(parsed!);
        Assert.Equal(session.Board.ToValues(), restored.Board.ToValues());
        Assert.Equal(session.Score, restored.Score);
        Assert.Equal(session.Moves, restored.Moves);
        Assert.True(restored.CanUndo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"Size\":4,\"Values\":[1,2]}")]
    public void DamagedSavesAreIgnored(string? json)
    {
        Assert.Null(GameSnapshot.TryParse(json));
    }
}
