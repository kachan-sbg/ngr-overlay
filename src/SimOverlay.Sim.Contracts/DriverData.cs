namespace SimOverlay.Sim.Contracts;

public sealed class DriverData
{
    public int Position { get; init; }
    public int Lap { get; init; }
    public TimeSpan LastLapTime { get; init; }
    public TimeSpan BestLapTime { get; init; }
    public float LapDeltaVsBestLap    { get; init; } // seconds; negative = faster than personal best
    public float LapDeltaVsSessionBest { get; init; } // seconds; negative = faster than session best; 0 = unavailable
}
