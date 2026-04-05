using System.Globalization;
using System.Windows.Data;
using SimOverlay.App.Settings;
using SimOverlay.Core.Config;
using Binding = System.Windows.Data.Binding;

namespace SimOverlay.App.Tests.Settings;

public class EnumBoolConverterTests
{
    private readonly EnumBoolConverter _converter = EnumBoolConverter.Instance;

    // ── Convert (enum → bool for IsChecked) ──────────────────────────────────

    [Fact]
    public void Convert_MatchingParameter_ReturnsTrue()
    {
        var result = _converter.Convert(
            TemperatureUnit.Celsius,
            typeof(bool),
            "Celsius",
            CultureInfo.InvariantCulture);

        Assert.Equal(true, result);
    }

    [Fact]
    public void Convert_NonMatchingParameter_ReturnsFalse()
    {
        var result = _converter.Convert(
            TemperatureUnit.Celsius,
            typeof(bool),
            "Fahrenheit",
            CultureInfo.InvariantCulture);

        Assert.Equal(false, result);
    }

    [Fact]
    public void Convert_NullValue_ReturnsFalse()
    {
        var result = _converter.Convert(null!, typeof(bool), "Celsius", CultureInfo.InvariantCulture);
        Assert.Equal(false, result);
    }

    // ── ConvertBack (bool → enum for radio binding) ───────────────────────────

    [Fact]
    public void ConvertBack_TrueWithParameter_ReturnsMatchingEnumValue()
    {
        var result = _converter.ConvertBack(
            true,
            typeof(TemperatureUnit),
            "Fahrenheit",
            CultureInfo.InvariantCulture);

        Assert.Equal(TemperatureUnit.Fahrenheit, result);
    }

    [Fact]
    public void ConvertBack_False_ReturnsDoNothing()
    {
        var result = _converter.ConvertBack(
            false,
            typeof(TemperatureUnit),
            "Fahrenheit",
            CultureInfo.InvariantCulture);

        Assert.Equal(Binding.DoNothing, result);
    }

    // ── Instance is singleton ─────────────────────────────────────────────────

    [Fact]
    public void Instance_IsSingleton()
    {
        Assert.Same(EnumBoolConverter.Instance, EnumBoolConverter.Instance);
    }
}
