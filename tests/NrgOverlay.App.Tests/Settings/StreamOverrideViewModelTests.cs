using NrgOverlay.App.Settings;
using NrgOverlay.Core.Config;

namespace NrgOverlay.App.Tests.Settings;

public class StreamOverrideViewModelTests
{
    private static OverlayConfig BaseConfig() => new()
    {
        Id              = "test",
        Width           = 500,
        Height          = 380,
        FontSize        = 13f,
        Opacity         = 0.9f,
        MaxDriversShown = 15,
        ShowIRating     = true,
        ShowLicense     = true,
        ShowWeather     = true,
        ShowDelta       = true,
        DeltaBarMaxSeconds = 2f,
        ShowTrendArrow  = true,
        ShowDeltaText   = true,
        ShowReferenceLapTime = true,
        BackgroundColor = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.85f },
        TextColor       = new ColorConfig { R = 1f, G = 1f, B = 1f, A = 1f },
        FasterColor     = new ColorConfig { R = 0f, G = 1f, B = 0f, A = 1f },
        SlowerColor     = new ColorConfig { R = 1f, G = 0f, B = 0f, A = 1f },
        PlayerHighlightColor = new ColorConfig { R = 1f, G = 1f, B = 0f, A = 1f },
    };

    // в”Ђв”Ђ LoadFrom with null StreamOverrideConfig в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void LoadFrom_NullSrc_AllHasXxxAreFalse()
    {
        var vm = new StreamOverrideViewModel();
        vm.LoadFrom(null, BaseConfig());

        Assert.False(vm.HasWidth);
        Assert.False(vm.HasHeight);
        Assert.False(vm.HasOpacity);
        Assert.False(vm.HasFontSize);
        Assert.False(vm.HasBackgroundColor);
        Assert.False(vm.HasTextColor);
        Assert.False(vm.HasShowIRating);
        Assert.False(vm.HasShowLicense);
        Assert.False(vm.HasMaxDriversShown);
        Assert.False(vm.HasShowWeather);
        Assert.False(vm.HasShowDelta);
        Assert.False(vm.HasDeltaBarMaxSeconds);
        Assert.False(vm.HasFasterColor);
        Assert.False(vm.HasSlowerColor);
        Assert.False(vm.HasShowTrendArrow);
        Assert.False(vm.HasShowDeltaText);
        Assert.False(vm.HasShowReferenceLapTime);
    }

    [Fact]
    public void LoadFrom_NullSrc_ValuesInheritedFromBase()
    {
        var base_ = BaseConfig();
        var vm    = new StreamOverrideViewModel();
        vm.LoadFrom(null, base_);

        Assert.Equal(base_.Width,            vm.Width);
        Assert.Equal(base_.Height,           vm.Height);
        Assert.Equal(base_.FontSize,         vm.FontSize);
        Assert.Equal(base_.Opacity,          vm.Opacity);
        Assert.Equal(base_.MaxDriversShown,  vm.MaxDriversShown);
        Assert.Equal(base_.ShowIRating,      vm.ShowIRating);
        Assert.Equal(base_.DeltaBarMaxSeconds, vm.DeltaBarMaxSeconds);
        Assert.Equal(base_.ShowReferenceLapTime, vm.ShowReferenceLapTime);
    }

    // в”Ђв”Ђ LoadFrom with partial StreamOverrideConfig в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void LoadFrom_PartialOverride_HasXxxOnlyForSetFields()
    {
        var src = new StreamOverrideConfig
        {
            Enabled = true,
            Width   = 800,         // set
            FontSize = 20f,        // set
            // Height, Opacity, etc. в†’ null (not set)
        };

        var vm = new StreamOverrideViewModel();
        vm.LoadFrom(src, BaseConfig());

        Assert.True(vm.HasWidth);
        Assert.True(vm.HasFontSize);

        Assert.False(vm.HasHeight);
        Assert.False(vm.HasOpacity);
        Assert.False(vm.HasMaxDriversShown);
    }

    [Fact]
    public void LoadFrom_PartialOverride_ValuesFromOverrideTakePrecedence()
    {
        var base_ = BaseConfig(); // Width=500, FontSize=13
        var src   = new StreamOverrideConfig
        {
            Enabled  = true,
            Width    = 800,
            FontSize = 20f,
        };

        var vm = new StreamOverrideViewModel();
        vm.LoadFrom(src, base_);

        Assert.Equal(800,  vm.Width);
        Assert.Equal(20f,  vm.FontSize);
        Assert.Equal(base_.Height, vm.Height); // inherited
    }

    // в”Ђв”Ђ ToConfig в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void ToConfig_WhenNotEnabled_ReturnsNull()
    {
        var vm = new StreamOverrideViewModel();
        vm.LoadFrom(null, BaseConfig()); // Enabled defaults to false

        Assert.Null(vm.ToConfig());
    }

    [Fact]
    public void ToConfig_WhenEnabled_OnlyIncludesHasXxxFields()
    {
        var src = new StreamOverrideConfig
        {
            Enabled  = true,
            Width    = 800,
            // everything else null
        };

        var vm = new StreamOverrideViewModel();
        vm.LoadFrom(src, BaseConfig());

        var result = vm.ToConfig();

        Assert.NotNull(result);
        Assert.True(result.Enabled);
        Assert.Equal(800, result.Width);
        Assert.Null(result.Height);    // not set в†’ must remain null
        Assert.Null(result.FontSize);  // not set в†’ must remain null
        Assert.Null(result.Opacity);   // not set в†’ must remain null
    }

    [Fact]
    public void LoadFrom_ToConfig_RoundTrip_PreservesEnabledAndSetFields()
    {
        var src = new StreamOverrideConfig
        {
            Enabled       = true,
            Width         = 800,
            FontSize      = 18f,
            ShowIRating   = false,
            DeltaBarMaxSeconds = 5f,
            ShowReferenceLapTime = false,
        };

        var vm = new StreamOverrideViewModel();
        vm.LoadFrom(src, BaseConfig());
        var result = vm.ToConfig()!;

        Assert.True(result.Enabled);
        Assert.Equal(800,   result.Width);
        Assert.Equal(18f,   result.FontSize);
        Assert.Equal(false, result.ShowIRating);
        Assert.Equal(5f,    result.DeltaBarMaxSeconds);
        Assert.Equal(false, result.ShowReferenceLapTime);
        Assert.Null(result.Height);   // was null in src в†’ must still be null
        Assert.Null(result.Opacity);  // was null in src в†’ must still be null
    }
}

