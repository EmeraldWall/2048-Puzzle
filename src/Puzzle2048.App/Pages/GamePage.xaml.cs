using Puzzle2048.App.Services;
using Puzzle2048.Core;
using Puzzle2048.Rendering;

namespace Puzzle2048.App.Pages;

public partial class GamePage : ContentPage
{
    private const double SwipeDistance = 24;

    private readonly GameMode _mode;
    private readonly GameSession _session;
    private readonly BoardRenderer _renderer;
    private readonly ProgressStore _progress = AppServices.Progress;

    private IDispatcherTimer? _timer;
    private long _lastTick;
    private bool _swipeHandled;
    private bool _resultShown;
    private Action _primaryAction = () => { };
    private Action _secondaryAction = () => { };
    private string _lastScoreText = "";
    private string _lastBestText = "";
    private string _lastModeText = "";
    private string _lastGoalText = "";

    public GamePage(GameMode mode, int size)
    {
        InitializeComponent();

        _mode = mode;
        _session = CreateSession(mode, size);
        _renderer = new BoardRenderer { Session = _session };
        BoardView.Drawable = _renderer;
        _renderer.BeginReveal();

        Root.BackgroundColor = Color.FromArgb(_progress.BackgroundColor);
        Outer.BackgroundColor = Root.BackgroundColor;
        Root.SizeChanged += (_, _) => BoardView.HeightRequest = Math.Max(0, Root.Width - Root.Padding.HorizontalThickness);

        // Undo and the tile-removal power-up belong to Classic only, so the other modes stay comparable
        UndoButton.IsVisible = TrashButton.IsVisible = mode.IsClassic();

        UpdateHud();
        UpdateAdVisibility();
    }

    private GameSession CreateSession(GameMode mode, int size)
    {
        if (mode.IsClassic()
            && GameSnapshot.TryParse(_progress.LoadClassicGame(size)) is { Mode: GameMode.Classic, State: RunState.Playing } saved)
        {
            return GameSession.FromSnapshot(saved);
        }

        return new GameSession(mode, size);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        UpdateAdVisibility();
        AppServices.Billing.AdsRemovedChanged += UpdateAdVisibility;

        _lastTick = Environment.TickCount64;
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(33);
        _timer.Tick += OnTick;
        _timer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        AppServices.Billing.AdsRemovedChanged -= UpdateAdVisibility;
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
        }

