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
    float   EstimatedLapTime);  // seconds; used to convert lap-pct delta → gap
