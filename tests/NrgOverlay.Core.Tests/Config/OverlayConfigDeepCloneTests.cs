using NrgOverlay.Core.Config;
using Xunit;

namespace NrgOverlay.Core.Tests.Config;

public class OverlayConfigDeepCloneTests
{
    private static OverlayConfig FullConfig() => new()
    {
        Id = "Relative",
        Enabled = true,
        X = 100,
        Y = 200,
        Width = 500,
        Height = 380,
        Opacity = 0.9f,
        BackgroundColor = new ColorConfig { R = 0.1f, G = 0.2f, B = 0.3f, A = 0.4f },
        TextColor = new ColorConfig { R = 1f, G = 1f, B = 1f, A = 1f },
        FontSize = 16f,
        ShowIRating = true,
        ShowLicense = false,
        MaxDriversShown = 12,
        PlayerHighlightColor = new ColorConfig { R = 0.5f, G = 0.5f, B = 1f, A = 0.3f },
        ShowWeather = true,
        ShowDelta = false,
        ShowGameTime = true,
        Use12HourClock = true,
        TemperatureUnit = TemperatureUnit.Fahrenheit,
        DeltaBarMaxSeconds = 3f,
        FasterColor = new ColorConfig { R = 0f, G = 1f, B = 0f, A = 1f },
        SlowerColor = new ColorConfig { R = 1f, G = 0f, B = 0f, A = 1f },
        ShowTrendArrow = false,
        ShowDeltaText = true,
        StreamOverride = new StreamOverrideConfig
        {
            Enabled = true,
            Width = 800,
            Height = 600,
            Opacity = 0.7f,
            FontSize = 20f,
            BackgroundColor = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.9f },
            TextColor = new ColorConfig { R = 0.9f, G = 0.9f, B = 0.9f, A = 1f },
            ShowIRating = false,
            MaxDriversShown = 10,
            DeltaBarMaxSeconds = 5f,
            FasterColor = new ColorConfig { R = 0f, G = 0.8f, B = 0f, A = 1f },
        },
    };

    [Fact]
    public void DeepClone_ProducesIndependentObject()
    {
        var original = FullConfig();
        var clone = original.DeepClone();

        Assert.NotSame(original, clone);
    }

    [Fact]
    public void DeepClone_AllScalarFieldsSurviveRoundTrip()
    {
        var original = FullConfig();
        var clone = original.DeepClone();

        Assert.Equal(original.Id, clone.Id);
        Assert.Equal(original.Enabled, clone.Enabled);
        Assert.Equal(original.X, clone.X);
        Assert.Equal(original.Y, clone.Y);
        Assert.Equal(original.Width, clone.Width);
        Assert.Equal(original.Height, clone.Height);
        Assert.Equal(original.Opacity, clone.Opacity);
        Assert.Equal(original.FontSize, clone.FontSize);
        Assert.Equal(original.ShowIRating, clone.ShowIRating);
        Assert.Equal(original.ShowLicense, clone.ShowLicense);
        Assert.Equal(original.MaxDriversShown, clone.MaxDriversShown);
        Assert.Equal(original.ShowWeather, clone.ShowWeather);
        Assert.Equal(original.ShowDelta, clone.ShowDelta);
        Assert.Equal(original.ShowGameTime, clone.ShowGameTime);
        Assert.Equal(original.Use12HourClock, clone.Use12HourClock);
        Assert.Equal(original.TemperatureUnit, clone.TemperatureUnit);
        Assert.Equal(original.DeltaBarMaxSeconds, clone.DeltaBarMaxSeconds);
        Assert.Equal(original.ShowTrendArrow, clone.ShowTrendArrow);
        Assert.Equal(original.ShowDeltaText, clone.ShowDeltaText);
    }

    [Fact]
    public void DeepClone_ColorConfigFieldsAreIndependentCopies()
    {
        var original = FullConfig();
        var clone = original.DeepClone();

        Assert.NotSame(original.BackgroundColor, clone.BackgroundColor);
        Assert.NotSame(original.TextColor, clone.TextColor);
        Assert.NotSame(original.PlayerHighlightColor, clone.PlayerHighlightColor);
        Assert.NotSame(original.FasterColor, clone.FasterColor);
        Assert.NotSame(original.SlowerColor, clone.SlowerColor);

        // Values match
        Assert.Equal(original.BackgroundColor.R, clone.BackgroundColor.R);
        Assert.Equal(original.BackgroundColor.A, clone.BackgroundColor.A);
        Assert.Equal(original.FasterColor.G, clone.FasterColor.G);
    }

    [Fact]
    public void DeepClone_MutatingCloneDoesNotAffectOriginal()
    {
        var original = FullConfig();
        var clone = original.DeepClone();

        clone.Width = 9999;
        clone.BackgroundColor.R = 0.99f;
        clone.StreamOverride!.Width = 1234;
        clone.StreamOverride.BackgroundColor!.A = 0.1f;

        Assert.Equal(500, original.Width);
        Assert.Equal(0.1f, original.BackgroundColor.R);
        Assert.Equal(800, original.StreamOverride!.Width);
        Assert.Equal(0.9f, original.StreamOverride.BackgroundColor!.A);
    }

    [Fact]
    public void DeepClone_StreamOverrideFieldsSurviveRoundTrip()
    {
        var original = FullConfig();
        var clone = original.DeepClone();

        Assert.NotNull(clone.StreamOverride);
        Assert.NotSame(original.StreamOverride, clone.StreamOverride);
        Assert.Equal(original.StreamOverride!.Enabled, clone.StreamOverride!.Enabled);
        Assert.Equal(original.StreamOverride.Width, clone.StreamOverride.Width);
        Assert.Equal(original.StreamOverride.Height, clone.StreamOverride.Height);
        Assert.Equal(original.StreamOverride.Opacity, clone.StreamOverride.Opacity);
        Assert.Equal(original.StreamOverride.FontSize, clone.StreamOverride.FontSize);
        Assert.Equal(original.StreamOverride.ShowIRating, clone.StreamOverride.ShowIRating);
        Assert.Equal(original.StreamOverride.MaxDriversShown, clone.StreamOverride.MaxDriversShown);
        Assert.Equal(original.StreamOverride.DeltaBarMaxSeconds, clone.StreamOverride.DeltaBarMaxSeconds);
    }

    [Fact]
    public void DeepClone_StreamOverrideColorConfigsAreIndependent()
    {
        var original = FullConfig();
        var clone = original.DeepClone();

        Assert.NotSame(original.StreamOverride!.BackgroundColor, clone.StreamOverride!.BackgroundColor);
        Assert.NotSame(original.StreamOverride.FasterColor, clone.StreamOverride.FasterColor);

        Assert.Equal(original.StreamOverride.BackgroundColor!.A, clone.StreamOverride.BackgroundColor!.A);
        Assert.Equal(original.StreamOverride.FasterColor!.G, clone.StreamOverride.FasterColor!.G);
    }

    [Fact]
    public void DeepClone_NullStreamOverride_RemainsNull()
    {
        var original = FullConfig();
        original.StreamOverride = null;

        var clone = original.DeepClone();

        Assert.Null(clone.StreamOverride);
    }

    [Fact]
    public void DeepClone_NullStreamOverrideFields_RemainNull()
    {
        var original = FullConfig();
        original.StreamOverride = new StreamOverrideConfig
        {
            Enabled = true,
            Width = 800,
            // All other nullable fields left null
        };

        var clone = original.DeepClone();

        Assert.NotNull(clone.StreamOverride);
        Assert.Equal(800, clone.StreamOverride!.Width);
        Assert.Null(clone.StreamOverride.Height);
        Assert.Null(clone.StreamOverride.BackgroundColor);
        Assert.Null(clone.StreamOverride.TextColor);
        Assert.Null(clone.StreamOverride.FasterColor);
    }
}

