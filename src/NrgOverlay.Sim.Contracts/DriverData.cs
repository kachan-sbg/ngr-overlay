namespace NrgOverlay.Sim.Contracts;

/// <summary>
/// Player-specific data published at 60 Hz.
/// Live session timing fields are updated from telemetry every tick so the
/// display stays accurate regardless of how stale the YAML snapshot is.
/// </summary>
public sealed class DriverData
{
    public int Position { get; init; }
    public int Lap { get; init; }
    public TimeSpan LastLapTime { get; init; }
    public TimeSpan BestLapTime { get; init; }          // personal best this session

    /// <summary>
    /// Fastest lap set by any driver this session.
    /// <see cref="TimeSpan.Zero"/> = no lap completed yet.
    /// </summary>
    public TimeSpan SessionBestLapTime { get; init; }

    public float LapDeltaVsBestLap     { get; init; } // seconds; negative = faster than personal best
    public float LapDeltaVsSessionBest { get; init; } // seconds; negative = faster than session best

    // в”Ђв”Ђ Live session timing (60 Hz from telemetry, not YAML snapshot) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>Time elapsed in the current session. <see cref="TimeSpan.Zero"/> if unavailable.</summary>
    public TimeSpan SessionTimeElapsed { get; init; }

    /// <summary>
    /// Countdown remaining in the current session.
    /// <c>null</c> = laps-based session (no countdown applicable).
    /// </summary>
    public TimeSpan? SessionTimeRemaining { get; init; }

    /// <summary>
    /// Current in-game time of day, updated at 60 Hz.
    /// <c>null</c> = not available from this sim (e.g. LMU).
    /// Supersedes <see cref="SessionData.GameTimeOfDay"/> which is a stale snapshot.
    /// </summary>
    public TimeOnly? GameTimeOfDay { get; init; }
}

