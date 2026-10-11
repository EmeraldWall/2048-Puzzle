namespace Puzzle2048.Rendering;

/// <summary>Buttons on the game screen. The page asks the scene which one was tapped.</summary>
public enum SceneAction
{
    None,
    Back,
    Restart,
    Undo,
    Trash,
    Primary,
    Secondary,
    Tertiary,
}

/// <summary>What the screen shows around the board. The page fills it in every frame.</summary>
public sealed class HudState
{
    public long Score { get; set; }
    public long Best { get; set; }

    /// <summary>Short label at the top, for example "CLASSIC 4x4" or "DAILY OCT 10".</summary>
    public string ModeChip { get; set; } = "";

    /// <summary>Optional bar under the scores: the goal, the clock or the sprint progress.</summary>
    public string? StripText { get; set; }
    public float? StripFraction { get; set; }
    public bool StripUrgent { get; set; }
    public bool StripDone { get; set; }

    /// <summary>One line under the board when there are no power-ups.</summary>
    public string? BottomHint { get; set; }

    public bool ShowPowerUps { get; set; }
    public bool CanUndo { get; set; }
    public bool CanTrash { get; set; }
    public int TrashCharges { get; set; }
}

/// <summary>The end-of-run card.</summary>
public sealed class ResultCard
{
    public string Title { get; init; } = "";
    public IReadOnlyList<string> Lines { get; init; } = [];

    /// <summary>0 to 3 stars, or -1 to hide the stars.</summary>
    public int Stars { get; init; } = -1;

    /// <summary>Confetti and a golden ribbon for wins, goals and records.</summary>
    public bool Celebrate { get; init; }

    public string Primary { get; init; } = "Play again";
    public string? Secondary { get; init; }
    public string Tertiary { get; init; } = "Menu";
}
