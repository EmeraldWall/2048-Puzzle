namespace Puzzle2048.App.Services;

public enum Sfx
{
    Move,
    Tap,
    Combo,
    Milestone,
    Win,
    LevelUp,
    Lose,
    Whoosh,
}

/// <summary>Short sound effects. Silent when the player switched sound off in Settings.</summary>
public interface ISoundService
{
    /// <summary>Loads the sounds in the background. Safe to call more than once.</summary>
    Task PreloadAsync();

    void Play(Sfx sound);

    /// <summary>The merge pop, pitched higher for bigger tiles.</summary>
    void PlayMerge(int tileValue);
}

public sealed class NoSoundService : ISoundService
{
    public Task PreloadAsync() => Task.CompletedTask;
    public void Play(Sfx sound) { }
    public void PlayMerge(int tileValue) { }
}
