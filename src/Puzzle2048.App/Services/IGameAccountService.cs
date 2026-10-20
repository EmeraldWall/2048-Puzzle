using Puzzle2048.Core;

namespace Puzzle2048.App.Services;

/// <summary>
/// The player's account: Google Play Games sign-in, a cloud copy of their progress, leaderboards and achievements.
/// Everything is optional. The game plays fully offline without it.
/// </summary>
public interface IGameAccountService
{
    /// <summary>False when the app has no Play Games project set up, which hides every account feature.</summary>
    bool IsAvailable { get; }

    bool IsSignedIn { get; }

    string? PlayerName { get; }

    bool IsSyncing { get; }

    /// <summary>Raised on the main thread when sign-in, the player name or the progress changes.</summary>
    event Action? Changed;

    /// <summary>Picks up an existing sign-in silently and syncs progress. Safe to call more than once.</summary>
    Task InitializeAsync();

    /// <summary>Shows the Play Games sign-in. Returns true when the player is signed in afterwards.</summary>
    Task<bool> SignInAsync();

    /// <summary>Merges the cloud copy into this phone and saves the result back. Does nothing when signed out.</summary>
    Task SyncAsync();

    /// <summary>Called when a run ends: posts scores and checks achievements.</summary>
    void ReportRun(GameMode mode, int size, long score, long? sprintMilliseconds);

    /// <summary>Called when a tile is made, for the tile achievements.</summary>
    void ReportTile(int value);

    Task ShowLeaderboardsAsync();

    Task ShowAchievementsAsync();

    /// <summary>Deletes the cloud copy of the player's progress. Returns false when that failed.</summary>
    Task<bool> DeleteCloudDataAsync();
}

public sealed class NoGameAccountService : IGameAccountService
{
    public bool IsAvailable => false;
    public bool IsSignedIn => false;
    public string? PlayerName => null;
    public bool IsSyncing => false;

    public event Action? Changed
    {
        add { }
        remove { }
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task<bool> SignInAsync() => Task.FromResult(false);
    public Task SyncAsync() => Task.CompletedTask;
    public void ReportRun(GameMode mode, int size, long score, long? sprintMilliseconds) { }
    public void ReportTile(int value) { }
    public Task ShowLeaderboardsAsync() => Task.CompletedTask;
    public Task ShowAchievementsAsync() => Task.CompletedTask;
    public Task<bool> DeleteCloudDataAsync() => Task.FromResult(true);
}
