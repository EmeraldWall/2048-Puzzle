using System.Text;
using Android.App;
using Android.Content;
using Android.Gms.Extensions;
using Android.Gms.Games;
using Android.Gms.Games.Snapshot;
using Android.Runtime;
using Puzzle2048.App.Services;
using Puzzle2048.Core;
using Application = Android.App.Application;
using GameMode = Puzzle2048.Core.GameMode;
using GmsTask = Android.Gms.Tasks.Task;

namespace Puzzle2048.App.Platforms.Android;

/// <summary>
/// Google Play Games Services v2: automatic sign-in, a Saved Games copy of the player's progress, leaderboards
/// and achievements. The ids come from Resources/values/playgames.xml. With no project id there, nothing here runs.
///
/// The .NET binding leaves out the players and saved games clients, so those few calls go through JNI.
/// Every call is guarded: a Play Games problem never breaks the game, it only leaves the player signed out.
/// </summary>
public sealed class AndroidPlayGamesService : IGameAccountService
{
    private const string SnapshotName = "progress";
    private const int ResolutionHighestProgress = 4; // SnapshotsClient.RESOLUTION_POLICY_HIGHEST_PROGRESS
    private const int UiRequestCode = 9010;

    private static readonly int[] TileAchievements = [256, 512, 1024, 2048, 4096];
    private static readonly int[] StreakAchievements = [3, 7, 30];
    private static readonly int[] LevelAchievements = [10, 25];

    private readonly ProgressStore _progress;
    private readonly HashSet<string> _unlockedThisRun = [];
    private bool _initialized;

    public AndroidPlayGamesService(ProgressStore progress)
    {
        _progress = progress;
        IsAvailable = Id("game_services_project_id").Length > 0;
    }

    public bool IsAvailable { get; }

    public bool IsSignedIn { get; private set; }

    public string? PlayerName { get; private set; }

    public bool IsSyncing { get; private set; }

    public event Action? Changed;

    /// <summary>Called from MainApplication before anything else touches Play Games.</summary>
    public static void InitializeSdk(Context context)
    {
        if (Id(context, "game_services_project_id").Length > 0)
            GuardSync(() => PlayGamesSdk.Initialize(context));
    }

    public async Task InitializeAsync()
    {
        if (!IsAvailable || _initialized || Platform.CurrentActivity is not Activity activity)
            return;
        _initialized = true;

        await Guard(async () =>
        {
            var result = await PlayGames.GetGamesSignInClient(activity).IsAuthenticated().AsAsync<AuthenticationResult>();
            await OnSignInResultAsync(activity, result.IsAuthenticated);
        });
    }

    public async Task<bool> SignInAsync()
    {
        if (!IsAvailable || Platform.CurrentActivity is not Activity activity)
            return false;

        await Guard(async () =>
        {
            var result = await PlayGames.GetGamesSignInClient(activity).SignIn().AsAsync<AuthenticationResult>();
            await OnSignInResultAsync(activity, result.IsAuthenticated);
        });
        return IsSignedIn;
    }

    public async Task SyncAsync()
    {
        if (!IsSignedIn || IsSyncing || Platform.CurrentActivity is not Activity activity)
            return;

        IsSyncing = true;
        RaiseChanged();
        try
        {
            await Guard(async () =>
            {
                using Java.Lang.Object client = Jni.CallStatic("com/google/android/gms/games/PlayGames", "getSnapshotsClient",
                    "(Landroid/app/Activity;)Lcom/google/android/gms/games/SnapshotsClient;", new JValue(activity));
                using Java.Lang.Object snapshot = await OpenSnapshotAsync(client, create: true);
                using Java.Lang.Object contents = Jni.Call(snapshot, "getSnapshotContents",
                    "()Lcom/google/android/gms/games/snapshot/SnapshotContents;");

                byte[] cloud = Jni.CallBytes(contents, "readFully", "()[B");
                if (cloud.Length > 0)
                    _progress.MergeCloud(Encoding.UTF8.GetString(cloud));

                Jni.CallBool(contents, "writeBytes", "([B)Z", Encoding.UTF8.GetBytes(_progress.ExportCloud()));
                ISnapshotMetadataChange change = new SnapshotMetadataChangeBuilder()
                    .SetDescription($"Level {ProgressStore.LevelFor(_progress.TotalPoints)}")
                    .SetProgressValue(_progress.TotalPoints)
                    .Build();
                using GmsTask commit = Jni.CallTask(client, "commitAndClose",
                    "(Lcom/google/android/gms/games/snapshot/Snapshot;Lcom/google/android/gms/games/snapshot/SnapshotMetadataChange;)Lcom/google/android/gms/tasks/Task;",
                    new JValue(snapshot), new JValue(change));
                await commit.AsAsync<Java.Lang.Object>();
            });
        }
        finally
        {
            IsSyncing = false;
            RaiseChanged();
        }
    }

