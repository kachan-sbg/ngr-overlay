namespace SimOverlay.Core.Config;

public sealed class OverlayConfig
{
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
            StreamOverride = StreamOverride,
        };
    }
}
