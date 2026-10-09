using Android.App;
using Android.Content;
using Puzzle2048.App.Services;

namespace Puzzle2048.App.Platforms.Android;

/// <summary>
/// Schedules one inexact daily alarm around 18:00 local time. It needs no exact-alarm permission.
/// The alarm itself is handled by <see cref="ReminderReceiver"/>.
/// </summary>
public sealed class AndroidReminderService : IReminderService
{
    private const int RequestCode = 4821;
    private const int ReminderHour = 18;

    public async Task<bool> EnableAsync()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            PermissionStatus status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
                return false;
        }

        AppServices.Progress.ReminderEnabled = true;
        Schedule();
        return true;
    }

    public void Disable()
    {
        AppServices.Progress.ReminderEnabled = false;
        AlarmManager? alarms = global::Android.App.Application.Context.GetSystemService(Context.AlarmService) as AlarmManager;
        alarms?.Cancel(CreatePendingIntent());
    }

    public void RestoreIfEnabled()
    {
        if (AppServices.Progress.ReminderEnabled)
            Schedule();
    }

    private static void Schedule()
    {
        DateTime first = DateTime.Today.AddHours(ReminderHour);
        if (first <= DateTime.Now)
            first = first.AddDays(1);

        long triggerAt = new DateTimeOffset(first).ToUnixTimeMilliseconds();
        AlarmManager? alarms = global::Android.App.Application.Context.GetSystemService(Context.AlarmService) as AlarmManager;
        alarms?.SetInexactRepeating(AlarmType.Rtc, triggerAt, AlarmManager.IntervalDay, CreatePendingIntent());
    }

    private static PendingIntent CreatePendingIntent()
    {
        var intent = new Intent(global::Android.App.Application.Context, typeof(ReminderReceiver));
        return PendingIntent.GetBroadcast(global::Android.App.Application.Context, RequestCode, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }
}
