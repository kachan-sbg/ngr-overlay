namespace NrgOverlay.Sim.Contracts;

/// <summary>
/// Per-car track position snapshot for rendering a flat (linear) track map.
/// Published at 10 Hz while connected. Spectators and pace cars excluded.
/// </summary>
public sealed record TrackMapData(
    float TrackLengthMeters,
    IReadOnlyList<TrackMapCarEntry> Cars
);

