using Android.App;
using Android.Content;
using AndroidX.Core.App;
using Puzzle2048.App.Services;
using Puzzle2048.Core;

namespace Puzzle2048.App.Platforms.Android;

/// <summary>Fires once a day. Stays silent when the daily challenge was already played.</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public class ReminderReceiver : BroadcastReceiver
{
    private const string ChannelId = "daily_challenge";
    private const int NotificationId = 7001;

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null)
            return;

        ProgressStore progress = AppServices.Progress;
        int today = DailyChallenge.Today();
        if (!progress.ReminderEnabled || progress.HasPlayedDaily(today))
            return;

        if (OperatingSystem.IsAndroidVersionAtLeast(33)
            && context.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) != global::Android.Content.PM.Permission.Granted)
            return;

        CreateChannel(context);

        int streak = progress.CurrentStreak(today);
        string text = streak > 0
            ? $"Keep your {streak} day streak alive. Today's challenge is ready."
            : "Today's challenge is ready. Can you beat it?";

        Intent? launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        PendingIntent? open = launch is null ? null
            : PendingIntent.GetActivity(context, 0, launch, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var builder = new NotificationCompat.Builder(context, ChannelId);
        builder.SetSmallIcon(Resource.Drawable.ic_stat_daily);
        builder.SetContentTitle("2048 Daily Challenge");
        builder.SetContentText(text);
        builder.SetContentIntent(open);
        builder.SetAutoCancel(true);
        builder.SetPriority(NotificationCompat.PriorityDefault);
        Notification notification = builder.Build()!;

        NotificationManagerCompat.From(context)!.Notify(NotificationId, notification);
    }

    private static void CreateChannel(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var channel = new NotificationChannel(ChannelId, "Daily challenge", NotificationImportance.Default)
        {
            Description = "A daily reminder to play today's challenge",
        };
        (context.GetSystemService(Context.NotificationService) as NotificationManager)?.CreateNotificationChannel(channel);
    }
}
