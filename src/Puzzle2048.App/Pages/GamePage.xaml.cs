using Puzzle2048.App.Services;
using Puzzle2048.Core;
using Puzzle2048.Rendering;

namespace Puzzle2048.App.Pages;

public partial class GamePage : ContentPage
{
    private const double SwipeDistance = 22;
    private static readonly Color Gold = Color.FromArgb("#FFD93D");
    private static readonly Color Orange = Color.FromArgb("#FF8F3D");
    private static readonly Color Plum = Color.FromArgb("#5B2A86");

    /// <summary>What the card on screen is for, so its buttons do the right thing.</summary>
    private enum CardKind { None, Result, ConfirmRestart }

    private readonly GameMode _mode;
    private readonly GameSession _session;
    private readonly GameScene _scene;
    private readonly ProgressStore _progress = AppServices.Progress;
    private readonly ISoundService _sound = AppServices.Sound;
    private readonly long _bestAtStart;

    private IDispatcherTimer? _timer;
    private Window? _window;
    private long _lastTick;
    private int _idleFrames;
    private bool _swipeHandled;
    private bool _runReported;
    private CardKind _card;
    private Action _primary = () => { };
    private Action? _secondary;
    private Action _tertiary = () => { };
    private long _lastAnnouncedScore = -1;

    public GamePage(GameMode mode, int size)
    {
        InitializeComponent();

        _mode = mode;
        _session = CreateSession(mode, size);
        _bestAtStart = _progress.HighScore(mode, _session.Size);

        Color background = Color.FromArgb(_progress.BackgroundColor);
        _scene = new GameScene { Session = _session, BackgroundColor = background };
        SceneView.Drawable = _scene;
        // The strip behind the status bar matches the top of the drawn background
        BackgroundColor = Painter.Lighten(background, 0.28f);

        UpdateHud();
        _scene.Reveal();
        UpdateAdVisibility();
    }

    private GameSession CreateSession(GameMode mode, int size)
    {
        if (mode.IsClassic()
            && GameSnapshot.TryParse(_progress.LoadClassicGame(size)) is { Mode: GameMode.Classic } saved
            && saved.State != RunState.Lost)
        {
            return GameSession.FromSnapshot(saved);
        }

        return new GameSession(mode, size);
    }

    // ------------------------------------------------------------------ page and app lifecycle

    protected override void OnAppearing()
    {
        base.OnAppearing();

        UpdateAdVisibility();
        AppServices.Billing.AdsRemovedChanged += UpdateAdVisibility;

        // Home button or another app on top: pause the clock and save, so Time Attack never runs in the background
        _window = Window;
        if (_window is not null)
        {
            _window.Stopped += OnAppStopped;
            _window.Resumed += OnAppResumed;
        }

        StartLoop();
        _sound.Play(Sfx.Whoosh);

        // A won Classic game that was closed before choosing comes back to the same question
        if (_session.State == RunState.Won && _card == CardKind.None)
            ShowRunResult();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        AppServices.Billing.AdsRemovedChanged -= UpdateAdVisibility;
        if (_window is not null)
        {
            _window.Stopped -= OnAppStopped;
            _window.Resumed -= OnAppResumed;
            _window = null;
        }

        StopLoop();
        SaveProgress();
    }

    private void OnAppStopped(object? sender, EventArgs e)
    {
        StopLoop();
        SaveProgress();
    }

    private void OnAppResumed(object? sender, EventArgs e) => StartLoop();

    private void StartLoop()
    {
        StopLoop();
        _lastTick = Environment.TickCount64;
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void StopLoop()
    {
        if (_timer is null)
            return;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }

    private void UpdateAdVisibility() => Ad.IsVisible = !_progress.AdsRemoved;

    // ------------------------------------------------------------------ input

    private void OnPan(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _swipeHandled = false;
                break;

            case GestureStatus.Running when !_swipeHandled && _card == CardKind.None:
                if (Math.Abs(e.TotalX) < SwipeDistance && Math.Abs(e.TotalY) < SwipeDistance)
                    break;

                _swipeHandled = true;
                Direction direction = Math.Abs(e.TotalX) > Math.Abs(e.TotalY)
                    ? (e.TotalX > 0 ? Direction.Right : Direction.Left)
                    : (e.TotalY > 0 ? Direction.Down : Direction.Up);
                ApplyMove(direction);
                break;
        }
    }

