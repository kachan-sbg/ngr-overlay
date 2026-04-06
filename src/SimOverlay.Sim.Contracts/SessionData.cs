namespace SimOverlay.Sim.Contracts;

public sealed class SessionData
{
    public string TrackName { get; init; } = "";
    public SessionType SessionType { get; init; }
    public TimeSpan SessionTimeRemaining { get; init; }
    public TimeSpan SessionTimeElapsed { get; init; }
    public int TotalLaps { get; init; }          // 0 = time-based session
    public float AirTempC { get; init; }
    public float TrackTempC { get; init; }
    public TimeOnly GameTimeOfDay { get; init; } // sim world clock, not wall clock

    // ── Weather (populated from telemetry; 0 = unavailable / not yet received) ──
    public float RelativeHumidity    { get; init; } // 0–1 (multiply by 100 for %)
    public bool  WeatherDeclaredWet  { get; init; } // true if stewards declared track wet
    /// <summary>
    /// iRacing TrackWetness enum: 0=unknown, 1=dry, 2=mostly dry, 3=very lightly wet,
    /// 4=lightly wet, 5=moderately wet, 6=very wet, 7=extremely wet.
    /// </summary>
    public int TrackWetness { get; init; }
}
