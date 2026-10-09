namespace Puzzle2048.Core;

/// <summary>A numbered tile. The Id stays the same while the tile slides, so animations can follow it.</summary>
public sealed class Tile
{
    public Tile(int id, int value, int x, int y)
    {
        Id = id;
        Value = value;
        X = x;
        Y = y;
    }

    public int Id { get; }
    public int Value { get; }
    public int X { get; internal set; }
    public int Y { get; internal set; }

    public override string ToString() => $"#{Id} {Value} at ({X},{Y})";
}