        SaveProgress();
    }

    private void UpdateAdVisibility() => Ad.IsVisible = !_progress.AdsRemoved;

    // ------------------------------------------------------------------ input and game loop

    private void OnPan(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _swipeHandled = false;
                break;

            case GestureStatus.Running when !_swipeHandled && !_resultShown:
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

    private void ApplyMove(Direction direction)
    {
        MoveResult? result = _session.Move(direction);
        if (result is null)
            return;

        _renderer.BeginMove(result);
        _progress.AddPoints(result.PointsEarned);

        if (_mode.IsClassic())
            SaveClassic();
        else if (_mode == GameMode.Daily && (result.GoalJustMet || _session.Moves % 10 == 0))
            SaveDaily();

        if (result.Outcome.MergeCount > 0)
            Buzz(result.Outcome.BiggestMerge >= 512 ? HapticFeedbackType.LongPress : HapticFeedbackType.Click);

        UpdateHud();
        BoardView.Invalidate();

        if (_session.State != RunState.Playing)
            _ = ShowResultAsync();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        long now = Environment.TickCount64;
        // A long gap means the screen was paused, so that time is not charged to the player
        var delta = TimeSpan.FromMilliseconds(Math.Min(now - _lastTick, 250));
        _lastTick = now;

        bool timeUp = _session.Tick(delta);
        UpdateHud();

        if (_renderer.IsAnimating)
            BoardView.Invalidate();

        if (timeUp)
        {
            BoardView.Invalidate();
            _ = ShowResultAsync();
        }
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

    // ------------------------------------------------------------------ labels

    private void UpdateHud()
    {
        SetText(ScoreLabel, _session.Score.ToString("N0"), ref _lastScoreText);

        long best = Math.Max(_progress.HighScore(_mode, _session.Size), _session.Score);
        SetText(BestLabel, best.ToString("N0"), ref _lastBestText);

        SetText(ModeLabel, ModeText(), ref _lastModeText);
        ModeLabel.TextColor = _mode == GameMode.TimeAttack && _session.ClockRunning && _session.TimeLeft <= TimeSpan.FromSeconds(10)
            ? Color.FromArgb("#D32F2F")
            : Color.FromArgb("#2B2D6E");

        SetText(GoalLabel, GoalText(), ref _lastGoalText);
        GoalLabel.TextColor = _session.GoalMet ? Color.FromArgb("#1B7F3B") : Color.FromArgb("#2B2D6E");

        UndoButton.Opacity = _session.CanUndo ? 1 : 0.45;
        TrashButton.Opacity = _session.CanTrash ? 1 : 0.45;
    }

    private static void SetText(Label label, string text, ref string last)
    {
        if (text == last)
            return;
        last = text;
        label.Text = text;
    }

    private string ModeText() => _mode switch
    {
        GameMode.Classic => _session.Endless ? "ENDLESS" : $"CLASSIC {_session.Size}x{_session.Size}",
        GameMode.Daily => $"DAILY {DailyChallenge.Label(_session.DayNumber).ToUpperInvariant()}",
        GameMode.TimeAttack => $"TIME {FormatClock(_session.TimeLeft, tenths: false)}",
        _ => $"SPRINT {FormatClock(_session.Elapsed, tenths: true)}",
    };

    private string GoalText()
    {
        switch (_mode)
        {
            case GameMode.Daily when _session.Goal is { } goal:
                string state = _session.GoalMet ? "done"
                    : goal.IsLost(_session.Moves) ? "missed"
                    : goal.Progress(_session.Board.MaxTile, _session.Score, _session.Moves);
                return $"Goal: {goal.Title} ({state})";
            case GameMode.TimeAttack:
                return "Every merge adds time";
            case GameMode.Sprint:
                return $"Reach {GameSession.SprintTarget} as fast as you can";
            default:
                return "";
        }
    }

    private static string FormatClock(TimeSpan time, bool tenths)
    {
        int seconds = (int)time.TotalSeconds;
        string text = $"{seconds / 60}:{seconds % 60:00}";
        return tenths ? $"{text}.{time.Milliseconds / 100}" : text;
    }

    // ------------------------------------------------------------------ buttons

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    private void OnUndoClicked(object? sender, EventArgs e)
    {
        if (_resultShown || !_session.Undo())
            return;

        _renderer.BeginReveal();
        SaveClassic();
        UpdateHud();
        BoardView.Invalidate();
    }

    private void OnTrashClicked(object? sender, EventArgs e)
    {
        if (_resultShown)
            return;

        if (_session.RemoveSmallTiles().Count == 0)
        {
            _renderer.ShowBanner(_session.TrashCharges == 0 ? "NO CHARGES LEFT" : "NEED MORE TILES");
        }
        else
        {
            _renderer.BeginReveal();
            SaveClassic();
        }

        UpdateHud();
        BoardView.Invalidate();
    }

    private async void OnNewGameClicked(object? sender, EventArgs e)
    {
        bool sure = await DisplayAlertAsync("New game", "Start over? Your current game will be lost.", "Start over", "Keep playing");
        if (sure)
            StartNewGame();
    }

    private void StartNewGame()
    {
        _resultShown = false;
        ResultLayer.IsVisible = false;
        _session.StartFresh();
        _renderer.BeginReveal();

        if (_mode.IsClassic())
            _progress.ClearClassicGame(_session.Size);

        UpdateHud();
        BoardView.Invalidate();
    }

    private async void OnMenuClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    private void OnPrimaryClicked(object? sender, EventArgs e) => _primaryAction();

    private void OnSecondaryClicked(object? sender, EventArgs e) => _secondaryAction();

    // ------------------------------------------------------------------ saving and results

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
        if (_mode.IsClassic())
            SaveClassic();
        else if (_mode == GameMode.Daily)
            SaveDaily();
    }

    private async Task ShowResultAsync()
    {
        if (_resultShown)
            return;
        _resultShown = true;

        bool newBest = _progress.RecordHighScore(_mode, _session.Size, _session.Score);
        bool newBestTime = _mode == GameMode.Sprint && _session.State == RunState.Won
            && _progress.RecordSprintTime((long)_session.Elapsed.TotalMilliseconds);
        if (_mode == GameMode.Daily)
            SaveDaily();
        if (_mode.IsClassic())
            SaveClassic();

        UpdateHud();
        BoardView.Invalidate();
        Buzz(HapticFeedbackType.LongPress);

        // Let the last animation finish before the card covers the board
        await Task.Delay(550);
        if (!_resultShown)
            return;

        BuildResultCard(newBest, newBestTime);
        ResultLayer.IsVisible = true;
        ResultLayer.Opacity = 0;
        await ResultLayer.FadeToAsync(1, 180);
    }

    private void BuildResultCard(bool newBest, bool newBestTime)
    {
        long score = _session.Score;
        long best = Math.Max(_progress.HighScore(_mode, _session.Size), score);
        string scoreLine = $"Score: {score:N0}";
        string bestLine = newBest ? "New best score!" : $"Best: {best:N0}";

        PrimaryButton.Text = "Play again";
        SecondaryButton.Text = "Share";
        _primaryAction = StartNewGame;
        _secondaryAction = () => _ = ShareAsync();
        SecondaryButton.IsVisible = true;

        switch (_mode)
        {
            case GameMode.Classic when _session.State == RunState.Won:
                ResultTitle.Text = "You win!";
                ResultBody.Text = $"You made the 2048 tile.\n{scoreLine}";
                PrimaryButton.Text = "Keep playing";
                SecondaryButton.Text = "New game";
                _primaryAction = () =>
                {
                    _session.ContinueEndless();
                    _resultShown = false;
                    ResultLayer.IsVisible = false;
                    SaveClassic();
                    UpdateHud();
                    BoardView.Invalidate();
                };
                _secondaryAction = StartNewGame;
                break;

            case GameMode.Classic:
                ResultTitle.Text = "Game over";
                ResultBody.Text = $"{scoreLine}\n{bestLine}";
                PrimaryButton.Text = "Try again";
                SecondaryButton.Text = "Share";
                break;

            case GameMode.Daily:
                int best2 = (int)Math.Max(_progress.DailyBestScore(_session.DayNumber), score);
                int streak = _progress.CurrentStreak(_session.DayNumber);
                ResultTitle.Text = _session.GoalMet ? "Goal complete!" : $"Daily {DailyChallenge.Label(_session.DayNumber)}";
                ResultBody.Text = $"{scoreLine}\nBest today: {best2:N0}\n"
                    + (_session.GoalMet ? "Goal: complete" : "Goal: not reached")
                    + $"\nStreak: {streak} {(streak == 1 ? "day" : "days")}\n\nSame puzzle for everyone today. Retry to beat your score, and come back tomorrow for a new one.";
                break;

            case GameMode.TimeAttack:
                ResultTitle.Text = newBest ? "New best!" : "Time's up!";
                ResultBody.Text = $"{scoreLine}\n{bestLine}";
                break;

            default:
                if (_session.State == RunState.Won)
                {
                    ResultTitle.Text = newBestTime ? "New best time!" : "512 reached!";
                    ResultBody.Text = $"Time: {FormatClock(_session.Elapsed, tenths: true)}\n{scoreLine}";
                }
                else
                {
                    ResultTitle.Text = "Board locked";
                    ResultBody.Text = $"{scoreLine}\nTry a different order next time.";
                }
                break;
        }
    }

    private async Task ShareAsync()
    {
        string line = _mode switch
        {
            GameMode.Daily => $"2048 Daily {DailyChallenge.Label(_session.DayNumber)}: {_session.Score} points"
                + (_session.GoalMet ? ", goal complete" : "")
                + $". Streak {_progress.CurrentStreak(_session.DayNumber)}.",
            GameMode.TimeAttack => $"2048 Time Attack: {_session.Score} points in one minute and change.",
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
