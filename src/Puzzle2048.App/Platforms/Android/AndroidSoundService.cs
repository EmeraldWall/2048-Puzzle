using Android.Media;
using Puzzle2048.App.Services;

namespace Puzzle2048.App.Platforms.Android;

/// <summary>
/// Plays the short effects with Android's SoundPool, which mixes several sounds at once with very low delay.
/// The WAV files ship as app assets and are copied to the cache once, because SoundPool needs real files.
/// </summary>
public sealed class AndroidSoundService : ISoundService
{
    private static readonly string[] Names =
    [
        "move", "tap", "combo", "milestone", "win", "levelup", "lose", "whoosh",
        "pop_01", "pop_02", "pop_03", "pop_04", "pop_05", "pop_06",
        "pop_07", "pop_08", "pop_09", "pop_10", "pop_11", "pop_12",
    ];

    private readonly Dictionary<string, int> _ids = [];
    private SoundPool? _pool;
    private Task? _loading;

    public Task PreloadAsync() => _loading ??= LoadAsync();

    public void Play(Sfx sound)
    {
        (string name, float volume) = sound switch
        {
            Sfx.Move => ("move", 0.35f),
            Sfx.Tap => ("tap", 0.6f),
            Sfx.Combo => ("combo", 0.8f),
            Sfx.Milestone => ("milestone", 0.9f),
            Sfx.Win => ("win", 1f),
            Sfx.LevelUp => ("levelup", 0.9f),
            Sfx.Lose => ("lose", 0.8f),
            _ => ("whoosh", 0.6f),
        };
        PlayNamed(name, volume);
    }

    public void PlayMerge(int tileValue)
    {
        int step = Math.Clamp((int)Math.Log2(Math.Max(4, tileValue)) - 1, 1, 12);
        PlayNamed($"pop_{step:00}", 0.85f);
    }

    private void PlayNamed(string name, float volume)
    {
        if (!AppServices.Progress.SoundEnabled || _pool is null || !_ids.TryGetValue(name, out int id))
            return;
        _pool.Play(id, volume, volume, 1, 0, 1f);
    }

    private async Task LoadAsync()
    {
        AudioAttributes attributes = new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Game)!
            .SetContentType(AudioContentType.Sonification)!
            .Build()!;
        var pool = new SoundPool.Builder().SetMaxStreams(8)!.SetAudioAttributes(attributes)!.Build()!;

        // A folder per app version, so updated sounds replace old copies
        string folder = Path.Combine(FileSystem.Current.CacheDirectory, $"sfx-{AppInfo.Current.BuildString}");
        Directory.CreateDirectory(folder);

        foreach (string name in Names)
        {
            string target = Path.Combine(folder, name + ".wav");
            try
            {
                if (!File.Exists(target))
                {
                    await using System.IO.Stream source = await FileSystem.Current.OpenAppPackageFileAsync($"sfx/{name}.wav");
                    await using FileStream file = File.Create(target);
                    await source.CopyToAsync(file);
                }
                _ids[name] = pool.Load(target, 1);
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sound {name} not loaded: {ex.Message}");
            }
        }

        _pool = pool;
    }
}
