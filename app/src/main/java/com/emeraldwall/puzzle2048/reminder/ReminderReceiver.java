package com.emeraldwall.puzzle2048.reminder;

import android.Manifest;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Build;

import androidx.core.app.NotificationCompat;
import androidx.core.app.NotificationManagerCompat;
import androidx.core.content.ContextCompat;

import com.emeraldwall.puzzle2048.MainMenuActivity;
import com.emeraldwall.puzzle2048.R;
import com.emeraldwall.puzzle2048.modes.DailyChallenge;
import com.emeraldwall.puzzle2048.modes.ProgressStore;

/** Fires once a day; stays silent when the daily challenge was already played. */
public class ReminderReceiver extends BroadcastReceiver
{
    private static final String CHANNEL_ID = "daily_challenge";
    private static final int NOTIFICATION_ID = 7001;

    @Override
    public void onReceive(Context context, Intent intent)
    {
        int today = DailyChallenge.dayNumber();
        if (!ProgressStore.isReminderEnabled(context) || ProgressStore.hasPlayedDaily(context, today))
            return;

        if (Build.VERSION.SDK_INT >= 33 && ContextCompat.checkSelfPermission(context,
                Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED)
            return;

        createChannel(context);

        int streak = ProgressStore.currentStreak(context, today);
        String text = streak > 0
                ? "Keep your " + streak + " day streak alive. Today's challenge is ready."
                : "Today's challenge is ready. Can you beat it?";

        PendingIntent open = PendingIntent.getActivity(context, 0,
                new Intent(context, MainMenuActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK),
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);

        NotificationCompat.Builder builder = new NotificationCompat.Builder(context, CHANNEL_ID)
                .setSmallIcon(R.drawable.ic_stat_daily)
                .setContentTitle("2048 Daily Challenge")
                .setContentText(text)
                .setContentIntent(open)
                .setAutoCancel(true)
                .setPriority(NotificationCompat.PRIORITY_DEFAULT);

        NotificationManagerCompat.from(context).notify(NOTIFICATION_ID, builder.build());
    }

    private static void createChannel(Context context)
    {
        if (Build.VERSION.SDK_INT < 26)
            return;
        NotificationChannel channel = new NotificationChannel(CHANNEL_ID, "Daily challenge",
                NotificationManager.IMPORTANCE_DEFAULT);
        channel.setDescription("A daily reminder to play today's challenge");
        context.getSystemService(NotificationManager.class).createNotificationChannel(channel);
    }
}
