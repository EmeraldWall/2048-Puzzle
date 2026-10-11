using Puzzle2048.Core;

namespace Puzzle2048.Tests;

public class ProgressTests
{
    [Fact]
    public void DailyChallengeIsTheSameForEveryoneOnADay()
    {
        Assert.Equal(DailyChallenge.SeedFor(20000), DailyChallenge.SeedFor(20000));
        Assert.NotEqual(DailyChallenge.SeedFor(20000), DailyChallenge.SeedFor(20001));
        Assert.Equal(DailyChallenge.GoalFor(20000), DailyChallenge.GoalFor(20000));
    }

    [Fact]
    public void EveryDayHasAPlayableGoal()
    {
        for (int day = -5; day < 500; day++)
        {
            DailyGoal goal = DailyChallenge.GoalFor(day);
            Assert.InRange(goal.Size, 4, 6);
            Assert.True(goal.Target > 0);
            Assert.False(string.IsNullOrEmpty(goal.Title));
        }
    }

    [Fact]
    public void GoalRules()
    {
        var tile = new DailyGoal(4, GoalType.Tile, 512);
        Assert.False(tile.IsMet(256, 99999, 10));
        Assert.True(tile.IsMet(512, 0, 10));

        var score = new DailyGoal(4, GoalType.Score, 3000);
        Assert.False(score.IsMet(1024, 2999, 10));
        Assert.True(score.IsMet(8, 3000, 10));

        var fast = new DailyGoal(4, GoalType.TileInMoves, 512, 220);
        Assert.True(fast.IsMet(512, 0, 220));
        Assert.False(fast.IsMet(512, 0, 221));
        Assert.False(fast.IsLost(220));
        Assert.True(fast.IsLost(221));
        Assert.False(tile.IsLost(100000));
    }

    [Fact]
    public void DayNumberAndLabelUseUtc()
    {
        var oct7 = new DateTimeOffset(2026, 10, 7, 23, 59, 0, TimeSpan.Zero);
        int day = DailyChallenge.DayNumber(oct7);
        Assert.Equal("Oct 7", DailyChallenge.Label(day));
        Assert.Equal(day + 1, DailyChallenge.DayNumber(oct7.AddMinutes(2)));

        // The same instant in another time zone is the same day
        Assert.Equal(day, DailyChallenge.DayNumber(oct7.ToOffset(TimeSpan.FromHours(5.5))));
    }

    [Fact]
    public void StreakContinuesOnlyFromYesterday()
    {
        Assert.Equal(1, ProgressStore.NextStreak(-1, 100, 0));
        Assert.Equal(4, ProgressStore.NextStreak(99, 100, 3));
        Assert.Equal(1, ProgressStore.NextStreak(97, 100, 9));
    }

    [Fact]
    public void DailyRunCountsAfterTenMovesOrAGoal()
    {
        var progress = new ProgressStore(new MemoryStorage());

        progress.RecordDailyRun(100, 500, goalMet: false, moves: 9);
        Assert.False(progress.HasPlayedDaily(100));
        Assert.Equal(0, progress.CurrentStreak(100));

        progress.RecordDailyRun(100, 600, goalMet: false, moves: 10);
        Assert.True(progress.HasPlayedDaily(100));
        Assert.Equal(1, progress.CurrentStreak(100));
        Assert.Equal(600, progress.DailyBestScore(100));

        // Recording again the same day never changes the streak and keeps the best score
        progress.RecordDailyRun(100, 300, goalMet: false, moves: 50);
        Assert.Equal(1, progress.CurrentStreak(100));
        Assert.Equal(600, progress.DailyBestScore(100));
    }

    [Fact]
    public void StreakGrowsDailyAndBreaksAfterAMissedDay()
    {
        var progress = new ProgressStore(new MemoryStorage());
        progress.RecordDailyRun(100, 1, goalMet: true, moves: 1);
        progress.RecordDailyRun(101, 1, goalMet: true, moves: 1);
        progress.RecordDailyRun(102, 1, goalMet: false, moves: 20);

        Assert.Equal(3, progress.CurrentStreak(102));
        Assert.Equal(3, progress.CurrentStreak(103));   // still alive, today not yet played
        Assert.Equal(0, progress.CurrentStreak(104));   // a whole day missed
        Assert.Equal(3, progress.BestStreak);

        progress.RecordDailyRun(105, 1, goalMet: true, moves: 1);
        Assert.Equal(1, progress.CurrentStreak(105));
        Assert.Equal(3, progress.BestStreak);
    }

