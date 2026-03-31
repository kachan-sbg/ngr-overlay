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
}
