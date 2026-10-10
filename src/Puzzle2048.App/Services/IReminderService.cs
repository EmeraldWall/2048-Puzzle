namespace Puzzle2048.App.Services;

/// <summary>One evening notification, only on days the Daily Challenge has not been played yet.</summary>
public interface IReminderService
{
    /// <summary>Asks for notification permission if needed and schedules the daily reminder.</summary>
    Task<bool> EnableAsync();

    void Disable();

    /// <summary>Re-arms the alarm for players who turned the reminder on (alarms do not survive a reboot).</summary>
    void RestoreIfEnabled();
}

/// <summary>Used where reminders are not available.</summary>
public sealed class NoReminderService : IReminderService
{
    public Task<bool> EnableAsync() => Task.FromResult(false);
    public void Disable() { }
    public void RestoreIfEnabled() { }
}