    [Fact]
    public void CompletingAGoalAddsLevelPointsOnce()
    {
        var progress = new ProgressStore(new MemoryStorage());
        progress.RecordDailyRun(100, 10, goalMet: true, moves: 5);
        progress.RecordDailyRun(100, 20, goalMet: true, moves: 6);

        Assert.Equal(ProgressStore.GoalBonusPoints, progress.TotalPoints);
        Assert.True(progress.IsGoalMet(100));
        Assert.Equal(1, progress.DailyGoalsMet);
    }

    [Fact]
    public void LevelsGrowAndStayConsistent()
    {
        Assert.Equal(1, ProgressStore.LevelFor(0));
        Assert.Equal(1, ProgressStore.LevelFor(399));
        Assert.Equal(2, ProgressStore.LevelFor(400));

        for (int level = 1; level < 60; level++)
        {
            Assert.Equal(level, ProgressStore.LevelFor(ProgressStore.LevelStart(level)));
            Assert.Equal(level, ProgressStore.LevelFor(ProgressStore.LevelStart(level + 1) - 1));
        }
    }

    [Fact]
    public void RecordsOnlyImprove()
    {
        var progress = new ProgressStore(new MemoryStorage());

        Assert.True(progress.RecordHighScore(GameMode.Classic, 4, 100));
        Assert.False(progress.RecordHighScore(GameMode.Classic, 4, 50));
        Assert.True(progress.RecordHighScore(GameMode.Classic, 4, 150));
        Assert.Equal(0, progress.HighScore(GameMode.Classic, 5));
        Assert.Equal(0, progress.HighScore(GameMode.TimeAttack, 4));

        Assert.Equal(-1, progress.SprintBestMs);
        Assert.True(progress.RecordSprintTime(90_000));
        Assert.False(progress.RecordSprintTime(95_000));
        Assert.True(progress.RecordSprintTime(80_000));
        Assert.Equal(80_000, progress.SprintBestMs);
    }

    [Fact]
    public void ClassicSaveSlotsAreSeparatePerSize()
    {
        var progress = new ProgressStore(new MemoryStorage());
        progress.SaveClassicGame(4, "four");
        progress.SaveClassicGame(5, "five");

        Assert.Equal("four", progress.LoadClassicGame(4));
        Assert.Equal("five", progress.LoadClassicGame(5));
        Assert.Null(progress.LoadClassicGame(6));

        progress.ClearClassicGame(4);
        Assert.Null(progress.LoadClassicGame(4));
    }
}

public class StarTests
{
    [Fact]
    public void StarsRewardPlayingTheGoalAndAHighScore()
    {
        var goal = new DailyGoal(4, GoalType.Tile, 512);
        Assert.Equal(0, goal.Stars(goalMet: false, score: 100, moves: 5));
        Assert.Equal(1, goal.Stars(goalMet: false, score: 100, moves: 10));
        Assert.Equal(2, goal.Stars(goalMet: true, score: 100, moves: 50));
        Assert.Equal(3, goal.Stars(goalMet: true, score: goal.ThreeStarScore, moves: 50));
    }

    [Fact]
    public void GoalProgressRunsFromZeroToOne()
    {
        var tile = new DailyGoal(4, GoalType.Tile, 512);
        Assert.Equal(0, tile.Fraction(0, 0, 0));
        Assert.Equal(1, tile.Fraction(512, 0, 0));
        Assert.Equal(1, tile.Fraction(1024, 0, 0));
        Assert.InRange(tile.Fraction(64, 0, 0), 0.6, 0.7);

        var score = new DailyGoal(4, GoalType.Score, 3000);
        Assert.Equal(0.5, score.Fraction(0, 1500, 0), 3);
    }

    [Fact]
    public void BestDailyStarsAreKeptPerDay()
    {
        var progress = new ProgressStore(new MemoryStorage());
        Assert.True(progress.RecordDailyStars(100, 2));
        Assert.False(progress.RecordDailyStars(100, 1));
        Assert.True(progress.RecordDailyStars(100, 3));
        Assert.Equal(3, progress.DailyStars(100));
        Assert.Equal(0, progress.DailyStars(101));
    }

    [Fact]
    public void SoundIsOnByDefault()
    {
        var progress = new ProgressStore(new MemoryStorage());
        Assert.True(progress.SoundEnabled);
        progress.SoundEnabled = false;
        Assert.False(progress.SoundEnabled);
    }
}
