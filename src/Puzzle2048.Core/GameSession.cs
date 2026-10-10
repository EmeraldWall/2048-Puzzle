namespace Puzzle2048.Core;

/// <summary>
/// One run of the game in a given mode: the board plus the score, combo, clock, goal and
/// win/lose rules. This is pure logic, so the whole game can be tested without a screen.
/// </summary>
public sealed class GameSession
{
    public const int ClassicWinTile = 2048;
    public const int SprintTarget = 512;
    public const int MilestoneTile = 256;
    public const int MaxComboLevel = 5;
    public const int TrashChargesPerGame = 2;

    public static readonly TimeSpan TimeAttackStart = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan TimeAttackMax = TimeSpan.FromSeconds(90);
    public static readonly TimeSpan TimeAttackBonusPerMerge = TimeSpan.FromMilliseconds(400);

    private readonly Func<Random> _randomFactory;
    private Random _random;
    private UndoState? _undo;

    /// <param name="mode">The mode to play.</param>
    /// <param name="size">Board size. Ignored for Daily, where today's goal decides.</param>
    /// <param name="dayNumber">For Daily: the UTC day number that sets the goal and the tile sequence.</param>
    /// <param name="randomFactory">Test hook for non-daily modes.</param>
    public GameSession(GameMode mode, int size = 4, int? dayNumber = null, Func<Random>? randomFactory = null)
    {
        Mode = mode;
        DayNumber = dayNumber ?? DailyChallenge.Today();
        Goal = mode == GameMode.Daily ? DailyChallenge.GoalFor(DayNumber) : null;
        Size = Goal?.Size ?? size;
        Board = new GameBoard(Size);

        _randomFactory = randomFactory ?? (mode == GameMode.Daily
            ? () => new Random(DailyChallenge.SeedFor(DayNumber))
            : () => new Random());
        _random = _randomFactory();

        StartFresh();
    }

    public GameMode Mode { get; }
    public int Size { get; }
    public int DayNumber { get; }
    public DailyGoal? Goal { get; }
    public GameBoard Board { get; }

    public long Score { get; private set; }
    public int Moves { get; private set; }
    public int Combo { get; private set; }
    public RunState State { get; private set; }
    public bool Endless { get; private set; }
    public bool GoalMet { get; private set; }
    public int TrashCharges { get; private set; }

    /// <summary>Time spent playing. The clock starts with the first move.</summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>Time Attack only: how long is left.</summary>
    public TimeSpan TimeLeft { get; private set; }

    public bool ClockStarted { get; private set; }

    public bool CanUndo => Mode.IsClassic() && _undo is not null && State == RunState.Playing;

    public bool CanTrash => Mode.IsClassic() && State == RunState.Playing && TrashCharges > 0
        && Board.TileCount > Size + 2 && Board.Tiles.Any(t => t.Value <= TrashMaxValue);

    public const int TrashMaxValue = 32;

    public bool ClockRunning => ClockStarted && State == RunState.Playing;

    /// <summary>Restarts the run. Daily replays the same puzzle.</summary>
    public void StartFresh()
    {
        _random = _randomFactory();
        Board.Clear();
        Score = 0;
        Moves = 0;
        Combo = 0;
        State = RunState.Playing;
        Endless = false;
        GoalMet = false;
        TrashCharges = TrashChargesPerGame;
        Elapsed = TimeSpan.Zero;
        TimeLeft = TimeAttackStart;
        ClockStarted = false;
        _undo = null;

        Board.SpawnRandom(_random);
        Board.SpawnRandom(_random);
    }

