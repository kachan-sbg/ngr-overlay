namespace NrgOverlay.Core.Config;

public sealed class GlobalSettings
{
    public bool StreamModeActive { get; set; }
    public bool StartWithWindows { get; set; }

    /// <summary>
    /// Ordered list of sim provider IDs to try when multiple sims could be active.
    /// The first running sim in this list wins. Default: iRacing only.
    /// </summary>
    public List<string> SimPriorityOrder { get; set; } = ["iRacing"];

    /// <summary>
    /// Fallback colour palette for car classes when the sim does not provide class colours
    /// (e.g. future sims without built-in class colour data). Colours are applied in order;
    /// if there are more classes than palette entries the palette wraps around.
    /// Populated by config migration v2в†’v3 with four distinct defaults.
    /// </summary>
    public List<ColorConfig> ClassColorPalette { get; set; } = [];

    /// <summary>
    /// Manual per-driver country overrides keyed by iRacing UserID.
    /// Values are expected to be ISO 3166-1 alpha-2 codes (e.g. "DE", "US").
    /// </summary>
    public Dictionary<int, string> DriverCountryOverrides { get; set; } = [];

    /// <summary>
    /// Optional per-user cache keyed by iRacing UserID.
    /// Values can be ISO 3166-1 alpha-2 or alpha-3 codes.
    /// </summary>
    public Dictionary<int, string> DriverCountryCache { get; set; } = [];

    /// <summary>
    /// Optional mapping from iRacing FlairID to ISO 3166-1 alpha-3 country code.
    /// Loaded from local flair mapping JSON and used as text fallback when ISO2 is unavailable.
    /// </summary>
    public Dictionary<int, string> DriverCountryByFlairId { get; set; } = [];

    /// <summary>
    /// Optional mapping from iRacing FlairID to ISO 3166-1 alpha-2 country code for emoji rendering.
    /// </summary>
    public Dictionary<int, string> DriverCountryIso2ByFlairId { get; set; } = [];
}

