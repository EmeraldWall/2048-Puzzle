using System.Globalization;

namespace Puzzle2048.Core;

public enum GoalType
{
    Tile,
    Score,
    TileInMoves,
}

/// <summary>Today's goal for the Daily Challenge.</summary>
public sealed record DailyGoal(int Size, GoalType Type, int Target, int MoveLimit = 0)
{
    public string Title => Type switch
    {
        GoalType.Tile => $"Make a {Target} tile",
        GoalType.Score => $"Score {Target} points",
        _ => $"Make {Target} in {MoveLimit} moves",
    };

    public bool IsMet(int bestTile, long score, int moves) => Type switch
    {
        GoalType.Tile => bestTile >= Target,
        GoalType.Score => score >= Target,
        _ => bestTile >= Target && moves <= MoveLimit,
    };

    /// <summary>True once the goal can no longer be reached in this run.</summary>
    public bool IsLost(int moves) => Type == GoalType.TileInMoves && moves > MoveLimit;

    /// <summary>Score that earns the third star once the goal is met.</summary>
    public int ThreeStarScore => Type switch
    {
        GoalType.Tile => Target * 6,
        GoalType.Score => Target * 3 / 2,
        _ => Target * 4,
    };

    /// <summary>
    /// Stars for a daily run, Candy-style: one for playing properly, two for the goal, three for the goal
    /// with a high score.
    /// </summary>
    public int Stars(bool goalMet, long score, int moves)
    {
        if (goalMet)
            return score >= ThreeStarScore ? 3 : 2;
        return moves >= ProgressStore.StreakMinMoves ? 1 : 0;
    }

    /// <summary>0 to 1 progress toward the goal, for a progress bar.</summary>
    public double Fraction(int bestTile, long score, int moves) => Type switch
    {
        GoalType.Score => Math.Clamp(score / (double)Target, 0, 1),
        _ => bestTile <= 0 ? 0 : Math.Clamp(Math.Log2(bestTile) / Math.Log2(Target), 0, 1),
    };

    public string Progress(int bestTile, long score, int moves) => Type switch
    {
        GoalType.Tile => $"best tile {bestTile}",
        GoalType.Score => $"{score} / {Target}",
        _ => $"move {moves} / {MoveLimit}",
    };
}

/// <summary>
/// One puzzle per UTC day. Everybody gets the same goal and the same tile sequence, because both
/// come from the calendar day. The day rolls over at 00:00 UTC.
/// </summary>
public static class DailyChallenge
{
    private static readonly DailyGoal[] Goals =
    [
        new(4, GoalType.Tile, 512),
        new(4, GoalType.Score, 3000),
        new(5, GoalType.Tile, 1024),
        new(4, GoalType.TileInMoves, 512, 220),
        new(5, GoalType.Score, 7000),
        new(4, GoalType.Score, 5000),
        new(6, GoalType.Tile, 2048),
        new(4, GoalType.TileInMoves, 256, 100),
        new(5, GoalType.TileInMoves, 512, 150),
    ];

    /// <summary>Days since 1970-01-01 in UTC.</summary>
    public static int DayNumber(DateTimeOffset now) => (int)(now.ToUniversalTime().ToUnixTimeSeconds() / 86_400);

    public static int Today() => DayNumber(DateTimeOffset.UtcNow);

    public static DailyGoal GoalFor(int day) => Goals[((day % Goals.Length) + Goals.Length) % Goals.Length];

    /// <summary>Seed for the tile sequence. A seeded System.Random gives the same numbers on every device.</summary>
    public static int SeedFor(int day) => unchecked((int)(day * 2_654_435_761L + 40_503L));

    /// <summary>For example "Oct 7".</summary>
    public static string Label(int day) =>
        DateTimeOffset.FromUnixTimeSeconds(day * 86_400L).ToString("MMM d", CultureInfo.InvariantCulture);
}
