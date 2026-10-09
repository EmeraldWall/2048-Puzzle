namespace Puzzle2048.Core;

/// <summary>
/// How one tile travelled during a move. Every tile that was on the board before the move gets one,
/// including tiles that did not move, so a renderer can draw the whole "before" picture and slide it.
/// </summary>
public sealed class TileSlide
{
    internal TileSlide(int tileId, int value, int fromX, int fromY, int toX, int toY)
    {
        TileId = tileId;
        Value = value;
        FromX = fromX;
        FromY = fromY;
        ToX = toX;
        ToY = toY;
    }

    public int TileId { get; }
    public int Value { get; }
    public int FromX { get; }
    public int FromY { get; }
    public int ToX { get; }
    public int ToY { get; }

    /// <summary>When set, this tile merges into the tile with this id when it arrives.</summary>
    public int? MergedIntoId { get; internal set; }

    public bool Moves => FromX != ToX || FromY != ToY;
}

/// <summary>What a single swipe did to the board, before any new tile spawns.</summary>
public sealed class MoveOutcome
{
    internal MoveOutcome(bool moved, long points, IReadOnlyList<TileSlide> slides, IReadOnlyList<Tile> merged)
    {
        Moved = moved;
        Points = points;
        Slides = slides;
        Merged = merged;
    }

    public bool Moved { get; }

    /// <summary>Sum of the values of all tiles created by merging.</summary>
    public long Points { get; }

    public IReadOnlyList<TileSlide> Slides { get; }

    /// <summary>The new, doubled tiles that appear when merges finish.</summary>
    public IReadOnlyList<Tile> Merged { get; }

    public int MergeCount => Merged.Count;

    public int BiggestMerge => Merged.Count == 0 ? 0 : Merged.Max(t => t.Value);
}
