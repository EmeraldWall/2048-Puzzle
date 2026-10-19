namespace Puzzle2048.App.Services;

/// <summary>
/// Calls back once per screen refresh, in step with the display (60, 90, 120 or 144 times a second), with the
/// time that frame will be shown. Animations that read this time move by exactly one refresh each frame.
/// </summary>
public interface IFrameTicker
{
    /// <summary>The current time in milliseconds, on the same clock as the frame times.</summary>
    long Now { get; }

    /// <summary>Raised on the main thread for every refresh while started.</summary>
    event Action<long>? Frame;

    void Start();

    void Stop();

    /// <summary>Asks the screen for its highest refresh rate while the game is open, or lets it go back to normal.</summary>
    void RequestHighRefreshRate(object? platformView, bool high);
}

/// <summary>Fallback for platforms without a vsync callback: a plain 60 Hz timer.</summary>
public sealed class TimerFrameTicker : IFrameTicker
{
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private IDispatcherTimer? _timer;

    public long Now => _clock.ElapsedMilliseconds;

    public event Action<long>? Frame;

    public void Start()
    {
        if (_timer is not null)
            return;
        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += (_, _) => Frame?.Invoke(Now);
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }

    public void RequestHighRefreshRate(object? platformView, bool high) { }
}
