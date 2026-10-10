using Android.Content;
using Google.Android.Gms.Ads;

// The AdMob binding marks these long-standing calls as deprecated, but they are still the documented way
// to configure child-directed ads and adaptive banners.
#pragma warning disable CS0618

namespace Puzzle2048.App;

/// <summary>
/// Starts the AdMob SDK. The game is made for children, so every ad request is tagged child-directed and
/// capped at the G rating (Google Play Families policy). That makes ads non-personalized and means no consent form is needed.
/// </summary>
internal static class AdsInitializer
{
    private static bool _started;

    public static void Start(Context context)
    {
        if (_started)
            return;
        _started = true;

        // Must be set before the SDK starts and before any ad is requested
        MobileAds.RequestConfiguration = new RequestConfiguration.Builder()
            .SetTagForChildDirectedTreatment(RequestConfiguration.TagForChildDirectedTreatmentTrue)
            .SetTagForUnderAgeOfConsent(RequestConfiguration.TagForUnderAgeOfConsentTrue)
            .SetMaxAdContentRating(RequestConfiguration.MaxAdContentRatingG)
            .Build();

        MobileAds.Initialize(context);
    }
}