    public void ReportRun(GameMode mode, int size, long score, long? sprintMilliseconds)
    {
        if (!IsSignedIn || Platform.CurrentActivity is not Activity activity)
            return;

        GuardSync(() =>
        {
            ILeaderboardsClient boards = PlayGames.GetLeaderboardsClient(activity);
            string board = mode switch
            {
                GameMode.Classic => Id($"leaderboard_classic_{size}"),
                GameMode.TimeAttack => Id("leaderboard_time_attack"),
                _ => "",
            };
            if (board.Length > 0 && score > 0)
                boards.SubmitScore(board, score);
            if (sprintMilliseconds is long ms && Id("leaderboard_sprint") is { Length: > 0 } sprint)
                boards.SubmitScore(sprint, ms);
            if (Id("leaderboard_level") is { Length: > 0 } level)
                boards.SubmitScore(level, _progress.TotalPoints);

            int playerLevel = ProgressStore.LevelFor(_progress.TotalPoints);
            foreach (int target in LevelAchievements.Where(t => playerLevel >= t))
                Unlock(activity, $"achievement_level_{target}");
            foreach (int target in StreakAchievements.Where(t => _progress.BestStreak >= t))
                Unlock(activity, $"achievement_streak_{target}");
        });
        _ = SyncAsync();
    }

    public void ReportTile(int value)
    {
        if (!IsSignedIn || Platform.CurrentActivity is not Activity activity)
            return;
        GuardSync(() =>
        {
            foreach (int target in TileAchievements.Where(t => value >= t))
                Unlock(activity, $"achievement_tile_{target}");
        });
    }

    public Task ShowLeaderboardsAsync() => ShowUiAsync(a => PlayGames.GetLeaderboardsClient(a).GetAllLeaderboardsIntent());

    public Task ShowAchievementsAsync() => ShowUiAsync(a => PlayGames.GetAchievementsClient(a).GetAchievementsIntent());

    public async Task<bool> DeleteCloudDataAsync()
    {
        if (!IsSignedIn)
            return true;
        if (Platform.CurrentActivity is not Activity activity)
            return false;

        return await Guard(async () =>
        {
            using Java.Lang.Object client = Jni.CallStatic("com/google/android/gms/games/PlayGames", "getSnapshotsClient",
                "(Landroid/app/Activity;)Lcom/google/android/gms/games/SnapshotsClient;", new JValue(activity));
            using Java.Lang.Object snapshot = await OpenSnapshotAsync(client, create: true);
            using Java.Lang.Object live = Jni.Call(snapshot, "getMetadata", "()Lcom/google/android/gms/games/snapshot/SnapshotMetadata;");
            // A frozen copy stays valid after the snapshot is closed
            using Java.Lang.Object metadata = Jni.Call(live, "freeze", "()Ljava/lang/Object;");

            using (GmsTask discard = Jni.CallTask(client, "discardAndClose",
                "(Lcom/google/android/gms/games/snapshot/Snapshot;)Lcom/google/android/gms/tasks/Task;", new JValue(snapshot)))
                await discard.AsAsync<Java.Lang.Object>();

            using GmsTask delete = Jni.CallTask(client, "delete",
                "(Lcom/google/android/gms/games/snapshot/SnapshotMetadata;)Lcom/google/android/gms/tasks/Task;", new JValue(metadata));
            await delete.AsAsync<Java.Lang.Object>();
        });
    }

    // ------------------------------------------------------------------ helpers

    private async Task OnSignInResultAsync(Activity activity, bool signedIn)
    {
        IsSignedIn = signedIn;
        if (signedIn)
        {
            using Java.Lang.Object client = Jni.CallStatic("com/google/android/gms/games/PlayGames", "getPlayersClient",
                "(Landroid/app/Activity;)Lcom/google/android/gms/games/PlayersClient;", new JValue(activity));
            using GmsTask task = Jni.CallTask(client, "getCurrentPlayer", "()Lcom/google/android/gms/tasks/Task;");
            using Java.Lang.Object player = await task.AsAsync<Java.Lang.Object>();
            PlayerName = Jni.CallString(player, "getDisplayName", "()Ljava/lang/String;");
        }
        RaiseChanged();

        if (signedIn)
            await SyncAsync();
    }

