using System.Text.Json;

namespace SimOverlay.Core.Config;

public sealed class OverlayConfig
{
    private static readonly JsonSerializerOptions CloneOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Returns an independent deep copy. All reference-type fields (ColorConfig,
    /// StreamOverrideConfig) are new instances — mutating the clone cannot affect
    /// the original.
    /// </summary>
    public OverlayConfig DeepClone() =>
        JsonSerializer.Deserialize<OverlayConfig>(
            JsonSerializer.Serialize(this, CloneOptions), CloneOptions)!;

    public string Id { get; set; } = "";
    public bool Enabled { get; set; } = true;

    // --- Position (never overridable) ---
    public int X { get; set; }
    public int Y { get; set; }

    // --- Size ---
    public int Width { get; set; } = 300;
    public int Height { get; set; } = 200;

    // --- Appearance ---
    public float Opacity { get; set; } = 1f;
    public ColorConfig BackgroundColor { get; set; } = ColorConfig.DarkBackground;
    public ColorConfig TextColor { get; set; } = ColorConfig.White;
    public float FontSize { get; set; } = 13f;

    // --- Relative overlay ---
    public bool ShowIRating { get; set; } = true;
    public bool ShowLicense { get; set; } = true;
    public int MaxDriversShown { get; set; } = 15;
    public ColorConfig PlayerHighlightColor { get; set; } = ColorConfig.PlayerHighlight;

    // --- Session Info overlay ---
    public bool ShowWeather { get; set; } = true;
    public bool ShowDelta { get; set; } = true;
    public bool ShowGameTime { get; set; } = true;
    public bool Use12HourClock { get; set; }
    public TemperatureUnit TemperatureUnit { get; set; } = TemperatureUnit.Celsius;

    // --- Delta Bar overlay ---
    public float DeltaBarMaxSeconds { get; set; } = 2f;
    public ColorConfig FasterColor { get; set; } = ColorConfig.Green;
    public ColorConfig SlowerColor { get; set; } = ColorConfig.Red;
    public bool ShowTrendArrow { get; set; } = true;
    public bool ShowDeltaText { get; set; } = true;

    // --- Standings overlay ---
    public StandingsDisplayMode StandingsDisplayMode { get; set; } = StandingsDisplayMode.Combined;
    public bool ShowClassBadge { get; set; } = true;
    public bool ShowBestLap { get; set; } = true;
    public int MaxStandingsRows { get; set; } = 30;

    // --- Fuel Calculator overlay ---
    public FuelUnit FuelUnit { get; set; } = FuelUnit.Liters;
    public float FuelSafetyMarginLaps { get; set; } = 1.0f;
    public bool ShowFuelMargin { get; set; } = true;

    // --- Input Telemetry overlay ---
    public bool ShowThrottle { get; set; } = true;
    public bool ShowBrake { get; set; } = true;
    public bool ShowClutch { get; set; } = true;
    public bool ShowInputTrace { get; set; } = true;
    public bool ShowGearSpeed { get; set; } = true;
    public SpeedUnit SpeedUnit { get; set; } = SpeedUnit.Kph;
    public ColorConfig ThrottleColor { get; set; } = ColorConfig.Green;
    public ColorConfig BrakeColor { get; set; } = ColorConfig.Red;
    public ColorConfig ClutchColor { get; set; } = ColorConfig.Blue;

    // --- Pit Helper overlay ---
    public bool ShowPitServices { get; set; } = true;
    public bool ShowNextPitEstimate { get; set; } = true;

    // --- Weather overlay ---
    public bool ShowHumidity { get; set; } = true;
    public bool ShowWind { get; set; } = true;
    public WindSpeedUnit WindSpeedUnit { get; set; } = WindSpeedUnit.Kph;

    // --- Flat Track Map overlay ---
    public FlatMapLabelMode FlatMapLabelMode { get; set; } = FlatMapLabelMode.CarNumber;
    public float PlayerMarkerSize { get; set; } = 8f;
    public float CarMarkerSize { get; set; } = 4f;
    public bool ShowPitCars { get; set; } = true;

    // --- Stream override ---
    public StreamOverrideConfig? StreamOverride { get; set; }

    /// <summary>
    /// Returns the effective config for the given mode.
    /// When stream mode is active and the override is enabled, each field is taken
    /// from the override if set (non-null), otherwise from this base config.
    /// X/Y are always taken from this base config regardless of mode.
    /// </summary>
    public OverlayConfig Resolve(bool streamModeActive)
    {
        if (!streamModeActive || StreamOverride is not { Enabled: true })
            return this;

        var o = StreamOverride;
        return new OverlayConfig
        {
            Id = Id,
            Enabled = Enabled,
            X = X,
            Y = Y,
            Width = o.Width ?? Width,
            Height = o.Height ?? Height,
            Opacity = o.Opacity ?? Opacity,
            BackgroundColor = o.BackgroundColor ?? BackgroundColor,
            TextColor = o.TextColor ?? TextColor,
            FontSize = o.FontSize ?? FontSize,
            ShowIRating = o.ShowIRating ?? ShowIRating,
            ShowLicense = o.ShowLicense ?? ShowLicense,
            MaxDriversShown = o.MaxDriversShown ?? MaxDriversShown,
            PlayerHighlightColor = o.PlayerHighlightColor ?? PlayerHighlightColor,
            ShowWeather = o.ShowWeather ?? ShowWeather,
            ShowDelta = o.ShowDelta ?? ShowDelta,
            ShowGameTime = o.ShowGameTime ?? ShowGameTime,
            Use12HourClock = o.Use12HourClock ?? Use12HourClock,
            TemperatureUnit = o.TemperatureUnit ?? TemperatureUnit,
            DeltaBarMaxSeconds = o.DeltaBarMaxSeconds ?? DeltaBarMaxSeconds,
            FasterColor = o.FasterColor ?? FasterColor,
            SlowerColor = o.SlowerColor ?? SlowerColor,
            ShowTrendArrow = o.ShowTrendArrow ?? ShowTrendArrow,
            ShowDeltaText = o.ShowDeltaText ?? ShowDeltaText,
            StandingsDisplayMode = o.StandingsDisplayMode ?? StandingsDisplayMode,
            ShowClassBadge = o.ShowClassBadge ?? ShowClassBadge,
            ShowBestLap = o.ShowBestLap ?? ShowBestLap,
            MaxStandingsRows = o.MaxStandingsRows ?? MaxStandingsRows,
            FuelUnit = o.FuelUnit ?? FuelUnit,
            FuelSafetyMarginLaps = o.FuelSafetyMarginLaps ?? FuelSafetyMarginLaps,
            ShowFuelMargin = o.ShowFuelMargin ?? ShowFuelMargin,
            ShowThrottle = o.ShowThrottle ?? ShowThrottle,
            ShowBrake = o.ShowBrake ?? ShowBrake,
            ShowClutch = o.ShowClutch ?? ShowClutch,
            ShowInputTrace = o.ShowInputTrace ?? ShowInputTrace,
            ShowGearSpeed = o.ShowGearSpeed ?? ShowGearSpeed,
            SpeedUnit = o.SpeedUnit ?? SpeedUnit,
            ThrottleColor = o.ThrottleColor ?? ThrottleColor,
            BrakeColor = o.BrakeColor ?? BrakeColor,
            ClutchColor = o.ClutchColor ?? ClutchColor,
            ShowPitServices = o.ShowPitServices ?? ShowPitServices,
            ShowNextPitEstimate = o.ShowNextPitEstimate ?? ShowNextPitEstimate,
            ShowHumidity = o.ShowHumidity ?? ShowHumidity,
            ShowWind = o.ShowWind ?? ShowWind,
            WindSpeedUnit = o.WindSpeedUnit ?? WindSpeedUnit,
            FlatMapLabelMode = o.FlatMapLabelMode ?? FlatMapLabelMode,
            PlayerMarkerSize = o.PlayerMarkerSize ?? PlayerMarkerSize,
            CarMarkerSize = o.CarMarkerSize ?? CarMarkerSize,
            ShowPitCars = o.ShowPitCars ?? ShowPitCars,
            StreamOverride = null, // Resolved config is a snapshot; no shared mutable refs
        };
    }
}
