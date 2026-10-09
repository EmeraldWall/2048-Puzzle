using Puzzle2048.Core;

namespace Puzzle2048.App.Services;

/// <summary>Stores progress in the phone's private app preferences.</summary>
public sealed class PreferencesStorage : IKeyValueStorage
{
    public int GetInt(string key, int defaultValue) => Preferences.Default.Get(key, defaultValue);
    public void SetInt(string key, int value) => Preferences.Default.Set(key, value);
    public long GetLong(string key, long defaultValue) => Preferences.Default.Get(key, defaultValue);
    public void SetLong(string key, long value) => Preferences.Default.Set(key, value);
    public bool GetBool(string key, bool defaultValue) => Preferences.Default.Get(key, defaultValue);
    public void SetBool(string key, bool value) => Preferences.Default.Set(key, value);
    public string GetString(string key, string defaultValue) => Preferences.Default.Get(key, defaultValue);
    public void SetString(string key, string value) => Preferences.Default.Set(key, value);
}
