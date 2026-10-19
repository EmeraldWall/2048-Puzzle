using Microsoft.Maui.Controls.Shapes;
using Puzzle2048.App.Controls;
using Puzzle2048.App.Services;
using Puzzle2048.Core;

namespace Puzzle2048.App.Pages;

public partial class SettingsPage : ContentPage
{
    private static readonly string[] Backgrounds =
    [
        "#7FD3FF", // sky (default)
        "#B9F6CA", // mint
        "#FFE6A7", // sunshine
        "#FFC1DE", // bubblegum
        "#D7C2FF", // lavender
        "#FFD3A8", // peach
        "#C4F0F5", // aqua
        "#F5F1FF", // cloud
    ];

    private readonly ProgressStore _progress = AppServices.Progress;
    private bool _loading = true;

    public SettingsPage()
    {
        InitializeComponent();

        BuildSwatches();
        ReminderSwitch.IsToggled = _progress.ReminderEnabled;
        SoundSwitch.IsToggled = _progress.SoundEnabled;
        VersionLabel.Text = $"Version {AppInfo.Current.VersionString}";
        RefreshStore();
        _loading = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        AppServices.Billing.AdsRemovedChanged += RefreshStore;
        AppServices.Account.Changed += RefreshAccount;
        RefreshStore();
        RefreshAccount();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        AppServices.Billing.AdsRemovedChanged -= RefreshStore;
        AppServices.Account.Changed -= RefreshAccount;
    }

