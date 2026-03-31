using SimOverlay.Core;
using SimOverlay.Sim.Contracts;

namespace SimOverlay.Core.Tests.Contracts;

public class LicenseClassTests
{
    [Theory]
    [InlineData(LicenseClass.R,   1.000f, 0.267f, 0.267f)]
    [InlineData(LicenseClass.D,   1.000f, 0.533f, 0.000f)]
    [InlineData(LicenseClass.C,   1.000f, 1.000f, 0.000f)]
    [InlineData(LicenseClass.B,   0.000f, 0.733f, 0.000f)]
    [InlineData(LicenseClass.A,   0.000f, 0.533f, 1.000f)]
    [InlineData(LicenseClass.Pro, 0.600f, 0.267f, 1.000f)]
    [InlineData(LicenseClass.WC,  1.000f, 0.267f, 1.000f)]
    public void GetColor_ReturnsCorrectRgb(LicenseClass cls, float r, float g, float b)
    {
        var (R, G, B, A) = cls.GetColor();

        Assert.Equal(r, R, precision: 2);
        Assert.Equal(g, G, precision: 2);
        Assert.Equal(b, B, precision: 2);
        Assert.Equal(1f, A);
    }

    [Fact]
    public void GetColor_AllValuesHaveFullAlpha()
    {
        foreach (LicenseClass cls in Enum.GetValues<LicenseClass>())
            Assert.Equal(1f, cls.GetColor().A);
    }

    [Fact]
    public void RequiresDarkText_OnlyTrueForC()
    {
        Assert.True(LicenseClass.C.RequiresDarkText());

        foreach (LicenseClass cls in Enum.GetValues<LicenseClass>())
            if (cls != LicenseClass.C)
                Assert.False(cls.RequiresDarkText());
    }

    [Fact]
    public void ISimProvider_StubSatisfiesInterface()
    {
        ISimProvider provider = new StubSimProvider();

        Assert.Equal("Stub", provider.SimId);
        Assert.False(provider.IsRunning());

        // Verify Start/Stop and StateChanged event wire up correctly.
        SimState? received = null;
        provider.StateChanged += s => received = s;
        provider.Start();

        Assert.Equal(SimState.Connected, received);
    }

    private sealed class StubSimProvider : ISimProvider
    {
        public string SimId => "Stub";
        public bool IsRunning() => false;
        public void Stop() { }
        public event Action<SimState>? StateChanged;

        public void Start() => StateChanged?.Invoke(SimState.Connected);
    }
}
