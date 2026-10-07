package com.emeraldwall.puzzle2048.reminder;

import android.app.AlarmManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;

import com.emeraldwall.puzzle2048.modes.ProgressStore;

import java.util.Calendar;

/** Schedules one inexact daily alarm, around 18:00 local time. No exact-alarm permission needed. */
public final class ReminderScheduler
{
    private static final int REQUEST_CODE = 4821;
    private static final int REMINDER_HOUR = 18;

    private ReminderScheduler() {}

    public static void enable(Context context)
    {
        ProgressStore.setReminderEnabled(context, true);
        schedule(context);
    }

    public static void disable(Context context)
    {
        ProgressStore.setReminderEnabled(context, false);
        AlarmManager alarms = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        alarms.cancel(pendingIntent(context));
    }

    /** Re-arms the alarm if the player enabled it; used on boot. */
    public static void restoreIfEnabled(Context context)
    {
        if (ProgressStore.isReminderEnabled(context))
            schedule(context);
    }

    private static void schedule(Context context)
    {
        Calendar first = Calendar.getInstance();
        first.set(Calendar.HOUR_OF_DAY, REMINDER_HOUR);
        first.set(Calendar.MINUTE, 0);
        first.set(Calendar.SECOND, 0);
        if (first.getTimeInMillis() <= System.currentTimeMillis())
            first.add(Calendar.DAY_OF_YEAR, 1);

        AlarmManager alarms = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        alarms.setInexactRepeating(AlarmManager.RTC, first.getTimeInMillis(),
                AlarmManager.INTERVAL_DAY, pendingIntent(context));
    }

    private static PendingIntent pendingIntent(Context context)
    {
        Intent intent = new Intent(context, ReminderReceiver.class);
        return PendingIntent.getBroadcast(context, REQUEST_CODE, intent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }
}
