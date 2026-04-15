using System.Text.Json;

namespace NrgOverlay.Core.Config;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _path;
    private readonly object _saveLock = new();

    public ConfigStore() : this(ResolveDefaultPath()) { }

    public ConfigStore(string path) => _path = path;

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NrgOverlay",
            "config.json");

    private static string LegacyPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimOverlay",
            "config.json");

    private static string ResolveDefaultPath()
    {
        var nrgPath = DefaultPath();
        if (File.Exists(nrgPath))
            return nrgPath;

        var legacyPath = LegacyPath();

        return File.Exists(legacyPath) ? legacyPath : nrgPath;
    }

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
            TryImportLegacyCountryMappings(config, _path);
            return config;
        }
        catch (Exception ex)
        {
            AppLog.Exception("Failed to load config, using defaults", ex);
            var fallback = new AppConfig();
            ConfigMigrator.MigrateToLatest(fallback);
            TryImportLegacyCountryMappings(fallback, _path);
            return fallback;
        }
    }

    private static void TryImportLegacyCountryMappings(AppConfig config, string activePath)
    {
        try
        {
            var legacyPath = LegacyPath();
            if (!File.Exists(legacyPath))
                return;

            // If we're actively reading legacy config as current, no import is needed.
            if (string.Equals(
                    Path.GetFullPath(activePath),
                    Path.GetFullPath(legacyPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            config.GlobalSettings ??= new GlobalSettings();
            config.GlobalSettings.DriverCountryByFlairId ??= [];
            config.GlobalSettings.DriverCountryIso2ByFlairId ??= [];
            config.GlobalSettings.DriverCountryCache ??= [];
            config.GlobalSettings.DriverCountryOverrides ??= [];

            var needIso3 = config.GlobalSettings.DriverCountryByFlairId.Count == 0;
            var needIso2 = config.GlobalSettings.DriverCountryIso2ByFlairId.Count == 0;
            var needCache = config.GlobalSettings.DriverCountryCache.Count == 0;
            var needOverrides = config.GlobalSettings.DriverCountryOverrides.Count == 0;

            if (!needIso3 && !needIso2 && !needCache && !needOverrides)
                return;

            var legacyJson = File.ReadAllText(legacyPath);
            var legacy = JsonSerializer.Deserialize<AppConfig>(legacyJson, JsonOptions);
            if (legacy?.GlobalSettings is null)
                return;

            bool changed = false;

            if (needIso3 && legacy.GlobalSettings.DriverCountryByFlairId is { Count: > 0 })
            {
                foreach (var kv in legacy.GlobalSettings.DriverCountryByFlairId)
                    config.GlobalSettings.DriverCountryByFlairId[kv.Key] = kv.Value;
                changed = true;
            }

            if (needIso2 && legacy.GlobalSettings.DriverCountryIso2ByFlairId is { Count: > 0 })
            {
                foreach (var kv in legacy.GlobalSettings.DriverCountryIso2ByFlairId)
                    config.GlobalSettings.DriverCountryIso2ByFlairId[kv.Key] = kv.Value;
                changed = true;
            }

            if (needCache && legacy.GlobalSettings.DriverCountryCache is { Count: > 0 })
            {
                foreach (var kv in legacy.GlobalSettings.DriverCountryCache)
                    config.GlobalSettings.DriverCountryCache[kv.Key] = kv.Value;
                changed = true;
            }

            if (needOverrides && legacy.GlobalSettings.DriverCountryOverrides is { Count: > 0 })
            {
                foreach (var kv in legacy.GlobalSettings.DriverCountryOverrides)
                    config.GlobalSettings.DriverCountryOverrides[kv.Key] = kv.Value;
                changed = true;
            }

            if (changed)
                AppLog.Info($"Imported legacy country mappings from '{legacyPath}'.");
        }
        catch (Exception ex)
        {
            AppLog.Exception("Failed to import legacy country mappings", ex);
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

