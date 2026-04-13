namespace SimOverlay.Sim.iRacing;

/// <summary>
/// Immutable snapshot of per-car telemetry fields needed for relative computation.
/// Built by <see cref="IRacingPoller"/> from live <c>IRacingSdkData</c> and passed
/// to <see cref="IRacingRelativeCalculator.Compute"/> so the calculator has no
/// dependency on the SDK and is fully unit-testable.
/// </summary>
internal sealed record TelemetrySnapshot(
    int     PlayerCarIdx,
    float[] LapDistPcts,        // CarIdxLapDistPct[64]: 0–1; -1 = car not on track
    int[]   Positions,          // CarIdxPosition[64]
    int[]   Laps,               // CarIdxLap[64]
    float   EstimatedLapTime,   // seconds; used to convert lap-pct delta → gap
    float[] BestLapTimes,       // CarIdxBestLapTime[64]: best lap in seconds; 0 = no lap
    float[] LastLapTimes,       // CarIdxLastLapTime[64]: last completed lap in seconds; 0 = no lap
    int[]   TrackSurfaces,      // CarIdxTrackSurface[64]: -1=NotInWorld, 0=OffTrack, 1=Pit, 2=ApproachingPits, 3=OnTrack
    bool[]  OnPitRoad,          // CarIdxOnPitRoad[64]
    float[] F2Times,            // CarIdxF2Time[64]: seconds behind leader (or 0 for leader)
    int[]   PitStopCounts,      // CarIdxNumPitStops[64]
    float[] PitLaneTimes,       // CarIdxLastPitLaneTimeAppro[64]: last pit-lane traversal seconds; 0 if none
    int[]   TireCompounds);     // CarIdxTireCompound[64]: compound index; 0 = unknown/unavailable