    private async void OnTapped(object? sender, TappedEventArgs e)
    {
        if (e.GetPosition(SceneView) is not { } point)
            return;

        SceneAction action = _scene.HitTest(new PointF((float)point.X, (float)point.Y));
        if (action == SceneAction.None)
            return;

        _scene.Press(action);
        _sound.Play(Sfx.Tap);
        Buzz(HapticFeedbackType.Click);
        SceneView.Invalidate();

        // Let the button visibly sink before the action happens
        await Task.Delay(90);

        switch (action)
        {
            case SceneAction.Back:
                await Navigation.PopAsync();
                break;
            case SceneAction.Restart:
                AskRestart();
                break;
            case SceneAction.Undo:
                Undo();
                break;
            case SceneAction.Trash:
                Trash();
                break;
            case SceneAction.Primary:
                _primary();
                break;
            case SceneAction.Secondary:
                _secondary?.Invoke();
                break;
            case SceneAction.Tertiary:
                _tertiary();
                break;
        }
    }

    // ------------------------------------------------------------------ playing

    private void ApplyMove(Direction direction)
    {
        MoveResult? result = _session.Move(direction);
        if (result is null)
            return;

        int levelBefore = ProgressStore.LevelFor(_progress.TotalPoints);
        _progress.AddPoints(result.PointsEarned);
        int levelAfter = ProgressStore.LevelFor(_progress.TotalPoints);

        UpdateHud();
        _scene.OnMove(result);
        PlayMoveSounds(result);

        if (levelAfter > levelBefore)
            CelebrateLevel(levelAfter);

        if (_mode.IsClassic())
            SaveClassic();
        else if (_mode == GameMode.Daily && (result.GoalJustMet || _session.Moves % 10 == 0))
            SaveDaily();

        SceneView.Invalidate();

        if (_session.State != RunState.Playing)
            _ = FinishRunAsync();
    }

    private void PlayMoveSounds(MoveResult result)
    {
        int biggest = result.Outcome.BiggestMerge;
        if (result.Outcome.MergeCount == 0)
        {
            _sound.Play(Sfx.Move);
            return;
        }

        // The pop lands when the tiles meet, a moment after the swipe
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(BoardRenderer.SlideMilliseconds), () =>
        {
            _sound.PlayMerge(biggest);
            if (result.GoalJustMet)
                _sound.Play(Sfx.Win);
            else if (biggest >= GameSession.MilestoneTile)
                _sound.Play(Sfx.Milestone);
            else if (result.ComboBonus > 0)
                _sound.Play(Sfx.Combo);
        });

