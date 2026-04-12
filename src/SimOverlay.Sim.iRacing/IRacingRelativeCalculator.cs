using SimOverlay.Core.Config;
using SimOverlay.Sim.Contracts;

namespace SimOverlay.Sim.iRacing;

/// <summary>
/// Pure static calculator that converts a <see cref="TelemetrySnapshot"/> +
/// cached driver list into a sorted <see cref="RelativeData"/> and <see cref="StandingsData"/>.
/// No SDK dependency — fully unit-testable.
/// </summary>
internal static class IRacingRelativeCalculator
{
    /// <summary>
    /// Number of entries to return (player + N/2 ahead + N/2 behind).
    /// Odd numbers work fine: the half-way split is floored for "ahead".
    /// </summary>
    private const int MaxEntries = 15;

    // Intermediate record used between the two passes.
    private sealed record CarCandidate(
        int          CarIdx,
        float        Gap,
        int          OverallPosition,
        int          LapDiff,
        DriverSnapshot? Driver);

    /// <summary>
    /// Computes the relative display list and full-field standings.
    /// </summary>
    /// <param name="snapshot">Live telemetry snapshot.</param>
    /// <param name="drivers">Driver list from the latest session YAML.</param>
    public static (RelativeData Relative, StandingsData Standings) Compute(
        TelemetrySnapshot snapshot,
        IReadOnlyList<DriverSnapshot> drivers)
    {
        var playerPct  = snapshot.LapDistPcts[snapshot.PlayerCarIdx];
        var playerLap  = snapshot.Laps[snapshot.PlayerCarIdx];
        var estLapTime = snapshot.EstimatedLapTime;

        // Build O(1) lookup: CarIdx → DriverSnapshot
        var driverByIdx = new Dictionary<int, DriverSnapshot>(drivers.Count);
        foreach (var d in drivers)
            driverByIdx[d.CarIdx] = d;

        // ── Pass 1: collect all on-track, non-spectator cars ─────────────────
        var allCars = new List<CarCandidate>(64);

        for (int i = 0; i < snapshot.LapDistPcts.Length; i++)
        {
            // CarIdxTrackSurface == -1 (NotInWorld) means the slot is unused or the
            // driver is registered but not spawned — skip to avoid ghost entries.
            if (i < snapshot.TrackSurfaces.Length && snapshot.TrackSurfaces[i] < 0) continue;

            var pct = snapshot.LapDistPcts[i];
            if (pct < 0f) continue;                 // car not on track (secondary guard)

            driverByIdx.TryGetValue(i, out var driver);
            if (driver is { IsSpectator: true } or { IsPaceCar: true }) continue;

            // Normalise delta to [-0.5, 0.5] (wrap at start/finish line)
            var delta = pct - playerPct;
            if (delta >  0.5f) delta -= 1f;
            if (delta < -0.5f) delta += 1f;

            var gapSeconds = -delta * estLapTime;  // negative = ahead of player
            var lapDiff    = snapshot.Laps[i] - playerLap;

            allCars.Add(new CarCandidate(i, gapSeconds, snapshot.Positions[i], lapDiff, driver));
        }

        // ── Pass 2: compute per-class positions from overall race positions ───
        var classPositionByCarIdx = new Dictionary<int, int>(allCars.Count);
        foreach (var group in allCars.GroupBy(c => c.Driver?.CarClassId ?? 0))
        {
            var sorted = group
                .OrderBy(c => c.OverallPosition == 0 ? int.MaxValue : c.OverallPosition)
                .ToList();
            for (int rank = 0; rank < sorted.Count; rank++)
                classPositionByCarIdx[sorted[rank].CarIdx] = rank + 1;
        }

        // Detect single-class: only one distinct ClassId among on-track cars.
        var distinctClasses = allCars
            .Select(c => c.Driver?.CarClassId ?? 0)
            .Distinct()
            .Count();
        var isMultiClass = distinctClasses > 1;

        // ── Build candidates list ─────────────────────────────────────────────
        var candidates = new List<(float Gap, int CarIdx, RelativeEntry Entry)>(allCars.Count);
        foreach (var car in allCars)
        {
            var driver        = car.Driver;
            var classPosition = classPositionByCarIdx.TryGetValue(car.CarIdx, out var cp) ? cp : car.OverallPosition;

            var lastLapSec = car.CarIdx < snapshot.LastLapTimes.Length
                ? snapshot.LastLapTimes[car.CarIdx]
                : 0f;

            candidates.Add((car.Gap, car.CarIdx, new RelativeEntry
            {
                Position           = car.OverallPosition,
                CarNumber          = driver?.CarNumber   ?? car.CarIdx.ToString(),
                DriverName         = driver?.UserName    ?? string.Empty,
                IRating            = driver?.IRating     ?? 0,
                LicenseClass       = driver?.LicenseClass ?? LicenseClass.R,
                LicenseLevel       = driver?.LicenseLevel ?? "R 0.00",
                GapToPlayerSeconds = car.Gap,
                LapDifference      = car.LapDiff,
                LastLapTime        = lastLapSec > 0f ? TimeSpan.FromSeconds(lastLapSec) : TimeSpan.Zero,
                IsPlayer           = car.CarIdx == snapshot.PlayerCarIdx,
                CarClass           = isMultiClass ? (driver?.CarClass ?? "") : "",
                ClassPosition      = classPosition,
                ClassColor         = isMultiClass ? (driver?.ClassColor ?? ColorConfig.White) : ColorConfig.White,
            }));
        }

        // Sort by gap: negative (ahead) first → player → positive (behind)
        candidates.Sort((a, b) => a.Gap.CompareTo(b.Gap));

        // ── Build standings (all cars sorted by overall position) ─────────────
        var standings = BuildStandings(candidates, snapshot, isMultiClass);

        // ── Select relative window ─────────────────────────────────────────────
        int playerIdx = candidates.FindIndex(c => c.Entry.IsPlayer);
        RelativeData relative;

        if (playerIdx < 0)
        {
            relative = new RelativeData
            {
                Entries = candidates.Take(MaxEntries).Select(c => c.Entry).ToList()
            };
        }
        else
        {
            int half  = MaxEntries / 2;
            int start = Math.Max(0, playerIdx - half);
            int end   = Math.Min(candidates.Count, start + MaxEntries);
            if (end - start < MaxEntries)
                start = Math.Max(0, end - MaxEntries);

            relative = new RelativeData
            {
                Entries = candidates
                    .Skip(start)
                    .Take(end - start)
                    .Select(c => c.Entry)
                    .ToList()
            };
        }

        return (relative, standings);
    }

