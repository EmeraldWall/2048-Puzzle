package com.emeraldwall.puzzle2048.modes;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNotEquals;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

import java.util.Random;

public class ModesLogicTest
{
    @Test
    public void dailySeedIsStableAndDiffersPerDay()
    {
        assertEquals(DailyChallenge.seedFor(20000), DailyChallenge.seedFor(20000));
        assertNotEquals(DailyChallenge.seedFor(20000), DailyChallenge.seedFor(20001));

        // The same seed must give the same tile sequence on every device
        Random a = new Random(DailyChallenge.seedFor(20000));
        Random b = new Random(DailyChallenge.seedFor(20000));
        for (int i = 0; i < 50; i++)
            assertEquals(a.nextInt(16), b.nextInt(16));
    }

    @Test
    public void everyDayHasAPlayableGoal()
    {
        for (int day = 0; day < 400; day++)
        {
            DailyChallenge.Goal goal = DailyChallenge.goalFor(day);
            assertTrue(goal.rows >= 4 && goal.rows <= 6);
            assertTrue(goal.target > 0);
            assertFalse(goal.title().isEmpty());
        }
        // Negative day numbers must not crash the lookup
        DailyChallenge.goalFor(-3);
    }

    @Test
    public void goalRules()
    {
        DailyChallenge.Goal tile = DailyChallenge.goalFor(0);              // 512 tile
        assertFalse(tile.isMet(256, 9999, 10));
        assertTrue(tile.isMet(512, 0, 10));

        DailyChallenge.Goal score = DailyChallenge.goalFor(1);             // 3000 points
        assertFalse(score.isMet(1024, 2999, 10));
        assertTrue(score.isMet(8, 3000, 10));

        DailyChallenge.Goal fast = DailyChallenge.goalFor(3);              // 512 in 220 moves
        assertTrue(fast.isMet(512, 0, 220));
        assertFalse(fast.isMet(512, 0, 221));
        assertFalse(fast.isLost(220));
        assertTrue(fast.isLost(221));
        assertFalse(tile.isLost(100000));
    }

    @Test
    public void dayLabelUsesUtc()
    {
        // 2026-10-07 00:00 UTC
        int day = (int) (java.time.LocalDate.of(2026, 10, 7).toEpochDay());
        assertEquals("Oct 7", DailyChallenge.label(day));
    }

    @Test
    public void streakContinuesOnlyFromYesterday()
    {
        assertEquals(1, ProgressStore.nextStreak(-1, 100, 0));   // first ever
        assertEquals(4, ProgressStore.nextStreak(99, 100, 3));   // consecutive day
        assertEquals(1, ProgressStore.nextStreak(97, 100, 9));   // missed days restart
    }

    @Test
    public void levelsGrowAndStayConsistent()
    {
        assertEquals(1, ProgressStore.levelFor(0));
        assertEquals(1, ProgressStore.levelFor(399));
        assertEquals(2, ProgressStore.levelFor(400));
        for (int level = 1; level < 60; level++)
        {
            long start = ProgressStore.levelStart(level);
            assertEquals(level, ProgressStore.levelFor(start));
            assertEquals(level, ProgressStore.levelFor(ProgressStore.levelStart(level + 1) - 1));
        }
    }
}
