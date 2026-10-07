package com.emeraldwall.puzzle2048.modes;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.TimeZone;

/**
 * Today's puzzle. Everyone gets the same tile sequence and the same goal, because both are
 * derived from the UTC calendar day. The day rolls over at 00:00 UTC.
 */
public final class DailyChallenge
{
    public enum GoalType { TILE, SCORE, TILE_IN_MOVES }

    public static final class Goal
    {
        public final int rows;
        public final GoalType type;
        public final int target;
        public final int moveLimit;

        Goal(int rows, GoalType type, int target, int moveLimit)
        {
            this.rows = rows;
            this.type = type;
            this.target = target;
            this.moveLimit = moveLimit;
        }

        public String title()
        {
            switch (type)
            {
                case TILE:
                    return "Make a " + target + " tile";
                case SCORE:
                    return "Score " + target + " points";
                default:
                    return "Make " + target + " in " + moveLimit + " moves";
            }
        }

        public boolean isMet(int bestTile, long score, int moves)
        {
            switch (type)
            {
                case TILE:
                    return bestTile >= target;
                case SCORE:
                    return score >= target;
                default:
                    return bestTile >= target && moves <= moveLimit;
            }
        }

        /** True when the goal can no longer be reached in this run. */
        public boolean isLost(int moves)
        {
            return type == GoalType.TILE_IN_MOVES && moves > moveLimit;
        }

        public String progress(int bestTile, long score, int moves)
        {
            switch (type)
            {
                case TILE:
                    return "best tile " + bestTile;
                case SCORE:
                    return score + " / " + target;
                default:
                    return "move " + moves + " / " + moveLimit;
            }
        }
    }

    // Rotates with the day number, so a given weekday does not always get the same goal.
    private static final Goal[] GOALS = {
            new Goal(4, GoalType.TILE, 512, 0),
            new Goal(4, GoalType.SCORE, 3000, 0),
            new Goal(5, GoalType.TILE, 1024, 0),
            new Goal(4, GoalType.TILE_IN_MOVES, 512, 220),
            new Goal(5, GoalType.SCORE, 7000, 0),
            new Goal(4, GoalType.SCORE, 5000, 0),
            new Goal(6, GoalType.TILE, 2048, 0),
            new Goal(4, GoalType.TILE_IN_MOVES, 256, 100),
            new Goal(5, GoalType.TILE_IN_MOVES, 512, 150),
    };

    private DailyChallenge() {}

    public static int dayNumber()
    {
        return (int) (System.currentTimeMillis() / 86_400_000L);
    }

    public static Goal goalFor(int day)
    {
        return GOALS[Math.floorMod(day, GOALS.length)];
    }

    public static long seedFor(int day)
    {
        return day * 2_654_435_761L + 40_503L;
    }

    /** For example "Oct 7". */
    public static String label(int day)
    {
        SimpleDateFormat format = new SimpleDateFormat("MMM d", Locale.US);
        format.setTimeZone(TimeZone.getTimeZone("UTC"));
        return format.format(new Date(day * 86_400_000L));
    }
}
