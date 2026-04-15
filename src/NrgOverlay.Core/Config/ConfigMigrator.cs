namespace NrgOverlay.Core.Config;

/// <summary>
/// Runs sequential config migrations from the file's persisted version
/// up to <see cref="CurrentVersion"/>. Each migration is a static method
/// that mutates the <see cref="AppConfig"/> in place.
/// </summary>
public static class ConfigMigrator
{
    /// <summary>
    /// The latest config schema version. Bump this and add a corresponding
    /// migration method each time the config shape changes.
    /// </summary>
    public const int CurrentVersion = 7;

    /// <summary>
    /// Ordered list of migrations. Index 0 = v1в†’v2, index 1 = v2в†’v3, etc.
    /// </summary>
    private static readonly Action<AppConfig>[] Migrations =
    [
        MigrateV1ToV2,
        MigrateV2ToV3,
        MigrateV3ToV4,
        MigrateV4ToV5,
        MigrateV5ToV6,
        MigrateV6ToV7,
    ];

    /// <summary>
    /// Migrates <paramref name="config"/> from its current version to
    /// <see cref="CurrentVersion"/>. No-op if already at current version.
    /// </summary>
    public static void MigrateToLatest(AppConfig config)
    {
        if (config.Version >= CurrentVersion)
            return;

        // Version 0 means the field was absent in the JSON (pre-versioning MVP config).
        // Treat it the same as version 1.
        var startVersion = config.Version <= 0 ? 1 : config.Version;

        for (var v = startVersion; v < CurrentVersion; v++)
        {
            var migrationIndex = v - 1; // v1в†’v2 is index 0
            if (migrationIndex < Migrations.Length)
            {
                AppLog.Info($"Config migration: v{v} в†’ v{v + 1}");
                Migrations[migrationIndex](config);
            }
        }

        config.Version = CurrentVersion;
    }

    // ---------------------------------------------------------------------
    // Migration methods вЂ” add new ones at the end, one per version bump.
    // ---------------------------------------------------------------------

    /// <summary>
    /// v1 в†’ v2: Alpha infrastructure. Populates default values for new
    /// Alpha-phase fields that don't exist in MVP configs.
    /// </summary>
    private static void MigrateV1ToV2(AppConfig config)
    {
        // GlobalSettings: ensure object exists (defensive вЂ” should always be non-null)
        config.GlobalSettings ??= new GlobalSettings();

        // TASK-704 / ISSUE-011: SimPriorityOrder вЂ” default to iRacing if absent.
        if (config.GlobalSettings.SimPriorityOrder is not { Count: > 0 })
            config.GlobalSettings.SimPriorityOrder = ["iRacing"];
    }

    /// <summary>
    /// v2 в†’ v3: Multi-class data model (TASK-705).
    /// Populates the fallback class colour palette used when a sim does not
    /// provide per-class colours.
    /// </summary>
    private static void MigrateV2ToV3(AppConfig config)
    {
        config.GlobalSettings ??= new GlobalSettings();

        if (config.GlobalSettings.ClassColorPalette is not { Count: > 0 })
        {
            config.GlobalSettings.ClassColorPalette =
            [
                new ColorConfig { R = 0.20f, G = 0.60f, B = 1.00f, A = 1f }, // blue  вЂ” class 1
                new ColorConfig { R = 1.00f, G = 0.30f, B = 0.20f, A = 1f }, // red   вЂ” class 2
                new ColorConfig { R = 0.20f, G = 0.80f, B = 0.30f, A = 1f }, // green вЂ” class 3
                new ColorConfig { R = 1.00f, G = 0.80f, B = 0.00f, A = 1f }, // gold  вЂ” class 4
            ];
        }
    }

    /// <summary>
    /// v3 в†’ v4: LMU integration (TASK-905).
    /// Appends "LMU" to <see cref="GlobalSettings.SimPriorityOrder"/> so existing
    /// configs gain LMU detection without wiping the user's current priority order.
    /// </summary>
    private static void MigrateV3ToV4(AppConfig config)
    {
        config.GlobalSettings ??= new GlobalSettings();

        if (!config.GlobalSettings.SimPriorityOrder.Contains("LMU"))
            config.GlobalSettings.SimPriorityOrder.Add("LMU");
    }

    /// <summary>
    /// v4 в†’ v5: driver country enrichment config (override + cache dictionaries).
    /// Ensures both containers exist for runtime country resolution.
    /// </summary>
    private static void MigrateV4ToV5(AppConfig config)
    {
        config.GlobalSettings ??= new GlobalSettings();
        config.GlobalSettings.DriverCountryOverrides ??= [];
        config.GlobalSettings.DriverCountryCache ??= [];
    }

    /// <summary>
    /// v5 в†’ v6: initialize FlairID-to-country mapping dictionary.
    /// </summary>
    private static void MigrateV5ToV6(AppConfig config)
    {
        config.GlobalSettings ??= new GlobalSettings();
        config.GlobalSettings.DriverCountryByFlairId ??= [];
    }

    /// <summary>
    /// v6 в†’ v7: initialize FlairID-to-ISO2 dictionary for emoji rendering.
    /// </summary>
    private static void MigrateV6ToV7(AppConfig config)
    {
        config.GlobalSettings ??= new GlobalSettings();
        config.GlobalSettings.DriverCountryIso2ByFlairId ??= [];
    }
}

