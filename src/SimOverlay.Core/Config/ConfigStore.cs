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
            AppConfig config;
            if (!File.Exists(_path))
            {
                config = new AppConfig();
            }
            else
            {
                var json = File.ReadAllText(_path);
                config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            }

            ConfigMigrator.MigrateToLatest(config);
            return config;
        }
        catch (Exception ex)
        {
            AppLog.Exception("Failed to load config, using defaults", ex);
            var fallback = new AppConfig();
            ConfigMigrator.MigrateToLatest(fallback);
            return fallback;
        }
    }

    public void Save(AppConfig config)
    {
        // Lock so concurrent debounced saves from multiple overlays don't race
        // on the .tmp file.
        lock (_saveLock)
        {
            var tmp = _path + ".tmp";
            try
            {
                var dir = Path.GetDirectoryName(_path)!;
                Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(tmp, json);
                File.Move(tmp, _path, overwrite: true);
            }
            catch (Exception ex)
            {
                AppLog.Exception("Failed to save config", ex);
                try
                {
                    if (File.Exists(tmp))
                        File.Delete(tmp);
                }
                catch
                {
                    // best effort cleanup
                }
            }
        }
    }
}
