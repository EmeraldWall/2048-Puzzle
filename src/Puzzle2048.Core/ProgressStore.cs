namespace Puzzle2048.Core;

/// <summary>
/// Everything that lasts between runs: player level, daily streak, records and the player's choices.
/// All of it lives on the device.
/// </summary>
public sealed class ProgressStore
{
    private const string TotalPointsKey = "total_points";
    private const string LastDailyDayKey = "last_daily_day";
    private const string StreakKey = "streak";
    private const string BestStreakKey = "best_streak";
    private const string DailyBestDayKey = "daily_best_day";
    private const string DailyBestScoreKey = "daily_best_score";
    private const string GoalMetDayKey = "goal_met_day";
    private const string DailyGoalsMetKey = "daily_goals_met";
    private const string SprintBestKey = "sprint_best_ms";
    private const string ReminderEnabledKey = "reminder_enabled";
    private const string ReminderPromptedKey = "reminder_prompted";
    private const string AdsRemovedKey = "ads_removed";
    private const string BackgroundKey = "background_color";

    /// <summary>A daily run counts toward the streak after this many moves.</summary>
    public const int StreakMinMoves = 10;

    /// <summary>Level points for completing a daily goal.</summary>
    public const int GoalBonusPoints = 500;

    /// <summary>The default game background (bright sky).</summary>
    public const string DefaultBackground = "#7FD3FF";

    private const double PointsPerLevelUnit = 400.0;

    private readonly IKeyValueStorage _storage;

    public ProgressStore(IKeyValueStorage storage) => _storage = storage;

    // ---- Level: level n starts at 400 * (n - 1)^2 points, so the first levels come quickly
    public long TotalPoints => _storage.GetLong(TotalPointsKey, 0);

    public void AddPoints(long points)
    {
        if (points > 0)
            _storage.SetLong(TotalPointsKey, TotalPoints + points);
    }

    public static int LevelFor(long points) => (int)Math.Floor(Math.Sqrt(points / PointsPerLevelUnit)) + 1;

    public static long LevelStart(int level)
    {
        long n = level - 1;
        return (long)(PointsPerLevelUnit * n * n);
    }

    // ---- Daily challenge
    public bool HasPlayedDaily(int day) => _storage.GetInt(LastDailyDayKey, -1) == day;

    public bool EverPlayedDaily => _storage.GetInt(LastDailyDayKey, -1) >= 0;

    /// <summary>The streak the player should see: broken once a whole day was missed.</summary>
    public int CurrentStreak(int today)
    {
        int last = _storage.GetInt(LastDailyDayKey, -1);
        return last == today || last == today - 1 ? _storage.GetInt(StreakKey, 0) : 0;
    }

    public int BestStreak => _storage.GetInt(BestStreakKey, 0);

    public int DailyBestScore(int day) =>
        _storage.GetInt(DailyBestDayKey, -1) == day ? _storage.GetInt(DailyBestScoreKey, 0) : 0;

    public bool IsGoalMet(int day) => _storage.GetInt(GoalMetDayKey, -1) == day;

    public int DailyGoalsMet => _storage.GetInt(DailyGoalsMetKey, 0);

    /// <summary>Safe to call over and over during a run.</summary>
    public void RecordDailyRun(int day, long score, bool goalMet, int moves)
    {
        if (_storage.GetInt(DailyBestDayKey, -1) != day || score > _storage.GetInt(DailyBestScoreKey, 0))
        {
            _storage.SetInt(DailyBestDayKey, day);
            _storage.SetInt(DailyBestScoreKey, (int)Math.Min(score, int.MaxValue));
        }

        if (goalMet && _storage.GetInt(GoalMetDayKey, -1) != day)
        {
            _storage.SetInt(GoalMetDayKey, day);
            _storage.SetInt(DailyGoalsMetKey, DailyGoalsMet + 1);
            AddPoints(GoalBonusPoints);
        }

        int last = _storage.GetInt(LastDailyDayKey, -1);
        if (last != day && (goalMet || moves >= StreakMinMoves))
        {
            int streak = NextStreak(last, day, _storage.GetInt(StreakKey, 0));
            _storage.SetInt(StreakKey, streak);
            _storage.SetInt(LastDailyDayKey, day);
            _storage.SetInt(BestStreakKey, Math.Max(streak, BestStreak));
        }
    }

    /// <summary>The streak after playing on <paramref name="day"/>: it continues from yesterday, otherwise restarts.</summary>
    public static int NextStreak(int lastPlayedDay, int day, int currentStreak) =>
        lastPlayedDay == day - 1 ? currentStreak + 1 : 1;

    // ---- Records
    public long HighScore(GameMode mode, int size) => _storage.GetLong($"high_{mode}_{size}", 0);

    /// <summary>Stores the score if it beats the record. Returns true for a new record.</summary>
    public bool RecordHighScore(GameMode mode, int size, long score)
    {
        if (score <= HighScore(mode, size))
            return false;
        _storage.SetLong($"high_{mode}_{size}", score);
        return true;
    }

    /// <summary>Fastest Sprint time in milliseconds, or -1 when none.</summary>
    public long SprintBestMs => _storage.GetLong(SprintBestKey, -1);

    public bool RecordSprintTime(long milliseconds)
    {
        long best = SprintBestMs;
        if (best >= 0 && milliseconds >= best)
            return false;
        _storage.SetLong(SprintBestKey, milliseconds);
        return true;
    }

    // ---- Classic autosave (one slot per board size)
    public string? LoadClassicGame(int size) => _storage.GetString($"classic_save_{size}", "") is { Length: > 0 } json ? json : null;

    public void SaveClassicGame(int size, string json) => _storage.SetString($"classic_save_{size}", json);

    public void ClearClassicGame(int size) => _storage.SetString($"classic_save_{size}", "");

    // ---- Choices
    public bool ReminderEnabled
    {
        get => _storage.GetBool(ReminderEnabledKey, false);
        set => _storage.SetBool(ReminderEnabledKey, value);
    }

    public bool ReminderPrompted
    {
        get => _storage.GetBool(ReminderPromptedKey, false);
        set => _storage.SetBool(ReminderPromptedKey, value);
    }

    /// <summary>Local copy of the purchase. Google Play stays the source of truth and refreshes this.</summary>
    public bool AdsRemoved
    {
        get => _storage.GetBool(AdsRemovedKey, false);
        set => _storage.SetBool(AdsRemovedKey, value);
    }

    public string BackgroundColor
    {
        get => _storage.GetString(BackgroundKey, DefaultBackground);
        set => _storage.SetString(BackgroundKey, value);
    }
}