    /// <summary>Applies a swipe. Returns null when nothing moved or the run is over.</summary>
    public MoveResult? Move(Direction direction)
    {
        if (State != RunState.Playing)
            return null;

        UndoState? before = Mode.IsClassic() ? CaptureUndo() : null;

        MoveOutcome outcome = Board.Move(direction);
        if (!outcome.Moved)
            return null;

        _undo = before;
        Moves++;
        ClockStarted = true;

        Tile? spawned = null;

        // Combo: consecutive merging moves earn up to +200% of the move's points
        long bonus = 0;
        if (outcome.MergeCount > 0)
        {
            Combo++;
            int level = Math.Min(Combo, MaxComboLevel);
            bonus = outcome.Points * (level - 1) / 2;
        }
        else
        {
            Combo = 0;
        }

        Score += outcome.Points + bonus;

        if (Mode == GameMode.TimeAttack && outcome.MergeCount > 0)
            TimeLeft = Min(TimeAttackMax, TimeLeft + TimeAttackBonusPerMerge * outcome.MergeCount);

        string? banner = null;
        if (bonus > 0)
            banner = $"COMBO x{Combo}  +{bonus}";
        if (outcome.BiggestMerge >= MilestoneTile)
            banner = $"NEW TILE  {outcome.BiggestMerge}!";

        bool won = false;
        if (Mode == GameMode.Sprint && outcome.BiggestMerge >= SprintTarget)
            won = true;
        else if (Mode.IsClassic() && !Endless && outcome.BiggestMerge >= ClassicWinTile)
            won = true;

        if (won)
        {
            State = RunState.Won;
        }
        else
        {
            spawned = Board.SpawnRandom(_random);
        }

        bool goalJustMet = false;
        if (Goal is not null && !GoalMet && Goal.IsMet(Board.MaxTile, Score, Moves))
        {
            GoalMet = true;
            goalJustMet = true;
            banner = "GOAL COMPLETE!";
        }

        bool lost = false;
        if (State == RunState.Playing && !Board.HasMoves())
        {
            State = RunState.Lost;
            lost = true;
        }

        return new MoveResult(outcome, spawned)
        {
            PointsEarned = outcome.Points + bonus,
            ComboBonus = bonus,
            Combo = Combo,
            Banner = banner,
            GoalJustMet = goalJustMet,
            BecameWon = won,
            BecameLost = lost,
        };
    }

    /// <summary>Moves the clock forward. Returns true when this ended the run (Time Attack ran out).</summary>
    public bool Tick(TimeSpan delta)
    {
        if (!ClockRunning || delta <= TimeSpan.Zero)
            return false;

        Elapsed += delta;

        if (Mode == GameMode.TimeAttack)
        {
            TimeLeft -= delta;
            if (TimeLeft <= TimeSpan.Zero)
            {
                TimeLeft = TimeSpan.Zero;
                State = RunState.Lost;
                return true;
            }
        }

        return false;
    }

    /// <summary>After reaching 2048 in Classic, keep playing without a further win.</summary>
    public void ContinueEndless()
    {
        if (Mode.IsClassic() && State == RunState.Won)
        {
            Endless = true;
            State = Board.HasMoves() ? RunState.Playing : RunState.Lost;
        }
    }

    /// <summary>Takes back the last move (Classic only, one step).</summary>
    public bool Undo()
    {
        if (!CanUndo)
            return false;

        UndoState undo = _undo!;
        Board.LoadValues(undo.Values);
        Score = undo.Score;
        Moves = undo.Moves;
        Combo = undo.Combo;
        _undo = null;
        return true;
    }

    /// <summary>Clears out the smallest tiles to make room (Classic only, limited uses per game).</summary>
    public IReadOnlyList<Tile> RemoveSmallTiles()
    {
        if (!CanTrash)
            return [];

        List<Tile> victims = Board.Tiles
            .Where(t => t.Value <= TrashMaxValue)
            .OrderBy(t => t.Value)
            .ThenBy(t => t.Y)
            .ThenBy(t => t.X)
            .Take(Size)
            .ToList();

        // Never empty the whole board
        int removable = Math.Max(0, Board.TileCount - (Size + 2));
        victims = victims.Take(removable).ToList();

        foreach (Tile tile in victims)
            Board.Remove(tile.X, tile.Y);

        TrashCharges--;
        _undo = null;
        return victims;
    }

    public GameSnapshot ToSnapshot() => new()
    {
        Mode = Mode,
        Size = Size,
        Values = Board.ToValues(),
        Score = Score,
        Moves = Moves,
        Combo = Combo,
        State = State,
        Endless = Endless,
        TrashCharges = TrashCharges,
        UndoValues = _undo?.Values,
        UndoScore = _undo?.Score ?? 0,
        UndoMoves = _undo?.Moves ?? 0,
    };

    /// <summary>Restores a saved Classic game.</summary>
    public static GameSession FromSnapshot(GameSnapshot snapshot)
    {
        var session = new GameSession(snapshot.Mode, snapshot.Size);
        session.Board.LoadValues(snapshot.Values);
        session.Score = snapshot.Score;
        session.Moves = snapshot.Moves;
        session.Combo = snapshot.Combo;
        session.State = snapshot.State;
        session.Endless = snapshot.Endless;
        session.TrashCharges = snapshot.TrashCharges;
        session._undo = snapshot.UndoValues is { } values
            ? new UndoState(values, snapshot.UndoScore, snapshot.UndoMoves, 0)
            : null;
        return session;
    }

    private UndoState CaptureUndo() => new(Board.ToValues(), Score, Moves, Combo);

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private sealed record UndoState(int[] Values, long Score, int Moves, int Combo);
}
