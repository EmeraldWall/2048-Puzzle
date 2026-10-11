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
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
