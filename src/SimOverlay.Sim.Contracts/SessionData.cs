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
}
