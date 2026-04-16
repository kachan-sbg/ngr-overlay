namespace NrgOverlay.Sim.Contracts;

public sealed class SessionData
{
    public string TrackName { get; init; } = "";
    public SessionType SessionType { get; init; }
    // Configured session duration from session definition (0 = unlimited / unavailable).
    public TimeSpan SessionTimeLimit { get; init; }
    // Snapshot remaining at decode time (for live countdown prefer DriverData.SessionTimeRemaining).
    public TimeSpan SessionTimeRemaining { get; init; }
    public TimeSpan SessionTimeElapsed { get; init; }
    public TimeSpan SessionBestLapTime { get; init; }
    public int TotalLaps { get; init; }          // 0 = time-based session
    public float AirTempC { get; init; }
    public float TrackTempC { get; init; }
    public TimeOnly? GameTimeOfDay { get; init; } // sim world clock; null = not available from this sim

    // в”Ђв”Ђ Weather (populated from telemetry; 0 = unavailable / not yet received) в”Ђв”Ђ
    public float RelativeHumidity    { get; init; } // 0вЂ“1 (multiply by 100 for %)
    public bool  WeatherDeclaredWet  { get; init; } // true if stewards declared track wet
    /// <summary>
    /// iRacing TrackWetness enum: 0=unknown, 1=dry, 2=mostly dry, 3=very lightly wet,
    /// 4=lightly wet, 5=moderately wet, 6=very wet, 7=extremely wet.
    /// </summary>
    public int TrackWetness { get; init; }

    // в”Ђв”Ђ Car classes в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    /// <summary>
    /// All car classes present in the session. Empty in single-class sessions
    /// (overlays should treat an empty list the same as one class).
    /// </summary>
    public IReadOnlyList<CarClassInfo> CarClasses { get; init; } = [];

    // Field composition metrics.
    public int PlayerCountOverall { get; init; }         // active non-spectator/non-pace drivers
    public int PlayerCountInClass { get; init; }         // player's class in multi-class; same as overall in single-class
    public int StrengthOfFieldOverall { get; init; }     // iRating-based SOF approximation
    public int StrengthOfFieldInClass { get; init; }     // player's class SOF in multi-class; same as overall in single-class

    // Incident rule thresholds (0 = unavailable from the sim/session rules).
    public int IncidentDriveThroughLimit { get; init; }
    public int IncidentDisqualificationLimit { get; init; }
}

