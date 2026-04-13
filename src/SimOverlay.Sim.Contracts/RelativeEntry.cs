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

    /// <summary>Last completed lap time. Zero = no lap yet or unavailable.</summary>
    public TimeSpan LastLapTime { get; init; }

    // ── Multi-class fields ────────────────────────────────────────────────────
    /// <summary>Short class name, e.g. "GTP", "GT3". Empty string in single-class sessions.</summary>
    public string CarClass { get; init; } = "";
    /// <summary>Position within the car's class. Equal to <see cref="Position"/> in single-class sessions.</summary>
    public int ClassPosition { get; init; }
    /// <summary>Display colour for the car's class. White in single-class sessions.</summary>
    public ColorConfig ClassColor { get; init; } = ColorConfig.White;

    // ── Extra fields ──────────────────────────────────────────────────────────
    /// <summary>iRacing club/country name, e.g. "Germany", "USA - Southeast".</summary>
    public string ClubName { get; init; } = "";
    /// <summary>Car is currently on pit road.</summary>
    public bool IsOnPitRoad { get; init; }
    /// <summary>Car just exited the pits and has not yet completed one full lap since the last pit stop.</summary>
    public bool IsOutLap { get; init; }
    /// <summary>
    /// Car is in the garage (irsdk_NotInWorld) — not yet spawned onto the track.
    /// <see cref="GapToPlayerSeconds"/> is a large sentinel value; display "GAR" instead of a numeric gap.
    /// </summary>
    public bool IsInGarage { get; init; }
    /// <summary>Tire compound index. 0 = unavailable/not applicable.</summary>
    public int TireCompound { get; init; }
}
