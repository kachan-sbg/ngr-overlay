using SimOverlay.Core.Config;
using Xunit;

namespace SimOverlay.Core.Tests.Config;

public class ConfigStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;
    private readonly ConfigStore _store;

    public ConfigStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"SimOverlayTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
        _store = new ConfigStore(_configPath);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // --- Unit tests ---

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var config = _store.Load();

        Assert.NotNull(config);
        Assert.NotNull(config.GlobalSettings);
        Assert.NotNull(config.Overlays);
        Assert.Empty(config.Overlays);
        Assert.False(config.GlobalSettings.StreamModeActive);
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
    public void RoundTrip_PreservesAllFields_IncludingNullOverrideFields()
    {
        var original = new AppConfig
        {
            GlobalSettings = new GlobalSettings { StreamModeActive = true },
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
                    StreamOverride = new StreamOverrideConfig
                    {
                        Enabled = true,
                        Width = 800,
                        // FontSize, Height, etc. intentionally left null
                    },
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
        Assert.NotNull(overlay.StreamOverride);
        Assert.True(overlay.StreamOverride.Enabled);
        Assert.Equal(800, overlay.StreamOverride.Width);
        Assert.Null(overlay.StreamOverride.FontSize);   // null preserved
        Assert.Null(overlay.StreamOverride.Height);      // null preserved
        Assert.True(loaded.GlobalSettings.StreamModeActive);
    }

    [Fact]
    public void Save_WritesToTempThenMoves_NoPartialFileOnDisk()
    {
        // After save, no .tmp file should remain.
        _store.Save(new AppConfig());

        Assert.True(File.Exists(_configPath));
        Assert.False(File.Exists(_configPath + ".tmp"));
    }

    [Fact]
    public void Save_OverwritesExistingFile()
    {
        _store.Save(new AppConfig { GlobalSettings = new() { StreamModeActive = false } });
        _store.Save(new AppConfig { GlobalSettings = new() { StreamModeActive = true } });

        var loaded = _store.Load();
        Assert.True(loaded.GlobalSettings.StreamModeActive);
    }

    // --- Version / migration tests ---

    [Fact]
    public void Load_MvpConfigWithoutVersion_MigratesToCurrentVersion()
    {
        // Simulate an MVP-era config file that has no Version field.
        var mvpJson = """
        {
            "GlobalSettings": { "StreamModeActive": false },
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
        // Save a config at current version, then reload — version stays the same.
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
        // No file on disk — fresh config should be at current version.
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

    // --- Integration test ---

    [Fact]
    public void Integration_FullRoundTrip_ComplexConfig()
    {
        var original = new AppConfig
        {
            GlobalSettings = new GlobalSettings
            {
                StreamModeActive = true,
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
                    StreamOverride = new StreamOverrideConfig
                    {
                        Enabled = true,
                        Width = 700,
                        FontSize = 16f,
                    },
                },
                new OverlayConfig
                {
                    Id = "SessionInfo",
                    X = 600, Y = 100,
                    Width = 260, Height = 280,
                    TemperatureUnit = TemperatureUnit.Fahrenheit,
                    StreamOverride = null,
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
        Assert.True(loaded.GlobalSettings.StreamModeActive);
        Assert.True(loaded.GlobalSettings.StartWithWindows);

        var rel = loaded.Overlays[0];
        Assert.Equal("Relative", rel.Id);
        Assert.False(rel.ShowIRating);
        Assert.Equal(11, rel.MaxDriversShown);
        Assert.NotNull(rel.StreamOverride);
        Assert.Equal(700, rel.StreamOverride.Width);
        Assert.Null(rel.StreamOverride.Height); // not set — still null after round-trip

        var session = loaded.Overlays[1];
        Assert.Equal(TemperatureUnit.Fahrenheit, session.TemperatureUnit);
        Assert.Null(session.StreamOverride);

        var delta = loaded.Overlays[2];
        Assert.Equal(3f, delta.DeltaBarMaxSeconds);
        Assert.False(delta.ShowTrendArrow);
    }
}
