namespace Puzzle2048.Core;

/// <summary>The grid of tiles and the sliding and merging rules. Knows nothing about scoring or modes.</summary>
public sealed class GameBoard
{
    private readonly Tile?[,] _grid;
    private int _nextId = 1;

    public GameBoard(int size)
    {
        if (size < 2 || size > 8)
            throw new ArgumentOutOfRangeException(nameof(size));
        Size = size;
        _grid = new Tile?[size, size];
    }

    public int Size { get; }

    public Tile? this[int x, int y] => _grid[x, y];

    public IEnumerable<Tile> Tiles
    {
        get
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    if (_grid[x, y] is { } tile)
                        yield return tile;
        }
    }

    public int TileCount => Tiles.Count();

    public int EmptyCount => Size * Size - TileCount;

    public int MaxTile => Tiles.Select(t => t.Value).DefaultIfEmpty(0).Max();

    /// <summary>True while any swipe could still change the board.</summary>
    public bool HasMoves()
    {
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Tile? tile = _grid[x, y];
                if (tile is null)
                    return true;
                if (x + 1 < Size && _grid[x + 1, y]?.Value == tile.Value)
                    return true;
                if (y + 1 < Size && _grid[x, y + 1]?.Value == tile.Value)
                    return true;
            }
        }
        return false;
    }

    /// <summary>Puts a 2 (90%) or a 4 (10%) in a random empty cell. Empty cells are listed row by row, so a seeded Random always gives the same board.</summary>
    public Tile? SpawnRandom(Random random)
    {
        var empty = new List<(int X, int Y)>();
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                if (_grid[x, y] is null)
                    empty.Add((x, y));

        if (empty.Count == 0)
            return null;

        var (cx, cy) = empty[random.Next(empty.Count)];
        int value = random.NextDouble() < 0.9 ? 2 : 4;
        return Place(cx, cy, value);
    }

    public Tile Place(int x, int y, int value)
    {
        var tile = new Tile(_nextId++, value, x, y);
        _grid[x, y] = tile;
        return tile;
    }

    public void Clear()
    {
        Array.Clear(_grid);
    }

    public Tile? Remove(int x, int y)
    {
        Tile? tile = _grid[x, y];
        _grid[x, y] = null;
        return tile;
    }

    /// <summary>Slides every tile as far as it goes in the direction, merging equal neighbours once per move.</summary>
    public MoveOutcome Move(Direction direction)
    {
        (int dx, int dy) = direction switch
        {
            Direction.Up => (0, -1),
            Direction.Right => (1, 0),
            Direction.Down => (0, 1),
            _ => (-1, 0),
        };

        // Visit the tiles closest to the wall first, so they settle before the others arrive
        var xs = Enumerable.Range(0, Size).ToList();
        var ys = Enumerable.Range(0, Size).ToList();
        if (dx == 1) xs.Reverse();
        if (dy == 1) ys.Reverse();

        var slides = new List<TileSlide>();
        var slideByTile = new Dictionary<int, TileSlide>();
        var merged = new List<Tile>();
        var mergeResultIds = new HashSet<int>();
        long points = 0;
        bool moved = false;

        foreach (int x in xs)
        {
            foreach (int y in ys)
            {
                Tile? tile = _grid[x, y];
                if (tile is null)
                    continue;

                int farX = x, farY = y;
                int nextX = x + dx, nextY = y + dy;
                while (InBounds(nextX, nextY) && _grid[nextX, nextY] is null)
                {
                    farX = nextX;
                    farY = nextY;
                    nextX += dx;
                    nextY += dy;
                }

                Tile? next = InBounds(nextX, nextY) ? _grid[nextX, nextY] : null;

                if (next is not null && next.Value == tile.Value && !mergeResultIds.Contains(next.Id))
                {
                    var result = new Tile(_nextId++, tile.Value * 2, nextX, nextY);
                    var slide = new TileSlide(tile.Id, tile.Value, x, y, nextX, nextY) { MergedIntoId = result.Id };
                    slides.Add(slide);
                    slideByTile[tile.Id] = slide;
                    slideByTile[next.Id].MergedIntoId = result.Id;

                    _grid[x, y] = null;
                    _grid[nextX, nextY] = result;
                    mergeResultIds.Add(result.Id);
                    merged.Add(result);
                    points += result.Value;
                    moved = true;
                }
                else
                {
                    var slide = new TileSlide(tile.Id, tile.Value, x, y, farX, farY);
                    slides.Add(slide);
                    slideByTile[tile.Id] = slide;

                    if (farX != x || farY != y)
                    {
                        _grid[x, y] = null;
                        _grid[farX, farY] = tile;
                        tile.X = farX;
                        tile.Y = farY;
                        moved = true;
                    }
                }
            }
        }

        return new MoveOutcome(moved, points, slides, merged);
    }

    /// <summary>Values row by row (index = y * Size + x), 0 for an empty cell.</summary>
    public int[] ToValues()
    {
        var values = new int[Size * Size];
        foreach (Tile tile in Tiles)
            values[tile.Y * Size + tile.X] = tile.Value;
        return values;
    }

    public void LoadValues(IReadOnlyList<int> values)
    {
        if (values.Count != Size * Size)
            throw new ArgumentException("Wrong number of cells", nameof(values));

        Clear();
        for (int i = 0; i < values.Count; i++)
            if (values[i] > 0)
                Place(i % Size, i / Size, values[i]);
    }

    private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;
}
