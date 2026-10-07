package com.emeraldwall.puzzle2048.modes;

import android.content.Context;
import android.content.SharedPreferences;

/** Everything that persists across runs: level, daily streak, mode records and reminder choice. */
public final class ProgressStore
{
    private static final String PREFS = "progress";
    private static final String TOTAL_POINTS = "total_points";
    private static final String LAST_DAILY_DAY = "last_daily_day";
    private static final String STREAK = "streak";
    private static final String BEST_STREAK = "best_streak";
    private static final String DAILY_BEST_DAY = "daily_best_day";
    private static final String DAILY_BEST_SCORE = "daily_best_score";
    private static final String GOAL_MET_DAY = "goal_met_day";
    private static final String DAILY_GOALS_MET = "daily_goals_met";
    private static final String SPRINT_BEST_MS = "sprint_best_ms";
    private static final String REMINDER_ENABLED = "reminder_enabled";
    private static final String REMINDER_PROMPTED = "reminder_prompted";

    /** A run counts toward the daily streak once the player has made this many moves. */
    public static final int STREAK_MIN_MOVES = 10;
    /** Points toward the player level for completing a daily goal. */
    public static final int GOAL_BONUS_POINTS = 500;
    private static final double POINTS_PER_LEVEL_UNIT = 400.0;

    private ProgressStore() {}

    private static SharedPreferences prefs(Context context)
    {
        return context.getApplicationContext().getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    // Level: level n starts at 400 * (n - 1)^2 points, so early levels come quickly.
    public static long totalPoints(Context context)
    {
        return prefs(context).getLong(TOTAL_POINTS, 0);
    }

    public static void addPoints(Context context, long points)
    {
        if (points <= 0)
            return;
        SharedPreferences p = prefs(context);
        p.edit().putLong(TOTAL_POINTS, p.getLong(TOTAL_POINTS, 0) + points).apply();
    }

    public static int levelFor(long points)
    {
        return (int) Math.floor(Math.sqrt(points / POINTS_PER_LEVEL_UNIT)) + 1;
    }

    public static long levelStart(int level)
    {
        long n = level - 1;
        return (long) (POINTS_PER_LEVEL_UNIT * n * n);
    }

    // Daily challenge
    public static boolean hasPlayedDaily(Context context, int day)
    {
        return prefs(context).getInt(LAST_DAILY_DAY, -1) == day;
    }

    public static boolean everPlayedDaily(Context context)
    {
        return prefs(context).getInt(LAST_DAILY_DAY, -1) >= 0;
    }

    /** The streak as the player should see it: broken once a whole day was missed. */
    public static int currentStreak(Context context, int today)
    {
        SharedPreferences p = prefs(context);
        int last = p.getInt(LAST_DAILY_DAY, -1);
        return (last == today || last == today - 1) ? p.getInt(STREAK, 0) : 0;
    }

    public static int bestStreak(Context context)
    {
        return prefs(context).getInt(BEST_STREAK, 0);
    }

    public static int dailyBestScore(Context context, int day)
    {
        SharedPreferences p = prefs(context);
        return p.getInt(DAILY_BEST_DAY, -1) == day ? p.getInt(DAILY_BEST_SCORE, 0) : 0;
    }

    public static boolean isGoalMet(Context context, int day)
    {
        return prefs(context).getInt(GOAL_MET_DAY, -1) == day;
    }

    public static int dailyGoalsMet(Context context)
    {
        return prefs(context).getInt(DAILY_GOALS_MET, 0);
    }

    /**
     * Safe to call repeatedly during a run: keeps the best score of the day and extends the streak
     * the first time the day counts as played.
     */
    public static void recordDailyRun(Context context, int day, long score, boolean goalMet, int moves)
    {
        SharedPreferences p = prefs(context);
        SharedPreferences.Editor e = p.edit();

        if (p.getInt(DAILY_BEST_DAY, -1) != day || score > p.getInt(DAILY_BEST_SCORE, 0))
        {
            e.putInt(DAILY_BEST_DAY, day);
            e.putInt(DAILY_BEST_SCORE, (int) Math.min(score, Integer.MAX_VALUE));
        }

        if (goalMet && p.getInt(GOAL_MET_DAY, -1) != day)
        {
            e.putInt(GOAL_MET_DAY, day);
            e.putInt(DAILY_GOALS_MET, p.getInt(DAILY_GOALS_MET, 0) + 1);
            e.putLong(TOTAL_POINTS, p.getLong(TOTAL_POINTS, 0) + GOAL_BONUS_POINTS);
        }

        int last = p.getInt(LAST_DAILY_DAY, -1);
        if (last != day && (goalMet || moves >= STREAK_MIN_MOVES))
        {
            int streak = nextStreak(last, day, p.getInt(STREAK, 0));
            e.putInt(STREAK, streak);
            e.putInt(LAST_DAILY_DAY, day);
            e.putInt(BEST_STREAK, Math.max(streak, p.getInt(BEST_STREAK, 0)));
        }
        e.apply();
    }

    /** Streak after playing on {@code day}: it continues from the day before, otherwise restarts at 1. */
    static int nextStreak(int lastPlayedDay, int day, int currentStreak)
    {
        return lastPlayedDay == day - 1 ? currentStreak + 1 : 1;
    }

    // Sprint: fastest time to the target tile, in milliseconds (-1 when never finished)
    public static long sprintBestMs(Context context)
    {
        return prefs(context).getLong(SPRINT_BEST_MS, -1);
    }

    public static void setSprintBestMs(Context context, long ms)
    {
        prefs(context).edit().putLong(SPRINT_BEST_MS, ms).apply();
    }

    // Reminder
    public static boolean isReminderEnabled(Context context)
    {
        return prefs(context).getBoolean(REMINDER_ENABLED, false);
    }

    public static void setReminderEnabled(Context context, boolean enabled)
    {
        prefs(context).edit().putBoolean(REMINDER_ENABLED, enabled).apply();
    }

    public static boolean wasReminderPrompted(Context context)
    {
        return prefs(context).getBoolean(REMINDER_PROMPTED, false);
    }

    public static void setReminderPrompted(Context context)
    {
        prefs(context).edit().putBoolean(REMINDER_PROMPTED, true).apply();
    }
}
