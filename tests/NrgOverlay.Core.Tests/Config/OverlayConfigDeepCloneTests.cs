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
        ShowReferenceLapTime = true,
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
        Assert.Equal(original.ShowReferenceLapTime, clone.ShowReferenceLapTime);
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

        Assert.Equal(500, original.Width);
        Assert.Equal(0.1f, original.BackgroundColor.R);
    }
}
