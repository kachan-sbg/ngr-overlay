using NrgOverlay.Core.Config;
using Xunit;

namespace NrgOverlay.Core.Tests.Config;

public class ConfigStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;
    private readonly ConfigStore _store;

    public ConfigStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"NrgOverlayTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
        _store = new ConfigStore(_configPath);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var config = _store.Load();

        Assert.NotNull(config);
        Assert.NotNull(config.GlobalSettings);
        Assert.NotNull(config.Overlays);
        Assert.Empty(config.Overlays);
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaults()
    {
        File.WriteAllText(_configPath, "{ this is not valid JSON !!! }");

        var config = _store.Load();

        Assert.NotNull(config);
        Assert.Empty(config.Overlays);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = new AppConfig
        {
            GlobalSettings = new GlobalSettings { StartWithWindows = true },
            Overlays =
            [
                new OverlayConfig
                {
                    Id = "Relative",
                    X = 10,
                    Y = 20,
                    Width = 500,
                    Height = 380,
                    FontSize = 16f,
                },
            ],
        };

        _store.Save(original);
        var loaded = _store.Load();

        var overlay = loaded.Overlays[0];
        Assert.Equal("Relative", overlay.Id);
        Assert.Equal(10, overlay.X);
        Assert.Equal(20, overlay.Y);
        Assert.Equal(500, overlay.Width);
        Assert.Equal(380, overlay.Height);
        Assert.Equal(16f, overlay.FontSize);
        Assert.True(loaded.GlobalSettings.StartWithWindows);
    }

    [Fact]
    public void Save_WritesToTempThenMoves_NoPartialFileOnDisk()
    {
        _store.Save(new AppConfig());

        Assert.True(File.Exists(_configPath));
        Assert.False(File.Exists(_configPath + ".tmp"));
    }

    [Fact]
    public void Save_OverwritesExistingFile()
    {
        _store.Save(new AppConfig { GlobalSettings = new() { StartWithWindows = false } });
        _store.Save(new AppConfig { GlobalSettings = new() { StartWithWindows = true } });

        var loaded = _store.Load();
        Assert.True(loaded.GlobalSettings.StartWithWindows);
    }

    [Fact]
    public void Save_PathFailure_DoesNotThrow_AndTempFileIsCleanedUp()
    {
        var invalidTarget = Path.Combine(_tempDir, "as-directory");
        Directory.CreateDirectory(invalidTarget);

        var store = new ConfigStore(invalidTarget);
        var tmp = invalidTarget + ".tmp";

        var ex = Record.Exception(() => store.Save(new AppConfig()));

        Assert.Null(ex);
        Assert.False(File.Exists(tmp));
    }

    [Fact]
    public void Load_MvpConfigWithoutVersion_MigratesToCurrentVersion()
    {
        var mvpJson = """
        {
            "GlobalSettings": { "StartWithWindows": false },
            "Overlays": [
                { "Id": "Relative", "X": 100, "Y": 200, "Width": 500, "Height": 380 }
            ]
        }
        """;
        File.WriteAllText(_configPath, mvpJson);

        var config = _store.Load();

        Assert.Equal(ConfigMigrator.CurrentVersion, config.Version);
        Assert.Single(config.Overlays);
        Assert.Equal("Relative", config.Overlays[0].Id);
        Assert.Equal(100, config.Overlays[0].X);
    }

    [Fact]
    public void Load_CurrentVersionConfig_NoMigrationRuns()
    {
        var config = new AppConfig { Version = ConfigMigrator.CurrentVersion };
        config.Overlays.Add(new OverlayConfig { Id = "SessionInfo", X = 10 });
        _store.Save(config);

        var loaded = _store.Load();

        Assert.Equal(ConfigMigrator.CurrentVersion, loaded.Version);
        Assert.Single(loaded.Overlays);
        Assert.Equal("SessionInfo", loaded.Overlays[0].Id);
    }

    [Fact]
    public void Load_NewConfig_CreatedAtCurrentVersion()
    {
        var config = _store.Load();

        Assert.Equal(ConfigMigrator.CurrentVersion, config.Version);
    }

    [Fact]
    public void Save_PersistsVersionField()
    {
        var config = new AppConfig { Version = ConfigMigrator.CurrentVersion };
        _store.Save(config);

        var json = File.ReadAllText(_configPath);
        Assert.Contains("\"Version\"", json);
        Assert.Contains($"{ConfigMigrator.CurrentVersion}", json);
    }

    [Fact]
    public void RoundTrip_SimPriorityOrder_SerializesAndDeserializesCorrectly()
    {
        var config = new AppConfig
        {
            GlobalSettings = new GlobalSettings
            {
                SimPriorityOrder = ["iRacing", "LMU"],
            },
        };

        _store.Save(config);
        var loaded = _store.Load();

        Assert.Equal(2, loaded.GlobalSettings.SimPriorityOrder.Count);
        Assert.Equal("iRacing", loaded.GlobalSettings.SimPriorityOrder[0]);
        Assert.Equal("LMU", loaded.GlobalSettings.SimPriorityOrder[1]);
    }

    [Fact]
    public void Load_MissingSimPriorityOrder_DefaultsToIRacing()
    {
        var json = """
        {
            "Version": 1,
            "GlobalSettings": { "StartWithWindows": false },
            "Overlays": []
        }
        """;
        File.WriteAllText(_configPath, json);

        var config = _store.Load();

        Assert.NotNull(config.GlobalSettings.SimPriorityOrder);
        Assert.Contains("iRacing", config.GlobalSettings.SimPriorityOrder);
        Assert.Contains("LMU", config.GlobalSettings.SimPriorityOrder);
        Assert.Equal("iRacing", config.GlobalSettings.SimPriorityOrder[0]);
    }

    [Fact]
    public void Integration_FullRoundTrip_ComplexConfig()
    {
        var original = new AppConfig
        {
            GlobalSettings = new GlobalSettings
            {
                StartWithWindows = true,
            },
            Overlays =
            [
                new OverlayConfig
                {
                    Id = "Relative",
                    X = 50, Y = 100,
                    Width = 500, Height = 380,
                    FontSize = 13f,
                    ShowIRating = false,
                    MaxDriversShown = 11,
                },
                new OverlayConfig
                {
                    Id = "SessionInfo",
                    X = 600, Y = 100,
                    Width = 260, Height = 280,
                    TemperatureUnit = TemperatureUnit.Fahrenheit,
                },
                new OverlayConfig
                {
                    Id = "DeltaBar",
                    X = 800, Y = 500,
                    Width = 300, Height = 80,
                    DeltaBarMaxSeconds = 3f,
                    ShowTrendArrow = false,
                },
            ],
        };

        _store.Save(original);
        var loaded = _store.Load();

        Assert.Equal(3, loaded.Overlays.Count);
        Assert.True(loaded.GlobalSettings.StartWithWindows);

        var rel = loaded.Overlays[0];
        Assert.Equal("Relative", rel.Id);
        Assert.False(rel.ShowIRating);
        Assert.Equal(11, rel.MaxDriversShown);

        var session = loaded.Overlays[1];
        Assert.Equal(TemperatureUnit.Fahrenheit, session.TemperatureUnit);

        var delta = loaded.Overlays[2];
        Assert.Equal(3f, delta.DeltaBarMaxSeconds);
        Assert.False(delta.ShowTrendArrow);
    }
}
