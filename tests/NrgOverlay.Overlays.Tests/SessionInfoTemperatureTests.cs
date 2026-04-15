using NrgOverlay.Core.Config;
using NrgOverlay.Overlays;

namespace NrgOverlay.Overlays.Tests;

public class SessionInfoTemperatureTests
{
    [Theory]
    [InlineData(0f,     "32.0\u00b0F")]   // freezing
    [InlineData(100f,   "212.0\u00b0F")] // boiling
    [InlineData(22.1f,  "71.8\u00b0F")]  // typical air temp
    [InlineData(38.7f,  "101.7\u00b0F")] // typical track temp
    [InlineData(-10f,   "14.0\u00b0F")]  // cold
    public void FormatTemp_Fahrenheit_ConvertsCorrectly(float tempC, string expected)
    {
        var result = SessionInfoOverlay.FormatTemp(tempC, TemperatureUnit.Fahrenheit);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(22.1f,  "22.1\u00b0C")]
    [InlineData(38.7f,  "38.7\u00b0C")]
    [InlineData(0f,     "0.0\u00b0C")]
    public void FormatTemp_Celsius_ReturnsCelsius(float tempC, string expected)
    {
        var result = SessionInfoOverlay.FormatTemp(tempC, TemperatureUnit.Celsius);
        Assert.Equal(expected, result);
    }
}