        Buzz(biggest >= 512 ? HapticFeedbackType.LongPress : HapticFeedbackType.Click);
    }

    private void CelebrateLevel(int level)
    {
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), () =>
        {
            _scene.Praise("LEVEL UP!", $"LEVEL {level}", Gold, Orange, Plum);
            _scene.Celebrate(1800);
            _sound.Play(Sfx.LevelUp);
        });
    }

    private void OnTick(object? sender, EventArgs e)
    {
        long now = Environment.TickCount64;
        // A long gap means the screen was paused, so that time is not charged to the player
        var delta = TimeSpan.FromMilliseconds(Math.Min(now - _lastTick, 250));
        _lastTick = now;

        bool timeUp = _session.Tick(delta);
        if (_session.ClockRunning || timeUp)
            UpdateHud();

        // Full speed while anything moves; a calm 15 frames a second for the drifting background
        if (_scene.IsAnimating || _session.ClockRunning || ++_idleFrames >= 4)
        {
            _idleFrames = 0;
            SceneView.Invalidate();
        }

        if (timeUp)
            _ = FinishRunAsync();
    }

    private void Undo()
    {
        if (_card != CardKind.None || !_session.Undo())
            return;

        _scene.Reveal();
        _sound.Play(Sfx.Whoosh);
        SaveClassic();
        UpdateHud();
    }

    private void Trash()
    {
        if (_card != CardKind.None)
            return;

        if (_session.RemoveSmallTiles().Count == 0)
        {
            _scene.Toast(_session.TrashCharges == 0 ? "No clears left" : "Need more tiles");
        }
        else
        {
            _scene.Reveal();
            _sound.Play(Sfx.Whoosh);
            SaveClassic();
        }
        UpdateHud();
    }

    private void AskRestart()
    {
        if (_card == CardKind.Result)
        {
            StartNewGame();
            return;
        }

        ShowCard(CardKind.ConfirmRestart, new ResultCard
        {
            Title = "New game?",
            Lines = [_mode == GameMode.Daily ? "Today's puzzle starts again." : "Your current game will end."],
            Primary = "Start over",
            Tertiary = "Keep playing",
        }, primary: StartNewGame, secondary: null, tertiary: CloseCard);
    }

    private void StartNewGame()
    {
        CloseCard();
        _runReported = false;
        _session.StartFresh();
        if (_mode.IsClassic())
            _progress.ClearClassicGame(_session.Size);

        _scene.Reveal();
        _sound.Play(Sfx.Whoosh);
        UpdateHud();
    }

    // ------------------------------------------------------------------ what the screen shows

    private void UpdateHud()
    {
        HudState hud = _scene.Hud;
        hud.Score = _session.Score;
        hud.Best = Math.Max(_bestAtStart, _progress.HighScore(_mode, _session.Size));
        hud.ShowPowerUps = _mode.IsClassic();
        hud.CanUndo = _session.CanUndo;
        hud.CanTrash = _session.CanTrash;
        hud.TrashCharges = _session.TrashCharges;
        hud.StripText = null;
        hud.StripFraction = null;
        hud.StripUrgent = false;
        hud.StripDone = false;
        hud.BottomHint = null;

        switch (_mode)
        {
            case GameMode.Classic:
                hud.ModeChip = _session.Endless ? "ENDLESS" : $"CLASSIC {_session.Size}x{_session.Size}";
                break;

            case GameMode.Daily when _session.Goal is { } goal:
                hud.ModeChip = $"DAILY  {DailyChallenge.Label(_session.DayNumber).ToUpperInvariant()}";
                hud.StripDone = _session.GoalMet;
                hud.StripFraction = _session.GoalMet ? 1f : (float)goal.Fraction(_session.Board.MaxTile, _session.Score, _session.Moves);
                hud.StripText = _session.GoalMet ? "Goal complete!"
                    : goal.IsLost(_session.Moves) ? $"{goal.Title}: missed"
                    : goal.Type == GoalType.TileInMoves ? $"{goal.Title}  ({_session.Moves}/{goal.MoveLimit})"
                    : goal.Title;
                hud.BottomHint = "Same puzzle for everyone today";
                break;

            case GameMode.TimeAttack:
                hud.ModeChip = "TIME ATTACK";
                hud.StripFraction = (float)(_session.TimeLeft / GameSession.TimeAttackMax);
                hud.StripText = $"TIME  {FormatClock(_session.TimeLeft, tenths: false)}";
                hud.StripUrgent = _session.ClockRunning && _session.TimeLeft <= TimeSpan.FromSeconds(10);
                hud.BottomHint = _session.ClockStarted ? "Every merge adds time" : "Swipe to start the clock";
                break;

            default:
                hud.ModeChip = "SPRINT";
                hud.StripFraction = (float)Math.Clamp(Math.Log2(Math.Max(1, _session.Board.MaxTile)) / Math.Log2(GameSession.SprintTarget), 0, 1);
                hud.StripText = $"{FormatClock(_session.Elapsed, tenths: true)}  to 512";
                hud.BottomHint = _session.ClockStarted ? "Make 512 as fast as you can" : "Swipe to start the clock";
                break;
        }

        // Screen readers hear the score as it changes
        if (_session.Score != _lastAnnouncedScore)
        {
            _lastAnnouncedScore = _session.Score;
            SemanticProperties.SetDescription(SceneView, $"{hud.ModeChip}. Score {_session.Score}. Swipe to move the tiles.");
        }
    }

    private static string FormatClock(TimeSpan time, bool tenths)
    {
        int seconds = (int)time.TotalSeconds;
        string text = $"{seconds / 60}:{seconds % 60:00}";
        return tenths ? $"{text}.{time.Milliseconds / 100}" : text;
    }

    private static void Buzz(HapticFeedbackType type)
    {
        try
        {
            HapticFeedback.Default.Perform(type);
        }
        catch (FeatureNotSupportedException)
        {
            // No vibration motor on this device
        }
    }

    // ------------------------------------------------------------------ end of a run

    private async Task FinishRunAsync()
    {
        if (_runReported)
            return;
        _runReported = true;

        SaveProgress();
        UpdateHud();

        // Let the last move finish animating before the card covers the board
        await Task.Delay(650);
        ShowRunResult();
    }

    private void ShowRunResult()
    {
        long score = _session.Score;
        bool newBest = score > 0 && score > _bestAtStart;
        bool won = _session.State == RunState.Won;
        string scoreLine = $"Score {score:N0}";
        string bestLine = newBest ? "New best score!" : $"Best {Math.Max(_bestAtStart, score):N0}";

        ResultCard card;
        Action primary = StartNewGame;
        Action? secondary = () => _ = ShareAsync();
        switch (_mode)
        {
            case GameMode.Classic when won:
                card = new ResultCard
                {
                    Title = "You made 2048!", Celebrate = true, Lines = [scoreLine, "Keep going for 4096?"],
                    Primary = "Keep playing", Secondary = "New game", Tertiary = "Menu",
                };
                primary = KeepPlaying;
                secondary = StartNewGame;
                break;

            case GameMode.Classic:
                card = new ResultCard
                {
                    Title = newBest ? "New best!" : "Game over", Celebrate = newBest, Lines = [scoreLine, bestLine],
                    Primary = "Try again", Secondary = "Share",
                };
                break;

            case GameMode.Daily:
                int stars = _session.Goal!.Stars(_session.GoalMet, score, _session.Moves);
                _progress.RecordDailyStars(_session.DayNumber, stars);
                int streak = _progress.CurrentStreak(_session.DayNumber);
                card = new ResultCard
                {
                    Title = _session.GoalMet ? "Goal complete!" : "Daily done",
                    Stars = stars,
                    Celebrate = _session.GoalMet,
                    Lines =
                    [
                        scoreLine,
                        $"Best today {Math.Max(_progress.DailyBestScore(_session.DayNumber), (int)score):N0}",
                        streak > 0 ? $"Streak {streak} {(streak == 1 ? "day" : "days")}" : "Come back tomorrow!",
                    ],
                    Primary = "Play again", Secondary = "Share",
                };
                break;

            case GameMode.TimeAttack:
                card = new ResultCard
                {
                    Title = newBest ? "New best!" : "Time's up!", Celebrate = newBest, Lines = [scoreLine, bestLine],
                    Primary = "Play again", Secondary = "Share",
                };
                break;

            default:
                card = won
                    ? new ResultCard
                    {
                        Title = "512 reached!", Celebrate = true,
                        Lines = [$"Time {FormatClock(_session.Elapsed, tenths: true)}", BestTimeLine()],
                        Primary = "Play again", Secondary = "Share",
                    }
                    : new ResultCard
                    {
                        Title = "Board locked", Lines = [scoreLine, "Try a different order"],
                        Primary = "Try again", Secondary = "Share",
                    };
                break;
        }

        _sound.Play(card.Celebrate ? Sfx.Win : Sfx.Lose);
        ShowCard(CardKind.Result, card, primary, secondary, tertiary: () => _ = Navigation.PopAsync());
    }

    private string BestTimeLine()
    {
        long best = _progress.SprintBestMs;
        bool record = best >= 0 && (long)_session.Elapsed.TotalMilliseconds <= best;
        return record ? "New best time!" : best > 0 ? $"Best {FormatClock(TimeSpan.FromMilliseconds(best), tenths: true)}" : "";
    }

    private void KeepPlaying()
    {
        CloseCard();
        _session.ContinueEndless();
        _runReported = false;
        SaveClassic();
        UpdateHud();
    }

    private void ShowCard(CardKind kind, ResultCard card, Action primary, Action? secondary, Action tertiary)
    {
        _card = kind;
        _primary = primary;
        _secondary = secondary;
        _tertiary = tertiary;
        _scene.ShowResult(card);
        SceneView.Invalidate();
    }

    private void CloseCard()
    {
        _card = CardKind.None;
        _scene.HideResult();
        SceneView.Invalidate();
    }

    // ------------------------------------------------------------------ saving and sharing

    private void SaveClassic()
    {
        if (_session.State == RunState.Lost)
            _progress.ClearClassicGame(_session.Size);
        else
            _progress.SaveClassicGame(_session.Size, _session.ToSnapshot().ToJson());
    }

    private void SaveDaily() =>
        _progress.RecordDailyRun(_session.DayNumber, _session.Score, _session.GoalMet, _session.Moves);

    private void SaveProgress()
    {
        _progress.RecordHighScore(_mode, _session.Size, _session.Score);
        if (_mode == GameMode.Sprint && _session.State == RunState.Won)
            _progress.RecordSprintTime((long)_session.Elapsed.TotalMilliseconds);

        if (_mode.IsClassic())
            SaveClassic();
        else if (_mode == GameMode.Daily)
            SaveDaily();
    }

    private async Task ShareAsync()
    {
        string line = _mode switch
        {
            GameMode.Daily => $"2048 Daily {DailyChallenge.Label(_session.DayNumber)}: {_session.Score} points"
                + (_session.GoalMet ? ", goal complete" : "")
                + $". Streak {_progress.CurrentStreak(_session.DayNumber)}.",
            GameMode.TimeAttack => $"2048 Time Attack: {_session.Score} points.",
            GameMode.Sprint => _session.State == RunState.Won
                ? $"2048 Sprint: 512 in {FormatClock(_session.Elapsed, tenths: true)}."
                : "2048 Sprint: tough board!",
            _ => $"I scored {_session.Score} in 2048.",
        };

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "Share your score",
            Text = $"{line} Can you beat me?\n{AppServices.StoreUrl}",
        });
    }
}
