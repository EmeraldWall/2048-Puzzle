using Puzzle2048.App.Services;
using Puzzle2048.Core;
using Puzzle2048.Rendering;

namespace Puzzle2048.App.Pages;

public partial class MenuPage : ContentPage
{
    private readonly ProgressStore _progress = AppServices.Progress;
    private bool _servicesStarted;
    private double _levelFraction;

    private readonly Backdrop _backdrop = new() { BaseColor = Color.FromArgb("#7FD3FF") };
    private IDispatcherTimer? _timer;

    public MenuPage()
    {
        InitializeComponent();
        LevelTrack.SizeChanged += (_, _) => ApplyLevelFill();
        BackdropView.Drawable = _backdrop;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        AppServices.Billing.AdsRemovedChanged += RefreshStore;
        Refresh();

        // The backdrop drifts gently at 20 frames a second while the menu is visible
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(50);
        _timer.Tick += (_, _) => BackdropView.Invalidate();
        _timer.Start();

        if (!_servicesStarted)
        {
            _servicesStarted = true;
            AppServices.Reminders.RestoreIfEnabled();
            _ = AppServices.Sound.PreloadAsync();
            await AppServices.Billing.InitializeAsync();
        }

        await OfferReminderOnceAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        AppServices.Billing.AdsRemovedChanged -= RefreshStore;
        _timer?.Stop();
        _timer = null;
    }

    // ------------------------------------------------------------------ what the screen shows

    private void Refresh()
    {
        int today = DailyChallenge.Today();

        // Level
        long points = _progress.TotalPoints;
        int level = ProgressStore.LevelFor(points);
        long start = ProgressStore.LevelStart(level);
        long next = ProgressStore.LevelStart(level + 1);
        LevelLabel.Text = $"Level {level}";
        LevelSubLabel.Text = $"{next - points:N0} points to level {level + 1}";
        _levelFraction = (double)(points - start) / (next - start);
        ApplyLevelFill();

        // Daily card
        DailyGoal goal = DailyChallenge.GoalFor(today);
        int streak = _progress.CurrentStreak(today);
        DailyTitle.Text = $"Daily Challenge  {DailyChallenge.Label(today)}";
        DailyGoalLabel.Text = $"{goal.Title} on {goal.Size}x{goal.Size}";
        DailyStatus.Text = _progress.HasPlayedDaily(today)
            ? (_progress.IsGoalMet(today) ? "Goal complete. " : "Goal not reached yet. ") + $"Best today: {_progress.DailyBestScore(today):N0}"
            : (streak > 0 ? "Play today to keep your streak" : "Play today to start a streak");
        DailyStreak.Text = streak > 0 ? $"Streak: {streak} {(streak == 1 ? "day" : "days")}" : "No streak yet";
        int stars = _progress.DailyStars(today);
        DailyStars.Text = new string('\u2605', stars) + new string('\u2606', 3 - stars);

        // Records
        long taBest = _progress.HighScore(GameMode.TimeAttack, 4);
        TimeAttackBest.Text = $"Best: {(taBest > 0 ? taBest.ToString("N0") : "none")}";
        long sprintBest = _progress.SprintBestMs;
        SprintBest.Text = $"Best: {(sprintBest > 0 ? FormatTime(sprintBest) : "none")}";

        RefreshStore();
    }

    private void ApplyLevelFill()
    {
        double width = LevelTrack.Width;
        if (width > 0)
            LevelFill.WidthRequest = Math.Max(14, width * Math.Clamp(_levelFraction, 0, 1));
    }

    private void RefreshStore()
    {
        bool removed = _progress.AdsRemoved;
        Ad.IsVisible = !removed;
        RemoveAdsButton.IsVisible = !removed;
        string? price = AppServices.Billing.Price;
        RemoveAdsButton.Text = price is null ? "Remove ads" : $"Remove ads ({price})";
    }

    private static string FormatTime(long milliseconds)
    {
        long seconds = milliseconds / 1000;
        return $"{seconds / 60}:{seconds % 60:00}.{milliseconds % 1000 / 100}";
    }

    // ------------------------------------------------------------------ buttons

    private Task Play(GameMode mode, int size) => Navigation.PushAsync(new GamePage(mode, size));

    private async void OnDailyClicked(object? sender, EventArgs e) =>
        await Play(GameMode.Daily, DailyChallenge.GoalFor(DailyChallenge.Today()).Size);

    private async void OnTimeAttackClicked(object? sender, EventArgs e) => await Play(GameMode.TimeAttack, 4);

    private async void OnSprintClicked(object? sender, EventArgs e) => await Play(GameMode.Sprint, 4);

    private async void OnClassic4Clicked(object? sender, EventArgs e) => await Play(GameMode.Classic, 4);

    private async void OnClassic5Clicked(object? sender, EventArgs e) => await Play(GameMode.Classic, 5);

    private async void OnClassic6Clicked(object? sender, EventArgs e) => await Play(GameMode.Classic, 6);

    private async void OnRemoveAdsClicked(object? sender, EventArgs e)
    {
        if (await AppServices.Billing.BuyAsync())
            await DisplayAlertAsync("Thank you!", "Ads have been removed.", "OK");
        else if (AppServices.Billing.PurchasePending)
            await DisplayAlertAsync("Almost there", "Your payment is pending. Ads will be removed as soon as Google Play confirms it.", "OK");
    }

    private async void OnSettingsClicked(object? sender, EventArgs e) => await Navigation.PushAsync(new SettingsPage());

    private async void OnContactClicked(object? sender, EventArgs e)
    {
        try
        {
            await Launcher.Default.OpenAsync($"mailto:{AppConstants.SupportEmail}?subject={Uri.EscapeDataString("2048 support")}");
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or InvalidOperationException)
        {
            await DisplayAlertAsync("Contact us", $"Please write to {AppConstants.SupportEmail}", "OK");
        }
    }

    private async void OnRateClicked(object? sender, EventArgs e)
    {
        try
        {
            await Launcher.Default.OpenAsync($"market://details?id={AppInfo.Current.PackageName}");
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or InvalidOperationException)
        {
            await Launcher.Default.OpenAsync(AppServices.StoreUrl);
        }
    }

    private async void OnShareClicked(object? sender, EventArgs e) =>
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "Share 2048 with friends",
            Text = $"Try this 2048 puzzle game!\n{AppServices.StoreUrl}",
        });

    // Asked once, right after the player has tried their first daily challenge
    private async Task OfferReminderOnceAsync()
    {
        if (_progress.ReminderPrompted || !_progress.EverPlayedDaily || _progress.ReminderEnabled)
            return;

        _progress.ReminderPrompted = true;
        bool yes = await DisplayAlertAsync("Never miss a day",
            "Get one reminder each evening, only when you have not played today's challenge yet. You can turn it off any time in Settings.",
            "Remind me", "No thanks");
        if (yes)
            await AppServices.Reminders.EnableAsync();
    }
}
