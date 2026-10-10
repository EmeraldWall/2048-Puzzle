using Android.App;
using Android.Content;

namespace Puzzle2048.App.Platforms.Android;

/// <summary>Alarms do not survive a reboot, so the daily reminder is armed again when the phone starts.</summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter([Intent.ActionBootCompleted])]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action == Intent.ActionBootCompleted)
            new AndroidReminderService().RestoreIfEnabled();
    }
}
