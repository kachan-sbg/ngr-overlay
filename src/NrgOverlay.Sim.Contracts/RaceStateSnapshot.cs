namespace NrgOverlay.Sim.Contracts;

/// <summary>
/// Unified computed race state published by sim providers.
/// Widgets can subscribe once and use only the slices they need.
/// </summary>
public sealed class RaceStateSnapshot
{
    public long Version { get; init; }

    public SessionData? Session { get; init; }
    public DriverData? Driver { get; init; }
    public TelemetryData? Telemetry { get; init; }
    public RelativeData? Relative { get; init; }
    public StandingsData? Standings { get; init; }
    public PitData? Pit { get; init; }
    public TrackMapData? TrackMap { get; init; }
    public WeatherData? Weather { get; init; }
}

