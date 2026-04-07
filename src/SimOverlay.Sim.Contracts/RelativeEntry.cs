using SimOverlay.Core.Config;

namespace SimOverlay.Sim.Contracts;

public sealed class RelativeEntry
{
    public int Position { get; init; }
    public string CarNumber { get; init; } = "";
    public string DriverName { get; init; } = "";
    public int IRating { get; init; }
    public LicenseClass LicenseClass { get; init; }
    public string LicenseLevel { get; init; } = ""; // e.g., "B 3.45"
    public float GapToPlayerSeconds { get; init; }  // negative = ahead of player
    public int LapDifference { get; init; }          // 0 = same lap, +1 = one lap ahead
    public bool IsPlayer { get; init; }

    // ── Multi-class fields ────────────────────────────────────────────────────
    /// <summary>Short class name, e.g. "GTP", "GT3". Empty string in single-class sessions.</summary>
    public string CarClass { get; init; } = "";
    /// <summary>Position within the car's class. Equal to <see cref="Position"/> in single-class sessions.</summary>
    public int ClassPosition { get; init; }
    /// <summary>Display colour for the car's class. White in single-class sessions.</summary>
    public ColorConfig ClassColor { get; init; } = ColorConfig.White;
}
