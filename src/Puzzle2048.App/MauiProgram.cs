using Microsoft.Extensions.Logging;

namespace Puzzle2048.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("Fredoka-Bold.ttf", "FredokaBold"))
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler<Controls.AdBanner, Platforms.Android.AdBannerHandler>();
#endif
            });

#if ANDROID
        Services.AppServices.Reminders = new Platforms.Android.AndroidReminderService();
        Services.AppServices.Sound = new Platforms.Android.AndroidSoundService();
        Services.AppServices.Account = new Platforms.Android.AndroidPlayGamesService(Services.AppServices.Progress);
        Services.AppServices.Frames = new Platforms.Android.ChoreographerTicker();
        Platforms.Android.AndroidSprite.Install(global::Android.App.Application.Context.Resources?.DisplayMetrics?.Density ?? 1f);
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