    private void BuildSwatches()
    {
        Swatches.Children.Clear();
        Swatches.RowDefinitions.Clear();
        for (int i = 0; i < Backgrounds.Length; i++)
        {
            string hex = Backgrounds[i];
            if (i % 4 == 0)
                Swatches.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            bool selected = string.Equals(hex, _progress.BackgroundColor, StringComparison.OrdinalIgnoreCase);
            var swatch = new Border
            {
                WidthRequest = 54,
                HeightRequest = 54,
                HorizontalOptions = LayoutOptions.Center,
                BackgroundColor = Color.FromArgb(hex),
                Stroke = selected ? Colors.White : Color.FromArgb("#55FFFFFF"),
                StrokeThickness = selected ? 5 : 2,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(27) },
                Scale = selected ? 1.08 : 1,
            };
            SemanticProperties.SetDescription(swatch, selected ? $"Background {i + 1}, selected" : $"Background {i + 1}");

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                AppServices.Sound.Play(Sfx.Tap);
                _progress.BackgroundColor = hex;
                BuildSwatches();
            };
            swatch.GestureRecognizers.Add(tap);
            Swatches.Add(swatch, i % 4, i / 4);
        }
    }

    private void RefreshStore()
    {
        bool removed = _progress.AdsRemoved;
        RemoveAdsButton.IsVisible = !removed;
        string? price = AppServices.Billing.Price;
        RemoveAdsButton.Text = price is null ? "Remove ads" : $"Remove ads ({price})";
    }

    private void RefreshAccount()
    {
        IGameAccountService account = AppServices.Account;
        bool signedIn = account.IsAvailable && account.IsSignedIn;
        AccountSignInButton.IsVisible = account.IsAvailable && !signedIn;
        SyncButton.IsVisible = signedIn;
        SyncButton.Text = account.IsSyncing ? "Saving..." : "Save progress now";
        AccountStatus.Text = !account.IsAvailable
            ? "Your progress is saved on this phone."
            : signedIn
                ? $"Signed in with Google Play Games as {account.PlayerName}. Your progress is saved to the cloud."
                : "Sign in with Google Play Games to keep your progress on a new phone and join the leaderboards.";
    }

    private async void OnSignInClicked(object? sender, EventArgs e)
    {
        if (!await AppServices.Account.SignInAsync())
            await Dialog.ShowAsync("Not signed in", "Google Play Games could not sign you in. You can keep playing and try again later.",
                "OK", tone: DialogTone.Blue);
    }

    private async void OnSyncClicked(object? sender, EventArgs e) => await AppServices.Account.SyncAsync();

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        IGameAccountService account = AppServices.Account;
        bool cloud = account.IsAvailable && account.IsSignedIn;
        string where = cloud ? "from this phone and from your Google Play Games cloud save" : "from this phone";

        bool yes = await Dialog.ShowAsync("Delete your data?",
            $"This removes your level, streaks, records, stars and saved games {where}. It cannot be undone.",
            "Delete", "Keep my data", danger: true);
        if (!yes)
            return;

        // Delete the cloud copy first. If that fails, the phone keeps its copy so the two never disagree.
        if (!await account.DeleteCloudDataAsync())
        {
            await Dialog.ShowAsync("Could not delete", "Google Play Games could not be reached. Check your internet connection and try again.",
                "OK", tone: DialogTone.Blue);
            return;
        }

        _progress.ResetProgress();
        string profile = cloud
            ? " Leaderboard scores and achievements belong to your Google Play Games profile, which you can manage in the Google Play Games settings."
            : "";
        await Dialog.ShowAsync("Data deleted", $"Your progress has been deleted. You are starting fresh!{profile}",
            "OK", tone: DialogTone.Blue);
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    protected override bool OnBackButtonPressed() => Dialog.HandleBack() || base.OnBackButtonPressed();

    private void OnSoundToggled(object? sender, ToggledEventArgs e)
    {
        if (_loading)
            return;
        _progress.SoundEnabled = e.Value;
        AppServices.Sound.Play(Sfx.Tap);
    }

    private async void OnReminderToggled(object? sender, ToggledEventArgs e)
    {
        if (_loading)
            return;

        if (e.Value)
        {
            if (!await AppServices.Reminders.EnableAsync())
            {
                _loading = true;
                ReminderSwitch.IsToggled = false;
                _loading = false;
                await Dialog.ShowAsync("Notifications are off",
                    "Allow notifications for this app in your phone settings to get the daily reminder.", "Got it",
                    tone: DialogTone.Blue);
            }
        }
        else
        {
            AppServices.Reminders.Disable();
        }
    }

    private async void OnRemoveAdsClicked(object? sender, EventArgs e)
    {
        if (await AppServices.Billing.BuyAsync())
            await Dialog.ShowAsync("Thank you!", "Ads have been removed. Enjoy the game!", "Yay!", tone: DialogTone.Gold);
        else if (AppServices.Billing.PurchasePending)
            await Dialog.ShowAsync("Almost there", "Your payment is pending. Ads will be removed as soon as Google Play confirms it.",
                "OK", tone: DialogTone.Blue);
    }

    private async void OnRestoreClicked(object? sender, EventArgs e)
    {
        bool owned = await AppServices.Billing.RestoreAsync();
        if (owned)
            await Dialog.ShowAsync("Welcome back!", "Your purchase was restored. Ads are off.", "Yay!", tone: DialogTone.Gold);
        else
            await Dialog.ShowAsync("Restore purchase", "No earlier purchase was found for this Google Play account.",
                "OK", tone: DialogTone.Blue);
    }

    private async void OnAboutClicked(object? sender, EventArgs e)
    {
        string text = $"2048 Puzzle {AppInfo.Current.VersionString}\n\n"
            + "Ads by Google AdMob. Purchases by Google Play Billing.\n\n"
            + "The Fredoka Bold font is used under the SIL Open Font License 1.1:\n\n"
            + Reflow(await ReadAssetAsync("Fredoka-OFL.txt"));
        await Dialog.ShowAsync("About", text, "Close", tone: DialogTone.Purple, longText: true);
    }

    // Licence files are hard-wrapped for a wide screen. Join each paragraph into one line so it wraps to the
    // phone, keep short all-caps headings on their own line and drop the dashed rules.
    private static string Reflow(string text)
    {
        var paragraphs = new List<string>();
        foreach (string block in text.Replace("\r", "").Split("\n\n"))
        {
            var lines = new List<string>();
            string current = "";
            foreach (string raw in block.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.All(ch => ch == '-'))
                    continue;
                bool heading = lines.Count == 0 && current.Length == 0 && line.Length <= 40
                    && line.Any(char.IsLetter) && line.Where(char.IsLetter).All(char.IsUpper);
                if (heading)
                {
                    if (current.Length > 0)
                        lines.Add(current);
                    lines.Add(line);
                    current = "";
                }
                else
                {
                    current = current.Length == 0 ? line : current + " " + line;
                }
            }
            if (current.Length > 0)
                lines.Add(current);
            if (lines.Count > 0)
                paragraphs.Add(string.Join("\n", lines));
        }
        return string.Join("\n\n", paragraphs);
    }

    private static async Task<string> ReadAssetAsync(string name)
    {
        try
        {
            using Stream stream = await FileSystem.Current.OpenAppPackageFileAsync(name);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (FileNotFoundException)
        {
            return "";
        }
    }
}