    private static StandingsData BuildStandings(
        List<(float Gap, int CarIdx, RelativeEntry Entry)> candidates,
        TelemetrySnapshot snapshot,
        bool isMultiClass)
    {
        // Sort by overall race position; unpositioned cars (pos=0) go to the end.
        var sorted = candidates
            .OrderBy(c => c.Entry.Position == 0 ? int.MaxValue : c.Entry.Position)
            .ToList();

        if (sorted.Count == 0) return new StandingsData();

        // ── Gap-to-leader via cumulative progress ─────────────────────────────
        // GapToPlayerSeconds (relative gap) breaks down when the race leader has
        // lapped the player: they appear just behind on track but ahead in overall
        // position, producing a positive GapToPlayerSeconds that makes the math wrong.
        //
        // Correct approach: compare total "race progress" = laps_completed + pct.
        // Progress difference × estimated lap time = gap in seconds.
        // For lapped cars the integer part of the difference gives the lap count.

        int   leaderCarIdx  = sorted[0].CarIdx;
        float leaderPct     = snapshot.LapDistPcts[leaderCarIdx];
        if (leaderPct < 0f) leaderPct = 0f;
        float leaderProgress = snapshot.Laps[leaderCarIdx] + leaderPct;

        var entries = new List<StandingsEntry>(sorted.Count);
        foreach (var (_, carIdx, rel) in sorted)
        {
            bool isLeader = entries.Count == 0;

            float carPct = snapshot.LapDistPcts[carIdx];
            if (carPct < 0f) carPct = 0f;
            float carProgress   = snapshot.Laps[carIdx] + carPct;
            float lapsBehind    = leaderProgress - carProgress;   // ≥ 0

            // Explicitly zero the leader; clamp all others to ≥ 0.001 so only one
            // entry ever gets gap == 0f (which FormatGap maps to "LEADER").
            float gapToLeader   = isLeader ? 0f : Math.Max(0.001f, lapsBehind * snapshot.EstimatedLapTime);
            int   lapDiffLeader = isLeader ? 0  : (int)Math.Max(0, Math.Floor(lapsBehind));

            float bestLapSec = carIdx < snapshot.BestLapTimes.Length
                ? snapshot.BestLapTimes[carIdx]
                : 0f;

            entries.Add(new StandingsEntry
            {
                Position           = rel.Position,
                ClassPosition      = rel.ClassPosition,
                CarNumber          = rel.CarNumber,
                DriverName         = rel.DriverName,
                IRating            = rel.IRating,
                CarClass           = isMultiClass ? rel.CarClass : "",
                ClassColor         = rel.ClassColor,
                GapToLeaderSeconds = gapToLeader,
                LapDifference      = lapDiffLeader,
                BestLapTime        = bestLapSec > 0 ? TimeSpan.FromSeconds(bestLapSec) : TimeSpan.Zero,
                IsPlayer           = rel.IsPlayer,
            });
        }

        return new StandingsData { Entries = entries };
    }
}
