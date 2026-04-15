using System.Globalization;
using System.Windows.Data;
using NrgOverlay.App.Settings;
using NrgOverlay.Core.Config;
using Binding = System.Windows.Data.Binding;

namespace NrgOverlay.App.Tests.Settings;

public class EnumBoolConverterTests
{
    private readonly EnumBoolConverter _converter = EnumBoolConverter.Instance;

    // в”Ђв”Ђ Convert (enum в†’ bool for IsChecked) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

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

    // в”Ђв”Ђ ConvertBack (bool в†’ enum for radio binding) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

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

    // в”Ђв”Ђ Instance is singleton в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void Instance_IsSingleton()
    {
        Assert.Same(EnumBoolConverter.Instance, EnumBoolConverter.Instance);
    }
}

