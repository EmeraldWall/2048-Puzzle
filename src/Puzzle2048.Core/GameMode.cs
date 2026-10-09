namespace Puzzle2048.Core;

/// <summary>The ways to play. Only Classic keeps a resumable board, undo and the tile-removal power-up.</summary>
public enum GameMode
{
    Classic,
    Daily,
    TimeAttack,
    Sprint,
}

public static class GameModeExtensions
{
    public static bool IsClassic(this GameMode mode) => mode == GameMode.Classic;

    public static string Title(this GameMode mode) => mode switch
    {
        GameMode.Classic => "Classic",
        GameMode.Daily => "Daily Challenge",
        GameMode.TimeAttack => "Time Attack",
        _ => "Sprint",
    };
}
