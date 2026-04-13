namespace SimOverlay.Core.Config;

/// <summary>
/// Nullable mirror of all overridable <see cref="OverlayConfig"/> fields.
/// A null value means "inherit from base config".
/// X/Y position fields are intentionally absent — position is never overridable.
/// </summary>
public sealed class StreamOverrideConfig
{
    public bool Enabled { get; set; }

    // --- Size ---
    public int? Width { get; set; }
    public int? Height { get; set; }

    // --- Appearance ---
    public float? Opacity { get; set; }
    public ColorConfig? BackgroundColor { get; set; }
    public ColorConfig? TextColor { get; set; }
    public float? FontSize { get; set; }

    // --- Relative overlay ---
    public bool? ShowIRating { get; set; }
    public bool? ShowLicense { get; set; }
    public int? MaxDriversShown { get; set; }
    public ColorConfig? PlayerHighlightColor { get; set; }

    // --- Session Info overlay ---
    public bool? ShowWeather { get; set; }
    public bool? ShowDelta { get; set; }
    public bool? ShowGameTime { get; set; }
    public bool? Use12HourClock { get; set; }
    public TemperatureUnit? TemperatureUnit { get; set; }

    // --- Delta Bar overlay ---
    public float? DeltaBarMaxSeconds { get; set; }
    public ColorConfig? FasterColor { get; set; }
    public ColorConfig? SlowerColor { get; set; }
    public bool? ShowTrendArrow { get; set; }
    public bool? ShowDeltaText { get; set; }

    // --- Standings overlay ---
    public StandingsDisplayMode? StandingsDisplayMode { get; set; }
    public bool? ShowClassBadge      { get; set; }
    public bool? ShowBestLap         { get; set; }
    public bool? ShowLastLap         { get; set; }
    public bool? ShowInterval        { get; set; }
    public bool? ShowStint           { get; set; }
    public bool? ShowPositionsGained { get; set; }
    public bool? ShowTeam            { get; set; }
    public bool? ShowPitTime         { get; set; }
    public int?  MaxStandingsRows    { get; set; }

    // --- Fuel Calculator overlay ---
    public FuelUnit? FuelUnit { get; set; }
    public float? FuelSafetyMarginLaps { get; set; }
    public bool? ShowFuelMargin { get; set; }

    // --- Pit Helper overlay ---
    public bool? ShowPitServices { get; set; }
    public bool? ShowNextPitEstimate { get; set; }

    // --- Weather overlay ---
    public bool? ShowHumidity { get; set; }
    public bool? ShowWind { get; set; }
    public WindSpeedUnit? WindSpeedUnit { get; set; }

    // --- Flat Track Map overlay ---
    public FlatMapLabelMode? FlatMapLabelMode { get; set; }
    public float? PlayerMarkerSize { get; set; }
    public float? CarMarkerSize { get; set; }
    public bool? ShowPitCars { get; set; }

    // --- Input Telemetry overlay ---
    public bool? ShowThrottle { get; set; }
    public bool? ShowBrake { get; set; }
    public bool? ShowClutch { get; set; }
    public bool? ShowInputTrace { get; set; }
    public bool? ShowGearSpeed { get; set; }
    public SpeedUnit? SpeedUnit { get; set; }
    public ColorConfig? ThrottleColor { get; set; }
    public ColorConfig? BrakeColor { get; set; }
    public ColorConfig? ClutchColor { get; set; }
}
