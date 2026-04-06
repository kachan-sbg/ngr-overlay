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
}
