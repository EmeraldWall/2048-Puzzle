using Android.Views;
using Puzzle2048.App.Services;
using View = Android.Views.View;

namespace Puzzle2048.App.Platforms.Android;

/// <summary>
/// Frame callbacks from Android's Choreographer, which fires on every display vsync at whatever rate the screen
/// runs. Frame times are on the System.nanoTime clock, so <see cref="Now"/> uses the same clock.
/// </summary>
public sealed class ChoreographerTicker : Java.Lang.Object, Choreographer.IFrameCallback, IFrameTicker
{
    private bool _running;
    private bool _posted;

    public long Now => Java.Lang.JavaSystem.NanoTime() / 1_000_000;

    public event Action<long>? Frame;

    public void Start()
    {
        _running = true;
        Post();
    }

    public void Stop() => _running = false;

    public void DoFrame(long frameTimeNanos)
    {
        _posted = false;
        if (!_running)
            return;
        // Ask for the next frame first, so a slow frame never makes the loop skip a beat
        Post();
        Frame?.Invoke(frameTimeNanos / 1_000_000);
    }

    private void Post()
    {
        if (_posted)
            return;
        _posted = true;
        Choreographer.Instance!.PostFrameCallback(this);
    }

    public void RequestHighRefreshRate(object? platformView, bool high)
    {
        if (platformView is not View view)
            return;

        // Android 15 and later: the view votes for a high frame rate and the system picks the best mode
        if (OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            view.RequestedFrameRate = high ? View.RequestedFrameRateCategoryHigh : View.RequestedFrameRateCategoryDefault;
            return;
        }

        // Android 6 to 14: pick the fastest display mode at the current resolution for this window
        if (!OperatingSystem.IsAndroidVersionAtLeast(23) || Platform.CurrentActivity?.Window is not { } window)
            return;
        Display? display = view.Display;
        if (display?.GetMode() is not { } current)
            return;

        int modeId = 0;
        if (high)
        {
            float best = current.RefreshRate;
            foreach (Display.Mode mode in display.GetSupportedModes() ?? [])
            {
                if (mode.PhysicalWidth == current.PhysicalWidth && mode.PhysicalHeight == current.PhysicalHeight && mode.RefreshRate > best + 0.5f)
                {
                    best = mode.RefreshRate;
                    modeId = mode.ModeId;
                }
            }
            if (modeId == 0)
                return;
        }

        WindowManagerLayoutParams? attributes = window.Attributes;
        if (attributes is null || attributes.PreferredDisplayModeId == modeId)
            return;
        attributes.PreferredDisplayModeId = modeId;
        window.Attributes = attributes;
    }
}
