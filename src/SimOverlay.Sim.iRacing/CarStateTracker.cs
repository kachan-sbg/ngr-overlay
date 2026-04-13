namespace SimOverlay.Sim.iRacing;

/// <summary>
/// Maintains stateful per-car data that cannot be derived from a single telemetry snapshot:
/// out-lap detection, stint lap counts, and positions-gained vs session start.
/// One instance lives in <see cref="IRacingPoller"/> and is updated at the same 10 Hz
/// cadence as the relative/standings publish.
/// </summary>
internal sealed class CarStateTracker
{
    private const int MaxCars = 64;

    private readonly int[]  _prevPitStopCounts  = new int[MaxCars];
    private readonly int[]  _lapAtLastPit        = new int[MaxCars];
    private readonly bool[] _isOnOutLap          = new bool[MaxCars];
    private readonly int[]  _startPositions      = new int[MaxCars];
    private bool _startPositionsCaptured;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>Called on session connect/reconnect to clear all stateful data.</summary>
    public void Reset()
    {
        Array.Clear(_prevPitStopCounts,  0, MaxCars);
        Array.Clear(_lapAtLastPit,       0, MaxCars);
        Array.Clear(_isOnOutLap,         0, MaxCars);
        Array.Clear(_startPositions,     0, MaxCars);
        _startPositionsCaptured = false;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called once per 10 Hz publish cycle before <see cref="IRacingRelativeCalculator.Compute"/>.
    /// </summary>
    public void Update(TelemetrySnapshot snapshot)
    {
        // Capture starting positions the first time we see valid position data.
        // In a race this corresponds roughly to the grid; in practice/qualy it
        // is just "first observed position" which is fine for the delta display.
        if (!_startPositionsCaptured)
        {
            bool anyValid = false;
            for (int i = 0; i < MaxCars; i++)
            {
                if (snapshot.Positions[i] > 0)
                {
                    _startPositions[i]  = snapshot.Positions[i];
                    anyValid            = true;
                }
            }
            if (anyValid) _startPositionsCaptured = true;
        }

        // Detect pit exits: pit-stop count just increased → mark car as on out-lap.
        // Clear out-lap flag once the car completes one lap after the pit exit.
        for (int i = 0; i < MaxCars; i++)
        {
            var pitCount = snapshot.PitStopCounts[i];
            if (pitCount > _prevPitStopCounts[i])
            {
                _isOnOutLap[i]        = true;
                _lapAtLastPit[i]      = snapshot.Laps[i];
                _prevPitStopCounts[i] = pitCount;
            }
            else if (_isOnOutLap[i] && snapshot.Laps[i] > _lapAtLastPit[i])
            {
                _isOnOutLap[i] = false;
            }
        }
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public bool IsOnOutLap(int carIdx) =>
        (uint)carIdx < MaxCars && _isOnOutLap[carIdx];

    /// <summary>
    /// Laps completed in the current stint (laps since last pit stop, or since
    /// session start if no pit stop has occurred).
    /// </summary>
    public int GetStintLaps(TelemetrySnapshot snapshot, int carIdx)
    {
        if ((uint)carIdx >= MaxCars) return 0;
        return Math.Max(0, snapshot.Laps[carIdx] - _lapAtLastPit[carIdx]);
    }

    /// <summary>
    /// Signed positions gained vs. starting grid: positive = moved forward.
    /// Returns 0 if the starting position is not yet known.
    /// </summary>
    public int GetPositionsGained(int carIdx, int currentPosition)
    {
        if (!_startPositionsCaptured) return 0;
        if ((uint)carIdx >= MaxCars)  return 0;
        if (_startPositions[carIdx] == 0 || currentPosition == 0) return 0;
        return _startPositions[carIdx] - currentPosition;
    }
}
