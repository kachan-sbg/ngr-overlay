using NrgOverlay.Core.Config;

namespace NrgOverlay.Sim.Contracts;

/// <summary>
/// A single row in the full-field standings.
/// Published as part of <see cref="StandingsData"/> at ~10 Hz.
/// </summary>
public sealed class StandingsEntry
{
    public int Position { get; init; }                   // overall race position
    public int ClassPosition { get; init; }              // position within class
    public string CarNumber { get; init; } = "";
    public string DriverName { get; init; } = "";
    public int IRating { get; init; }                    // 0 = unavailable (LMU)
    public LicenseClass LicenseClass { get; init; }
    public string LicenseLevel { get; init; } = "";      // e.g., "B 3.45"
    public string CarClass { get; init; } = "";          // e.g. "GTP". Empty in single-class
    public ColorConfig ClassColor { get; init; } = ColorConfig.White;
    public float GapToLeaderSeconds { get; init; }       // gap to P1; 0 for leader
    public float Interval { get; init; }                 // gap to car directly ahead; 0 for leader
    public int LapDifference { get; init; }              // laps behind leader
    public TimeSpan BestLapTime { get; init; }           // Zero = no lap set yet
    public TimeSpan LastLapTime { get; init; }           // Zero = no lap completed yet
    public bool IsPlayer { get; init; }

    // в”Ђв”Ђ Extra fields в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    /// <summary>iRacing club/country name, e.g. "Germany", "USA - Southeast".</summary>
    public string ClubName { get; init; } = "";
    /// <summary>Resolved ISO 3166-1 alpha-2 country code (e.g. "DE"). Empty when unknown.</summary>
    public string CountryCode { get; init; } = "";
    /// <summary>iRacing flair identifier for debug/fallback country rendering.</summary>
    public int FlairId { get; init; }
    /// <summary>True when driver has active timing context (on track or has lap timing data).</summary>
    public bool HasTimingData { get; init; } = true;
    /// <summary>Team name. Empty for solo drivers.</summary>
    public string TeamName { get; init; } = "";
    /// <summary>Full car display name, e.g. "Porsche 911 GT3 R (992)".</summary>
    public string CarScreenName { get; init; } = "";
    /// <summary>Car is currently on pit road.</summary>
    public bool IsOnPitRoad { get; init; }
    /// <summary>Car just exited pits and has not completed a full lap since the last pit stop.</summary>
    public bool IsOutLap { get; init; }
    /// <summary>Tire compound index. 0 = unavailable/not applicable.</summary>
    public int TireCompound { get; init; }
    /// <summary>Laps completed in the current stint (since last pit stop, or since session start).</summary>
    public int StintLaps { get; init; }
    /// <summary>Positions gained vs. starting grid. Positive = moved forward.</summary>
    public int PositionsGained { get; init; }
    /// <summary>Last pit-lane traversal time in seconds. 0 = no pit stop yet or unavailable.</summary>
    public float PitLaneTime { get; init; }
}

