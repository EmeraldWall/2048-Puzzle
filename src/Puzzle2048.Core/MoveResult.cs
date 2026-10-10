namespace Puzzle2048.Core;

/// <summary>Everything the screen needs to show after one swipe.</summary>
public sealed class MoveResult
{
    internal MoveResult(MoveOutcome outcome, Tile? spawned)
    {
        Outcome = outcome;
        Spawned = spawned;
    }

    public MoveOutcome Outcome { get; }

    /// <summary>The new tile that appeared after the move, if the board had room.</summary>
    public Tile? Spawned { get; }

    /// <summary>Points for the move including any combo bonus.</summary>
    public long PointsEarned { get; internal set; }

    public long ComboBonus { get; internal set; }

    /// <summary>Length of the current streak of merging moves.</summary>
    public int Combo { get; internal set; }

    /// <summary>Short pop-up text such as "COMBO x3  +120", or null when there is nothing to announce.</summary>
    public string? Banner { get; internal set; }

    public bool GoalJustMet { get; internal set; }

    public bool BecameWon { get; internal set; }

    public bool BecameLost { get; internal set; }
}
