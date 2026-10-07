package com.emeraldwall.puzzle2048.reminder;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

/** Alarms do not survive a reboot, so re-arm the daily reminder. */
public class BootReceiver extends BroadcastReceiver
{
    @Override
    public void onReceive(Context context, Intent intent)
    {
        if (Intent.ACTION_BOOT_COMPLETED.equals(intent.getAction()))
            ReminderScheduler.restoreIfEnabled(context);
    }
}
