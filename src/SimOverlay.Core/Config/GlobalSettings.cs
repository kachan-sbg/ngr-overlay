namespace SimOverlay.Core.Config;

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
    /// Populated by config migration v2→v3 with four distinct defaults.
    /// </summary>
    public List<ColorConfig> ClassColorPalette { get; set; } = [];
}
