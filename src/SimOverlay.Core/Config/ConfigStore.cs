using System.Text.Json;

namespace SimOverlay.Core.Config;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _path;
    private readonly object _saveLock = new();

    public ConfigStore() : this(DefaultPath()) { }

    public ConfigStore(string path) => _path = path;

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimOverlay",
            "config.json");

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new AppConfig();

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch (Exception ex)
        {
            AppLog.Exception("Failed to load config, using defaults", ex);
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        // Lock so concurrent debounced saves from multiple overlays don't race
        // on the .tmp file.
        lock (_saveLock)
        {
            var dir = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(dir);

            var tmp  = _path + ".tmp";
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(tmp, json);
            File.Move(tmp, _path, overwrite: true);
        }
    }
}
