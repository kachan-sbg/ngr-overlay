using SimOverlay.App.Settings;
using SimOverlay.Core.Config;

namespace SimOverlay.App.Tests.Settings;

public class ColorViewModelTests
{
    // ── LoadFrom / ToColorConfig round-trip ───────────────────────────────────

    [Theory]
    [InlineData(0f,    0f,    0f,    0f,    0,   0,   0,   0  )]
    [InlineData(1f,    1f,    1f,    1f,    255, 255, 255, 255)]
    [InlineData(0.5f,  0.25f, 0.75f, 0.8f,  128, 64,  191, 204)]
    public void LoadFrom_ConvertsFloatChannelsToInt(
        float r, float g, float b, float a,
        int expectedR, int expectedG, int expectedB, int expectedA)
    {
        var vm = new ColorViewModel();
        vm.LoadFrom(new ColorConfig { R = r, G = g, B = b, A = a });

        Assert.Equal(expectedR, vm.R);
        Assert.Equal(expectedG, vm.G);
        Assert.Equal(expectedB, vm.B);
        Assert.Equal(expectedA, vm.A);
    }

    [Fact]
    public void ToColorConfig_RoundTripsApproximately()
    {
        var vm = new ColorViewModel();
        var original = new ColorConfig { R = 0.6f, G = 0.3f, B = 0.9f, A = 1f };
        vm.LoadFrom(original);
        var result = vm.ToColorConfig();

        // Tolerate ±1/255 rounding (~0.004)
        Assert.InRange(result.R, original.R - 0.005f, original.R + 0.005f);
        Assert.InRange(result.G, original.G - 0.005f, original.G + 0.005f);
        Assert.InRange(result.B, original.B - 0.005f, original.B + 0.005f);
        Assert.InRange(result.A, original.A - 0.005f, original.A + 0.005f);
    }

    // ── Channel clamping ─────────────────────────────────────────────────────

    [Fact]
    public void SetR_BelowZero_ClampedToZero()
    {
        var vm = new ColorViewModel();
        vm.R = -10;
        Assert.Equal(0, vm.R);
    }

    [Fact]
    public void SetA_AboveMax_ClampedTo255()
    {
        var vm = new ColorViewModel();
        vm.A = 999;
        Assert.Equal(255, vm.A);
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    [Fact]
    public void SetChannel_RaisesPropertyChanged()
    {
        var vm = new ColorViewModel();
        var fired = new List<string?>();
        vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName);

        vm.R = 100;

        Assert.NotEmpty(fired);
    }

    [Fact]
    public void LoadFrom_RaisesPropertyChangedForAll()
    {
        var vm = new ColorViewModel();
        bool notified = false;
        // null name = "all properties"
        vm.PropertyChanged += (_, e) => { if (e.PropertyName is null) notified = true; };

        vm.LoadFrom(new ColorConfig { R = 1f, G = 0f, B = 0f, A = 1f });

        Assert.True(notified);
    }

    // ── PreviewBrush ──────────────────────────────────────────────────────────

    [Fact]
    public void PreviewBrush_ReflectsChannelValues()
    {
        var vm = new ColorViewModel();
        vm.LoadFrom(new ColorConfig { R = 1f, G = 0f, B = 0f, A = 1f });

        var brush = (System.Windows.Media.SolidColorBrush)vm.PreviewBrush;

        Assert.Equal(255, brush.Color.R);
        Assert.Equal(0,   brush.Color.G);
        Assert.Equal(0,   brush.Color.B);
        Assert.Equal(255, brush.Color.A);
    }
}
