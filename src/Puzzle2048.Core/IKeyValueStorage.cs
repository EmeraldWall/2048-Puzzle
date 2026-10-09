namespace Puzzle2048.Core;

/// <summary>Where small settings and records are kept. The app plugs in Android preferences; tests use memory.</summary>
public interface IKeyValueStorage
{
    int GetInt(string key, int defaultValue);
    void SetInt(string key, int value);
    long GetLong(string key, long defaultValue);
    void SetLong(string key, long value);
    bool GetBool(string key, bool defaultValue);
    void SetBool(string key, bool value);
    string GetString(string key, string defaultValue);
    void SetString(string key, string value);
}

public sealed class MemoryStorage : IKeyValueStorage
{
    private readonly Dictionary<string, object> _values = [];

    public int GetInt(string key, int defaultValue) => Get(key, defaultValue);
    public void SetInt(string key, int value) => _values[key] = value;
    public long GetLong(string key, long defaultValue) => Get(key, defaultValue);
    public void SetLong(string key, long value) => _values[key] = value;
    public bool GetBool(string key, bool defaultValue) => Get(key, defaultValue);
    public void SetBool(string key, bool value) => _values[key] = value;
    public string GetString(string key, string defaultValue) => Get(key, defaultValue);
    public void SetString(string key, string value) => _values[key] = value;

    private T Get<T>(string key, T defaultValue) => _values.TryGetValue(key, out object? v) && v is T typed ? typed : defaultValue;
}
