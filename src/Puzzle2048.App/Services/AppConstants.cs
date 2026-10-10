namespace Puzzle2048.App.Services;

/// <summary>Values to review before publishing.</summary>
public static class AppConstants
{
    /// <summary>Product id of the one-time "Remove ads" purchase. Create it with this id in Play Console.</summary>
    public const string RemoveAdsProductId = "remove_ads";

    // TODO: replace with your own support address before release
    public const string SupportEmail = "support@example.com";

    public const string StoreUrlFormat = "https://play.google.com/store/apps/details?id={0}";
}

/// <summary>AdMob identifiers. Debug builds always use Google's test ads, so tapping them is safe.</summary>
public static class AdConfig
{
#if DEBUG
    public const string AppId = "ca-app-pub-3940256099942544~3347511713";
    public const string BannerUnitId = "ca-app-pub-3940256099942544/9214589741";
#else
    // Live ids from the AdMob account. They are public by design and ship inside every app.
    public const string AppId = "ca-app-pub-3331893734282241~7035696459";
    public const string BannerUnitId = "ca-app-pub-3331893734282241/7164433824";
#endif
}
