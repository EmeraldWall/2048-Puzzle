using Android.App;
using Puzzle2048.App.Services;

// AdMob reads its application id from the manifest. Debug builds get Google's test id.
[assembly: MetaData("com.google.android.gms.ads.APPLICATION_ID", Value = AdConfig.AppId)]
