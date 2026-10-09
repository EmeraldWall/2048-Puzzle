using System.Text.Json;

namespace Puzzle2048.Core;

/// <summary>A saved game, written as JSON so it survives the app being closed.</summary>
public sealed class GameSnapshot
{
    public GameMode Mode { get; set; }
    public int Size { get; set; }
    public int[] Values { get; set; } = [];
    public long Score { get; set; }
    public int Moves { get; set; }
    public int Combo { get; set; }
    public RunState State { get; set; }
    public bool Endless { get; set; }
    public int TrashCharges { get; set; }
    public int[]? UndoValues { get; set; }
    public long UndoScore { get; set; }
    public int UndoMoves { get; set; }

    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>Returns null for missing or damaged data instead of throwing.</summary>
    public static GameSnapshot? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            GameSnapshot? snapshot = JsonSerializer.Deserialize<GameSnapshot>(json);
            if (snapshot is null || snapshot.Size < 2 || snapshot.Size > 8 || snapshot.Values.Length != snapshot.Size * snapshot.Size)
                return null;
            return snapshot;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
