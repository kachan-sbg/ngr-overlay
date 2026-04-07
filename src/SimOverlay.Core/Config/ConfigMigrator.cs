namespace SimOverlay.Core.Config;

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
    public const int CurrentVersion = 3;

    /// <summary>
    /// Ordered list of migrations. Index 0 = v1→v2, index 1 = v2→v3, etc.
    /// </summary>
    private static readonly Action<AppConfig>[] Migrations =
    [
        MigrateV1ToV2,
        MigrateV2ToV3,
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
            var migrationIndex = v - 1; // v1→v2 is index 0
            if (migrationIndex < Migrations.Length)
            {
                AppLog.Info($"Config migration: v{v} → v{v + 1}");
                Migrations[migrationIndex](config);
            }
        }

        config.Version = CurrentVersion;
    }

    // ---------------------------------------------------------------------
    // Migration methods — add new ones at the end, one per version bump.
    // ---------------------------------------------------------------------

    /// <summary>
    /// v1 → v2: Alpha infrastructure. Populates default values for new
    /// Alpha-phase fields that don't exist in MVP configs.
    /// </summary>
    private static void MigrateV1ToV2(AppConfig config)
    {
        // GlobalSettings: ensure object exists (defensive — should always be non-null)
        config.GlobalSettings ??= new GlobalSettings();

        // TASK-704 / ISSUE-011: SimPriorityOrder — default to iRacing if absent.
        if (config.GlobalSettings.SimPriorityOrder is not { Count: > 0 })
            config.GlobalSettings.SimPriorityOrder = ["iRacing"];
    }

    /// <summary>
    /// v2 → v3: Multi-class data model (TASK-705).
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
                new ColorConfig { R = 0.20f, G = 0.60f, B = 1.00f, A = 1f }, // blue  — class 1
                new ColorConfig { R = 1.00f, G = 0.30f, B = 0.20f, A = 1f }, // red   — class 2
                new ColorConfig { R = 0.20f, G = 0.80f, B = 0.30f, A = 1f }, // green — class 3
                new ColorConfig { R = 1.00f, G = 0.80f, B = 0.00f, A = 1f }, // gold  — class 4
            ];
        }
    }
}
