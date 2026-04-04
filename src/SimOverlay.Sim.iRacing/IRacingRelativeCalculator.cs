using SimOverlay.Sim.Contracts;

namespace SimOverlay.Sim.iRacing;

/// <summary>
/// Pure static calculator that converts a <see cref="TelemetrySnapshot"/> +
/// cached driver list into a sorted <see cref="RelativeData"/>.
/// No SDK dependency — fully unit-testable.
/// </summary>
internal static class IRacingRelativeCalculator
{
    /// <summary>
    /// Number of entries to return (player + N/2 ahead + N/2 behind).
    /// Odd numbers work fine: the half-way split is floored for "ahead".
    /// </summary>
    private const int MaxEntries = 15;

    /// <summary>
    /// Computes the relative display list.
    /// </summary>
    /// <param name="snapshot">Live telemetry snapshot.</param>
    /// <param name="drivers">Driver list from the latest session YAML.</param>
    public static RelativeData Compute(
        TelemetrySnapshot snapshot,
        IReadOnlyList<DriverSnapshot> drivers)
    {
        var playerPct   = snapshot.LapDistPcts[snapshot.PlayerCarIdx];
        var playerLap   = snapshot.Laps[snapshot.PlayerCarIdx];
        var estLapTime  = snapshot.EstimatedLapTime;

        // Build O(1) lookup: CarIdx → DriverSnapshot
        var driverByIdx = new Dictionary<int, DriverSnapshot>(drivers.Count);
        foreach (var d in drivers)
            driverByIdx[d.CarIdx] = d;

        var candidates = new List<(float Gap, RelativeEntry Entry)>(64);

        for (int i = 0; i < snapshot.LapDistPcts.Length; i++)
        {
            var pct = snapshot.LapDistPcts[i];
            if (pct < 0f) continue;                 // car not on track

            driverByIdx.TryGetValue(i, out var driver);
            if (driver is { IsSpectator: true } or { IsPaceCar: true }) continue;

            // Normalise delta to [-0.5, 0.5] (wrap at start/finish line)
            var delta = pct - playerPct;
            if (delta >  0.5f) delta -= 1f;
            if (delta < -0.5f) delta += 1f;

            var gapSeconds = -delta * estLapTime;  // negative = ahead of player
            var lapDiff    = snapshot.Laps[i] - playerLap;

            candidates.Add((gapSeconds, new RelativeEntry
            {
                Position           = snapshot.Positions[i],
                CarNumber          = driver?.CarNumber  ?? i.ToString(),
                DriverName         = driver?.UserName   ?? string.Empty,
                IRating            = driver?.IRating    ?? 0,
                LicenseClass       = driver?.LicenseClass ?? LicenseClass.R,
                LicenseLevel       = driver?.LicenseLevel ?? "R 0.00",
                GapToPlayerSeconds = gapSeconds,
                LapDifference      = lapDiff,
                IsPlayer           = i == snapshot.PlayerCarIdx,
            }));
        }

        // Sort: negative (ahead) first → player → positive (behind)
        candidates.Sort((a, b) => a.Gap.CompareTo(b.Gap));

        // Select a window of MaxEntries centred on the player
        int playerIdx = candidates.FindIndex(c => c.Entry.IsPlayer);
        if (playerIdx < 0)
        {
            // Player not on track — return whatever we have, up to MaxEntries
            return new RelativeData
            {
                Entries = candidates.Take(MaxEntries).Select(c => c.Entry).ToList()
            };
        }

        int half  = MaxEntries / 2;
        int start = Math.Max(0, playerIdx - half);
        int end   = Math.Min(candidates.Count, start + MaxEntries);

        // If not enough entries below, shift window up
        if (end - start < MaxEntries)
            start = Math.Max(0, end - MaxEntries);

        return new RelativeData
        {
            Entries = candidates
                .Skip(start)
                .Take(end - start)
                .Select(c => c.Entry)
                .ToList()
        };
    }
}
