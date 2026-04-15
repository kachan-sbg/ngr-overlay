using NrgOverlay.Core.Config;
using Xunit;

namespace NrgOverlay.Core.Tests.Config;

public class ConfigMigratorTests
{
    [Fact]
    public void MigrateToLatest_V1Config_MigratesToCurrentVersion()
    {
        var config = new AppConfig { Version = 1 };

        ConfigMigrator.MigrateToLatest(config);

        Assert.Equal(ConfigMigrator.CurrentVersion, config.Version);
    }

    [Fact]
    public void MigrateToLatest_V0Config_TreatedAsV1()
    {
        // Version=0 means the field was absent in JSON (int default).
        var config = new AppConfig { Version = 0 };

        ConfigMigrator.MigrateToLatest(config);

        Assert.Equal(ConfigMigrator.CurrentVersion, config.Version);
    }

    [Fact]
    public void MigrateToLatest_CurrentVersion_NoOp()
    {
        var config = new AppConfig { Version = ConfigMigrator.CurrentVersion };
        var originalOverlayCount = config.Overlays.Count;

        ConfigMigrator.MigrateToLatest(config);

        Assert.Equal(ConfigMigrator.CurrentVersion, config.Version);
        Assert.Equal(originalOverlayCount, config.Overlays.Count);
    }

    [Fact]
    public void MigrateToLatest_FutureVersion_NoOp()
    {
        var config = new AppConfig { Version = ConfigMigrator.CurrentVersion + 99 };

        ConfigMigrator.MigrateToLatest(config);

        // Should not downgrade.
        Assert.Equal(ConfigMigrator.CurrentVersion + 99, config.Version);
    }

    [Fact]
    public void MigrateV1ToV2_EnsuresGlobalSettingsNotNull()
    {
        var config = new AppConfig { Version = 1, GlobalSettings = null! };

        ConfigMigrator.MigrateToLatest(config);

        Assert.NotNull(config.GlobalSettings);
        Assert.Equal(ConfigMigrator.CurrentVersion, config.Version);
    }

    [Fact]
    public void MigrateV1ToV2_SetsSimPriorityOrderDefault()
    {
        var config = new AppConfig { Version = 1 };
        config.GlobalSettings.SimPriorityOrder = null!; // simulate missing field

        ConfigMigrator.MigrateToLatest(config);

        Assert.NotNull(config.GlobalSettings.SimPriorityOrder);
        // After full migration (v1в†’v4) the list contains both providers.
        Assert.Contains("iRacing", config.GlobalSettings.SimPriorityOrder);
        Assert.Contains("LMU",     config.GlobalSettings.SimPriorityOrder);
        Assert.Equal("iRacing", config.GlobalSettings.SimPriorityOrder[0]); // iRacing remains first
    }

    [Fact]
    public void MigrateV1ToV2_PreservesExistingOverlays()
    {
        var config = new AppConfig
        {
            Version = 1,
            Overlays =
            [
                new OverlayConfig { Id = "Relative", X = 50, Width = 500 },
                new OverlayConfig { Id = "DeltaBar", X = 800 },
            ],
        };

        ConfigMigrator.MigrateToLatest(config);

        Assert.Equal(2, config.Overlays.Count);
        Assert.Equal("Relative", config.Overlays[0].Id);
        Assert.Equal(50, config.Overlays[0].X);
        Assert.Equal(500, config.Overlays[0].Width);
        Assert.Equal("DeltaBar", config.Overlays[1].Id);
    }
}

