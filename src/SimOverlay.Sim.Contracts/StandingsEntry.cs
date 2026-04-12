using SimOverlay.Core.Config;

namespace SimOverlay.Sim.Contracts;

/// <summary>
/// A single row in the full-field standings.
/// Published as part of <see cref="StandingsData"/> at ~10 Hz.
/// </summary>
public sealed class StandingsEntry
{
    public int Position { get; init; }                  // overall race position
    public int ClassPosition { get; init; }             // position within class
    public string CarNumber { get; init; } = "";
    public string DriverName { get; init; } = "";
    public int IRating { get; init; }                   // 0 = unavailable (LMU)
    public string CarClass { get; init; } = "";         // e.g. "GTP". Empty in single-class
    public ColorConfig ClassColor { get; init; } = ColorConfig.White;
    public float GapToLeaderSeconds { get; init; }      // gap to P1; 0 for leader
    public int LapDifference { get; init; }             // laps behind leader
    public TimeSpan BestLapTime { get; init; }          // Zero = no lap set yet
    public bool IsPlayer { get; init; }
}