    private static async Task<Java.Lang.Object> OpenSnapshotAsync(Java.Lang.Object client, bool create)
    {
        using var name = new Java.Lang.String(SnapshotName);
        using GmsTask open = Jni.CallTask(client, "open", "(Ljava/lang/String;ZI)Lcom/google/android/gms/tasks/Task;",
            new JValue(name), new JValue(create), new JValue(ResolutionHighestProgress));
        using Java.Lang.Object dataOrConflict = await open.AsAsync<Java.Lang.Object>();
        // With an automatic resolution policy the result is never a conflict
        return Jni.Call(dataOrConflict, "getData", "()Ljava/lang/Object;");
    }

    private async Task ShowUiAsync(Func<Activity, GmsTask> intentTask)
    {
        if (!IsSignedIn || Platform.CurrentActivity is not Activity activity)
            return;
        await Guard(async () =>
        {
            Intent intent = await intentTask(activity).AsAsync<Intent>();
            activity.StartActivityForResult(intent, UiRequestCode);
        });
    }

    private void Unlock(Activity activity, string name)
    {
        string id = Id(name);
        if (id.Length > 0 && _unlockedThisRun.Add(id))
            PlayGames.GetAchievementsClient(activity).Unlock(id);
    }

    private static string Id(string name) => Id(Application.Context, name);

    private static string Id(Context context, string name)
    {
        int resource = context.Resources?.GetIdentifier(name, "string", context.PackageName) ?? 0;
        return resource == 0 ? "" : context.GetString(resource).Trim();
    }

    private void RaiseChanged() => MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke());

    // Play Games failures (no network, an old Play Games app, a misconfigured project) leave the game as it was
    private async Task<bool> Guard(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Play Games: {ex}");
            return false;
        }
    }

    private static void GuardSync(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Play Games: {ex}");
        }
    }

    /// <summary>Plain JNI calls for the Java methods the binding does not expose.</summary>
    private static class Jni
    {
        public static Java.Lang.Object CallStatic(string className, string method, string signature, params JValue[] args)
        {
            IntPtr cls = JNIEnv.FindClass(className);
            try
            {
                IntPtr id = JNIEnv.GetStaticMethodID(cls, method, signature);
                return Wrap<Java.Lang.Object>(JNIEnv.CallStaticObjectMethod(cls, id, args));
            }
            finally
            {
                JNIEnv.DeleteGlobalRef(cls);
            }
        }

        public static Java.Lang.Object Call(Java.Lang.Object target, string method, string signature, params JValue[] args) =>
            Wrap<Java.Lang.Object>(JNIEnv.CallObjectMethod(target.Handle, Method(target, method, signature), args));

        public static GmsTask CallTask(Java.Lang.Object target, string method, string signature, params JValue[] args) =>
            Wrap<GmsTask>(JNIEnv.CallObjectMethod(target.Handle, Method(target, method, signature), args));

        public static string CallString(Java.Lang.Object target, string method, string signature) =>
            JNIEnv.GetString(JNIEnv.CallObjectMethod(target.Handle, Method(target, method, signature)), JniHandleOwnership.TransferLocalRef) ?? "";

        public static byte[] CallBytes(Java.Lang.Object target, string method, string signature)
        {
            IntPtr array = JNIEnv.CallObjectMethod(target.Handle, Method(target, method, signature));
            try
            {
                return array == IntPtr.Zero ? [] : JNIEnv.GetArray<byte>(array) ?? [];
            }
            finally
            {
                JNIEnv.DeleteLocalRef(array);
            }
        }

        public static bool CallBool(Java.Lang.Object target, string method, string signature, byte[] bytes)
        {
            IntPtr array = JNIEnv.NewArray(bytes);
            try
            {
                return JNIEnv.CallBooleanMethod(target.Handle, Method(target, method, signature), [new JValue(array)]);
            }
            finally
            {
                JNIEnv.DeleteLocalRef(array);
            }
        }

        private static IntPtr Method(Java.Lang.Object target, string method, string signature)
        {
            IntPtr cls = JNIEnv.GetObjectClass(target.Handle);
            try
            {
                return JNIEnv.GetMethodID(cls, method, signature);
            }
            finally
            {
                JNIEnv.DeleteLocalRef(cls);
            }
        }

        private static T Wrap<T>(IntPtr handle) where T : class, IJavaObject =>
            Java.Lang.Object.GetObject<T>(handle, JniHandleOwnership.TransferLocalRef)
            ?? throw new InvalidOperationException("Play Games returned nothing");
    }
}
