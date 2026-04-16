using NrgOverlay.App.Settings;
using NrgOverlay.Core.Config;

namespace NrgOverlay.App.Tests.Settings;

public class OverlayConfigViewModelTests
{
    private static OverlayConfig MakeConfig() => new()
    {
        Id              = "Relative",
        Enabled         = true,
        X = 10, Y = 20,
        Width = 500, Height = 380,
        FontSize        = 14f,
        Opacity         = 0.85f,
        BackgroundColor = new ColorConfig { R = 0.1f, G = 0.1f, B = 0.1f, A = 0.9f },
        TextColor       = new ColorConfig { R = 1f,   G = 1f,   B = 1f,   A = 1f   },
        ShowIRating     = false,
        ShowLicense     = true,
        MaxDriversShown = 11,
        PlayerHighlightColor = new ColorConfig { R = 1f, G = 0.5f, B = 0f, A = 1f },
        ShowWeather     = false,
        ShowDelta       = true,
        ShowGameTime    = false,
        Use12HourClock  = true,
        TemperatureUnit = TemperatureUnit.Fahrenheit,
        DeltaBarMaxSeconds = 3f,
        ShowTrendArrow  = false,
        ShowDeltaText   = true,
        ShowReferenceLapTime = true,
        FasterColor     = new ColorConfig { R = 0f, G = 1f, B = 0f, A = 1f },
        SlowerColor     = new ColorConfig { R = 1f, G = 0f, B = 0f, A = 1f },
        StreamOverride  = new StreamOverrideConfig { Enabled = true, Width = 800 },
    };

    // в”Ђв”Ђ Round-trip в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void LoadFrom_ToConfig_PreservesScalarFields()
    {
        var config = MakeConfig();
        var vm     = new OverlayConfigViewModel();
        vm.LoadFrom(config);
        var result = vm.ToConfig();

        Assert.Equal(config.Id,     result.Id);
        Assert.Equal(config.X,      result.X);
        Assert.Equal(config.Y,      result.Y);
        Assert.Equal(config.Width,  result.Width);
        Assert.Equal(config.Height, result.Height);
        Assert.Equal(config.FontSize,        result.FontSize);
        Assert.Equal(config.Opacity,         result.Opacity);
        Assert.Equal(config.ShowIRating,     result.ShowIRating);
        Assert.Equal(config.ShowLicense,     result.ShowLicense);
        Assert.Equal(config.MaxDriversShown, result.MaxDriversShown);
        Assert.Equal(config.ShowWeather,     result.ShowWeather);
        Assert.Equal(config.ShowDelta,       result.ShowDelta);
        Assert.Equal(config.ShowGameTime,    result.ShowGameTime);
        Assert.Equal(config.Use12HourClock,  result.Use12HourClock);
        Assert.Equal(config.TemperatureUnit, result.TemperatureUnit);
        Assert.Equal(config.DeltaBarMaxSeconds, result.DeltaBarMaxSeconds);
        Assert.Equal(config.ShowTrendArrow,  result.ShowTrendArrow);
        Assert.Equal(config.ShowDeltaText,   result.ShowDeltaText);
        Assert.Equal(config.ShowReferenceLapTime, result.ShowReferenceLapTime);
    }

    [Fact]
    public void LoadFrom_ToConfig_PopulatesStreamOverride()
    {
        var config = MakeConfig();
        var vm     = new OverlayConfigViewModel();
        vm.LoadFrom(config);
        var result = vm.ToConfig();

        Assert.NotNull(result.StreamOverride);
        Assert.True(result.StreamOverride.Enabled);
        Assert.Equal(800, result.StreamOverride.Width);
    }

    [Fact]
    public void LoadFrom_NullStreamOverride_ToConfigReturnsNull()
    {
        var config = MakeConfig();
        config.StreamOverride = null;

        var vm = new OverlayConfigViewModel();
        vm.LoadFrom(config);
        var result = vm.ToConfig();

        // StreamOverrideViewModel.ToConfig() returns null when Enabled=false (inherited default)
        Assert.Null(result.StreamOverride);
    }

    // в”Ђв”Ђ PropertyChanged в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void LoadFrom_RaisesPropertyChangedForAll()
    {
        var vm      = new OverlayConfigViewModel();
        bool notified = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName is null) notified = true; };

        vm.LoadFrom(MakeConfig());

        Assert.True(notified);
    }

    [Fact]
    public void SetWidth_RaisesPropertyChanged_OnlyWhenValueChanges()
    {
        var vm = new OverlayConfigViewModel();
        vm.LoadFrom(MakeConfig()); // Width = 500

        var fired = new List<string?>();
        vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName);

        vm.Width = 500; // same value вЂ” should NOT fire
        Assert.Empty(fired);

        vm.Width = 600; // different вЂ” should fire
        Assert.Single(fired);
        Assert.Equal(nameof(vm.Width), fired[0]);
    }
}

