using System.Text.Json;

namespace NrgOverlay.Core.Config;

public sealed class OverlayConfig
{
    private static readonly JsonSerializerOptions CloneOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Returns an independent deep copy. All reference-type fields (for example
    /// color configs) are new instances so mutating the clone cannot affect the original.
    /// </summary>
    public OverlayConfig DeepClone() =>
        JsonSerializer.Deserialize<OverlayConfig>(
            JsonSerializer.Serialize(this, CloneOptions), CloneOptions)!;

    public string Id { get; set; } = "";
    public bool Enabled { get; set; } = true;

    public int X { get; set; }
    public int Y { get; set; }

    public int Width { get; set; } = 300;
    public int Height { get; set; } = 200;

    public float Opacity { get; set; } = 1f;
    public ColorConfig BackgroundColor { get; set; } = ColorConfig.DarkBackground;
    public ColorConfig TextColor { get; set; } = ColorConfig.White;
    public float FontSize { get; set; } = 13f;

    // Relative overlay
    public bool ShowIRating { get; set; } = true;
    public bool ShowLicense { get; set; } = true;
    public int MaxDriversShown { get; set; } = 15;
    public ColorConfig PlayerHighlightColor { get; set; } = ColorConfig.PlayerHighlight;

    // Session Info overlay
    public bool ShowWeather { get; set; } = true;
    public bool ShowDelta { get; set; } = true;
    public bool ShowGameTime { get; set; } = true;
    public bool Use12HourClock { get; set; }
    public TemperatureUnit TemperatureUnit { get; set; } = TemperatureUnit.Celsius;

    // Delta Bar overlay
    public float DeltaBarMaxSeconds { get; set; } = 3f;
    public ColorConfig FasterColor { get; set; } = new() { R = 0.56f, G = 0.85f, B = 0.66f, A = 0.95f };
    public ColorConfig SlowerColor { get; set; } = new() { R = 0.95f, G = 0.65f, B = 0.65f, A = 0.95f };
    public bool ShowTrendArrow { get; set; } = true;
    public bool ShowDeltaText { get; set; } = true;
    public bool ShowReferenceLapTime { get; set; } = true;

    // Standings overlay
    public StandingsDisplayMode StandingsDisplayMode { get; set; } = StandingsDisplayMode.Combined;
    public bool ShowClassBadge      { get; set; } = true;
    public bool ShowBestLap         { get; set; } = true;
    public bool ShowLastLap         { get; set; } = true;
    public bool ShowInterval        { get; set; } = true;
    public bool ShowStint           { get; set; } = true;
    public bool ShowPositionsGained { get; set; } = true;
    public bool ShowTeam            { get; set; } = false;
    public bool ShowPitTime         { get; set; } = false;
    public int MaxStandingsRows     { get; set; } = 30;

    // Fuel Calculator overlay
    public FuelUnit FuelUnit { get; set; } = FuelUnit.Liters;
    public float FuelSafetyMarginLaps { get; set; } = 1.0f;
    public bool ShowFuelMargin { get; set; } = true;

    // Input Telemetry overlay
    public bool ShowThrottle { get; set; } = true;
    public bool ShowBrake { get; set; } = true;
    public bool ShowClutch { get; set; } = true;
    public bool ShowInputTrace { get; set; } = true;
    public bool ShowGearSpeed { get; set; } = true;
    public SpeedUnit SpeedUnit { get; set; } = SpeedUnit.Kph;
    public ColorConfig ThrottleColor { get; set; } = ColorConfig.Green;
    public ColorConfig BrakeColor { get; set; } = ColorConfig.Red;
    public ColorConfig ClutchColor { get; set; } = ColorConfig.Blue;

    // Pit Helper overlay
    public bool ShowPitServices { get; set; } = true;
    public bool ShowNextPitEstimate { get; set; } = true;

    // Weather overlay
    public bool ShowHumidity { get; set; } = true;
    public bool ShowWind { get; set; } = true;
    public WindSpeedUnit WindSpeedUnit { get; set; } = WindSpeedUnit.Kph;

    // Flat Track Map overlay
    public FlatMapLabelMode FlatMapLabelMode { get; set; } = FlatMapLabelMode.CarNumber;
    public float PlayerMarkerSize { get; set; } = 8f;
    public float CarMarkerSize { get; set; } = 4f;
    public bool ShowPitCars { get; set; } = true;
}
