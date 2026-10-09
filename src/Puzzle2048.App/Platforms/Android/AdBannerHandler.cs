using Google.Android.Gms.Ads;
using Microsoft.Maui.Handlers;
using Puzzle2048.App.Controls;
using Puzzle2048.App.Services;

// The AdMob binding marks these long-standing calls as deprecated, but they are still the documented way
// to configure child-directed ads and adaptive banners.
#pragma warning disable CS0618

namespace Puzzle2048.App.Platforms.Android;

/// <summary>Shows a Google AdMob adaptive banner in place of the empty <see cref="AdBanner"/> view.</summary>
public class AdBannerHandler : ViewHandler<AdBanner, AdView>
{
    public static readonly IPropertyMapper<AdBanner, AdBannerHandler> Mapper =
        new PropertyMapper<AdBanner, AdBannerHandler>(ViewHandler.ViewMapper);

    public AdBannerHandler() : base(Mapper)
    {
    }

    protected override AdView CreatePlatformView()
    {
        AdsInitializer.Start(Context);

        var view = new AdView(Context)
        {
            AdUnitId = AdConfig.BannerUnitId,
        };

        var metrics = Context.Resources!.DisplayMetrics!;
        int widthDp = (int)(metrics.WidthPixels / metrics.Density);
        view.AdSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSize(Context, widthDp);
        return view;
    }

    protected override void ConnectHandler(AdView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.LoadAd(new AdRequest.Builder().Build());
    }

    protected override void DisconnectHandler(AdView platformView)
    {
        platformView.Destroy();
        base.DisconnectHandler(platformView);
    }
}
