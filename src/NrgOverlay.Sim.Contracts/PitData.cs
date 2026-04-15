namespace NrgOverlay.Sim.Contracts;

/// <summary>
/// Pit-related telemetry, published at 10 Hz while connected.
/// </summary>
public sealed record PitData(
    bool             IsOnPitRoad,
    bool             IsInPitStall,
    float            PitLimiterSpeedMps,  // track pit road speed limit
    float            CurrentSpeedMps,
    bool             PitLimiterActive,
    int              PitStopCount,        // stops completed this session
    PitServiceFlags  RequestedService,    // flags currently set in the pit menu
    float            FuelToAddLiters      // fuel amount entered in pit menu
);

