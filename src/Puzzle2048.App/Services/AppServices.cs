using Puzzle2048.Core;

namespace Puzzle2048.App.Services;

/// <summary>The few app-wide services, created once.</summary>
public static class AppServices
{
    public static ProgressStore Progress { get; } = new(new PreferencesStorage());

    public static BillingService Billing { get; } = new(Progress);

    /// <summary>Replaced with the Android implementation at startup.</summary>
    public static IReminderService Reminders { get; set; } = new NoReminderService();

    public static string StoreUrl => string.Format(AppConstants.StoreUrlFormat, AppInfo.Current.PackageName);
}
