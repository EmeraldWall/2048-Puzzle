using Microsoft.Maui.Controls.Shapes;
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
        VersionLabel.Text = $"Version {AppInfo.Current.VersionString}";
        RefreshStore();
        _loading = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        AppServices.Billing.AdsRemovedChanged += RefreshStore;
        RefreshStore();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        AppServices.Billing.AdsRemovedChanged -= RefreshStore;
    }

    private void BuildSwatches()
    {
        Swatches.Children.Clear();
        foreach (string hex in Backgrounds)
        {
            bool selected = string.Equals(hex, _progress.BackgroundColor, StringComparison.OrdinalIgnoreCase);
            var swatch = new Border
            {
                WidthRequest = 52,
                HeightRequest = 52,
                Margin = new Thickness(0, 0, 10, 10),
                BackgroundColor = Color.FromArgb(hex),
                Stroke = selected ? Colors.White : Color.FromArgb("#55FFFFFF"),
                StrokeThickness = selected ? 5 : 2,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(26) },
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                _progress.BackgroundColor = hex;
                BuildSwatches();
            };
            swatch.GestureRecognizers.Add(tap);
            Swatches.Children.Add(swatch);
        }
    }

    private void RefreshStore()
    {
        bool removed = _progress.AdsRemoved;
        RemoveAdsButton.IsVisible = !removed;
        string? price = AppServices.Billing.Price;
        RemoveAdsButton.Text = price is null ? "Remove ads" : $"Remove ads ({price})";
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

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
                await DisplayAlertAsync("Notifications are off",
                    "Allow notifications for this app in your phone settings to get the daily reminder.", "OK");
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
            await DisplayAlertAsync("Thank you!", "Ads have been removed.", "OK");
    }

    private async void OnRestoreClicked(object? sender, EventArgs e)
    {
        bool owned = await AppServices.Billing.RestoreAsync();
        await DisplayAlertAsync("Restore purchase",
            owned ? "Your purchase was restored. Ads are off." : "No earlier purchase was found for this Google Play account.",
            "OK");
    }

    private async void OnAboutClicked(object? sender, EventArgs e)
    {
        string text = $"2048 Puzzle {AppInfo.Current.VersionString}\n\n"
            + "Ads by Google AdMob. Purchases by Google Play Billing.\n\n"
            + "The Fredoka Bold font is used under the SIL Open Font License 1.1:\n\n"
            + await ReadAssetAsync("Fredoka-OFL.txt");
        await DisplayAlertAsync("About and licenses", text, "OK");
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
