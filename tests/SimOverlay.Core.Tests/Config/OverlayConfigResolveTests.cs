using SimOverlay.Core.Config;
using Xunit;

namespace SimOverlay.Core.Tests.Config;

public class OverlayConfigResolveTests
{
    private static OverlayConfig BaseConfig() => new()
    {
        Id = "Relative",
        X = 100,
        Y = 200,
        Width = 500,
        Height = 380,
        FontSize = 13f,
        Opacity = 0.9f,
        MaxDriversShown = 15,
        ShowIRating = true,
        DeltaBarMaxSeconds = 2f,
    };

    [Fact]
    public void Resolve_StreamModeInactive_ReturnsBaseConfig()
    {
        var config = BaseConfig();
        config.StreamOverride = new StreamOverrideConfig
        {
            Enabled = true,
            Width = 800,
            FontSize = 20f,
        };

        var resolved = config.Resolve(streamModeActive: false);

        Assert.Same(config, resolved);
    }

    [Fact]
    public void Resolve_StreamModeActive_OverrideDisabled_ReturnsBaseConfig()
    {
        var config = BaseConfig();
        config.StreamOverride = new StreamOverrideConfig
        {
            Enabled = false,
            Width = 800,
        };

        var resolved = config.Resolve(streamModeActive: true);

        Assert.Same(config, resolved);
    }

    [Fact]
    public void Resolve_StreamModeActive_NullOverride_ReturnsBaseConfig()
    {
        var config = BaseConfig();
        config.StreamOverride = null;

        var resolved = config.Resolve(streamModeActive: true);

        Assert.Same(config, resolved);
    }

    [Fact]
    public void Resolve_StreamModeActive_FullyNullOverrideFields_ReturnsBaseValues()
    {
        var config = BaseConfig();
        config.StreamOverride = new StreamOverrideConfig { Enabled = true };

        var resolved = config.Resolve(streamModeActive: true);

        Assert.Equal(config.Width, resolved.Width);
        Assert.Equal(config.Height, resolved.Height);
        Assert.Equal(config.FontSize, resolved.FontSize);
        Assert.Equal(config.Opacity, resolved.Opacity);
        Assert.Equal(config.MaxDriversShown, resolved.MaxDriversShown);
    }

    [Fact]
    public void Resolve_StreamModeActive_PartialOverride_MixesValues()
    {
        var config = BaseConfig();
        config.StreamOverride = new StreamOverrideConfig
        {
            Enabled = true,
            Width = 800,
            FontSize = 20f,
            // Height, Opacity, MaxDriversShown left null → inherit from base
        };

        var resolved = config.Resolve(streamModeActive: true);

        Assert.Equal(800, resolved.Width);           // from override
        Assert.Equal(20f, resolved.FontSize);        // from override
        Assert.Equal(config.Height, resolved.Height);       // from base
        Assert.Equal(config.Opacity, resolved.Opacity);     // from base
        Assert.Equal(config.MaxDriversShown, resolved.MaxDriversShown); // from base
    }

    [Fact]
    public void Resolve_XYAreNeverTakenFromOverride()
    {
        // StreamOverrideConfig intentionally has no X/Y fields.
        // Verify the resolved config always uses the base X/Y.
        var config = BaseConfig(); // X=100, Y=200
        config.StreamOverride = new StreamOverrideConfig { Enabled = true, Width = 999 };

        var resolved = config.Resolve(streamModeActive: true);

        Assert.Equal(100, resolved.X);
        Assert.Equal(200, resolved.Y);
    }

    [Fact]
    public void Resolve_StreamOverrideIsNullOnResolvedCopy()
    {
        var config = BaseConfig();
        config.StreamOverride = new StreamOverrideConfig { Enabled = true, Width = 700 };

        var resolved = config.Resolve(streamModeActive: true);

        Assert.Null(resolved.StreamOverride);
    }
}
