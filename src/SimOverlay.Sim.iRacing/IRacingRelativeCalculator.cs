using SimOverlay.Core.Config;
using SimOverlay.Sim.Contracts;

namespace SimOverlay.Sim.iRacing;

/// <summary>
/// Pure calculator that converts a <see cref="TelemetrySnapshot"/> +
/// cached driver list + <see cref="CarStateTracker"/> into a sorted
/// <see cref="RelativeData"/> and <see cref="StandingsData"/>.
/// No SDK dependency — fully unit-testable.
///
/// <para>
/// <b>Session modes</b>:
/// <list type="bullet">
///   <item><b>Race</b> (any <c>CarIdxPosition &gt; 0</c>): standings sorted by track position;
///     gaps expressed as time behind leader by track progress.</item>
///   <item><b>Practice / Qualify</b> (all positions = 0): standings sorted by best lap time;
///     gaps expressed as delta from leader's best lap.  All registered session drivers
///     are included even when in the garage (irsdk_NotInWorld, pct = -1), because the
///     session YAML has their data and omitting them makes the widget look empty.</item>
/// </list>
/// </para>
/// </summary>
internal static class IRacingRelativeCalculator
{
    // Large sentinel gap assigned to garage cars — sorts them to the bottom of the relative.
    private const float GarageGapSentinel = 99_999f;

    // Intermediate record used between the two passes.
    private sealed record CarCandidate(
        int             CarIdx,
        float           Gap,
        int             OverallPosition,
        int             LapDiff,
        DriverSnapshot? Driver,
        bool            IsGarage = false);

    /// <summary>
    /// Computes the relative display list and full-field standings.
    /// </summary>
    public static (RelativeData Relative, StandingsData Standings) Compute(
        TelemetrySnapshot            snapshot,
        IReadOnlyList<DriverSnapshot> drivers,
        CarStateTracker              carState)
    {
        var playerPct  = snapshot.LapDistPcts[snapshot.PlayerCarIdx];
        var playerLap  = snapshot.Laps[snapshot.PlayerCarIdx];
        var estLapTime = snapshot.EstimatedLapTime;

        // Build O(1) lookup: CarIdx → DriverSnapshot
        var driverByIdx = new Dictionary<int, DriverSnapshot>(drivers.Count);
        foreach (var d in drivers)
            driverByIdx[d.CarIdx] = d;

        // ── Determine whether the player is spawned on track ─────────────────
        var playerSurface    = snapshot.PlayerCarIdx < snapshot.TrackSurfaces.Length
            ? snapshot.TrackSurfaces[snapshot.PlayerCarIdx] : -1;
        var playerIsOnTrack  = playerSurface >= 0 && playerPct >= 0f;

        // ── Pass 1: collect candidates for the relative board ─────────────────
        // On-track cars (surface ≥ 0, pct ≥ 0) get a real gap.
        // Registered session drivers who are in the garage (irsdk_NotInWorld) get a
        // sentinel gap so they still appear at the bottom of the relative — the user
        // wants to see connected drivers even before they spawn, and the player's own
        // entry must always be present regardless of track status.
        var onTrackCars = new List<CarCandidate>(64);

        // Collect on-track car indices for the garage-pass exclusion check below.
        var onTrackSet = new HashSet<int>(64);

        for (int i = 0; i < snapshot.LapDistPcts.Length; i++)
        {
            var surface = i < snapshot.TrackSurfaces.Length ? snapshot.TrackSurfaces[i] : -1;
            var pct     = snapshot.LapDistPcts[i];

            if (surface < 0) continue;   // irsdk_NotInWorld (garage or empty slot)
            if (pct < 0f)   continue;    // iRacing uses -1.0 for unspawned cars

            driverByIdx.TryGetValue(i, out var driver);
            if (driver is { IsSpectator: true } or { IsPaceCar: true }) continue;

            onTrackSet.Add(i);

            float gap;
            if (playerIsOnTrack)
            {
                var delta = pct - playerPct;
                if (delta >  0.5f) delta -= 1f;
                if (delta < -0.5f) delta += 1f;
                gap = -delta * estLapTime;
            }
            else
            {
                // Player in garage — on-track cars appear "ahead" sorted by pct descending.
                // Use negative pct-based value so they sort before the player's 0.
                gap = -(pct * estLapTime);
            }

            onTrackCars.Add(new CarCandidate(
                CarIdx:          i,
                Gap:             gap,
                OverallPosition: snapshot.Positions[i],
                LapDiff:         snapshot.Laps[i] - playerLap,
                Driver:          driver,
                IsGarage:        false));
        }

        // Include the player's own car if they are in the garage.
        if (!playerIsOnTrack)
        {
            driverByIdx.TryGetValue(snapshot.PlayerCarIdx, out var playerDriver);
            if (playerDriver is not { IsSpectator: true } and not { IsPaceCar: true })
            {
                onTrackCars.Add(new CarCandidate(
                    CarIdx:          snapshot.PlayerCarIdx,
                    Gap:             0f,
                    OverallPosition: 0,
                    LapDiff:         0,
                    Driver:          playerDriver,
                    IsGarage:        true));
            }
        }

        // Include other registered garage drivers (connected, in the pits or setup bay).
        foreach (var driver in drivers)
        {
            if (driver.IsSpectator || driver.IsPaceCar) continue;
            if (driver.CarIdx == snapshot.PlayerCarIdx) continue; // already handled above
            if (onTrackSet.Contains(driver.CarIdx))     continue; // already in on-track pass

            onTrackCars.Add(new CarCandidate(
                CarIdx:          driver.CarIdx,
                Gap:             GarageGapSentinel,
                OverallPosition: 0,
                LapDiff:         0,
                Driver:          driver,
                IsGarage:        true));
        }

        // ── Determine session mode ────────────────────────────────────────────
        // In a race, iRacing populates CarIdxPosition (> 0) for every active car.
        // In practice/qualify all positions are 0 — use best lap time ordering.
        bool isRace = onTrackCars.Any(c => c.OverallPosition > 0);

        // ── Pass 2: per-class positions ───────────────────────────────────────
        // Class positions are computed across on-track cars only (for the relative).
        // Standings re-computes them across all session drivers below.
        var classPositionByCarIdx = new Dictionary<int, int>(onTrackCars.Count);
        foreach (var group in onTrackCars.GroupBy(c => c.Driver?.CarClassId ?? 0))
        {
            var sortedGroup = group
                .OrderBy(c => c.OverallPosition == 0 ? int.MaxValue : c.OverallPosition)
                .ToList();
            for (int rank = 0; rank < sortedGroup.Count; rank++)
                classPositionByCarIdx[sortedGroup[rank].CarIdx] = rank + 1;
        }

        var distinctOnTrackClasses = onTrackCars
            .Select(c => c.Driver?.CarClassId ?? 0)
            .Distinct()
            .Count();
        var isMultiClass = distinctOnTrackClasses > 1;

        // ── Build relative candidates (on-track only, sorted by gap) ─────────
        var candidates = new List<(float Gap, int CarIdx, RelativeEntry Entry)>(onTrackCars.Count);
        foreach (var car in onTrackCars)
        {
            var driver        = car.Driver;
            var classPosition = classPositionByCarIdx.TryGetValue(car.CarIdx, out var cp)
                ? cp : car.OverallPosition;
            var lastLapSec    = car.CarIdx < snapshot.LastLapTimes.Length
                ? snapshot.LastLapTimes[car.CarIdx] : 0f;
            var isOnPit  = car.CarIdx < snapshot.OnPitRoad.Length && snapshot.OnPitRoad[car.CarIdx];
            var isOutLap = carState.IsOnOutLap(car.CarIdx);
            var tire     = car.CarIdx < snapshot.TireCompounds.Length ? snapshot.TireCompounds[car.CarIdx] : 0;

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
                ClubName           = driver?.ClubName    ?? string.Empty,
                IsOnPitRoad        = isOnPit,
                IsOutLap           = isOutLap,
                IsInGarage         = car.IsGarage,
                TireCompound       = tire,
            }));
        }

        // Sort by gap: most-ahead first
        candidates.Sort((a, b) => a.Gap.CompareTo(b.Gap));

        // ── Standings: include ALL registered session drivers ─────────────────
        // Not just on-track — drivers in the garage (NotInWorld) appear in the
        // session YAML and should be shown in the standings widget just like
        // iOverlay and similar tools do.
        var standings = BuildStandings(
            candidates, onTrackSet, snapshot,
            isRace, isMultiClass, drivers, driverByIdx, carState);

        var relative = new RelativeData
        {
            Entries = candidates.Select(c => c.Entry).ToList()
        };

        return (relative, standings);
    }

    private static StandingsData BuildStandings(
        List<(float Gap, int CarIdx, RelativeEntry Entry)> allRelCandidates,
        HashSet<int>                                       onTrackSet,
        TelemetrySnapshot                                  snapshot,
        bool                                               isRace,
        bool                                               isMultiClass,
        IReadOnlyList<DriverSnapshot>                      allDrivers,
        Dictionary<int, DriverSnapshot>                    driverByIdx,
        CarStateTracker                                    carState)
    {
        // ── Collect all entries: on-track + garage ────────────────────────────
        // onTrackSet contains cars that are genuinely spawned on track (not garage).
        // allRelCandidates may also include garage cars from the relative list —
        // use onTrackSet to distinguish the two groups for standings sort/gap logic.
        var allEntries = new List<(int CarIdx, bool IsOnTrack, int Position, float BestLap, float Progress, RelativeEntry? RelEntry)>();

        foreach (var (_, carIdx, rel) in allRelCandidates)
        {
            bool isOnTrack = onTrackSet.Contains(carIdx);
            var bestLap  = carIdx < snapshot.BestLapTimes.Length ? snapshot.BestLapTimes[carIdx] : 0f;
            var pct      = snapshot.LapDistPcts[carIdx];
            var progress = isOnTrack ? snapshot.Laps[carIdx] + Math.Max(0f, pct) : 0f;
            allEntries.Add((carIdx, isOnTrack, rel.Position, bestLap, progress, rel));
        }

        // Garage drivers already in allRelCandidates (from the relative pass)
        var alreadyInEntries = new HashSet<int>(allEntries.Select(e => e.CarIdx));

        // Remaining session drivers not yet covered (registered but not in any list above)
        foreach (var driver in allDrivers)
        {
            if (driver.IsSpectator || driver.IsPaceCar)       continue;
            if (alreadyInEntries.Contains(driver.CarIdx))      continue;

            var carIdx  = driver.CarIdx;
            var bestLap = carIdx < snapshot.BestLapTimes.Length ? snapshot.BestLapTimes[carIdx] : 0f;
            allEntries.Add((carIdx, false, 0, bestLap, 0f, null));
        }

        if (allEntries.Count == 0) return new StandingsData();

        // ── Sort ──────────────────────────────────────────────────────────────
        // Race:     on-track by position → on-track by lap progress → garage by best lap
        // Practice: on-track + garage combined by best lap time (fastest first)
        List<(int CarIdx, bool IsOnTrack, int Position, float BestLap, float Progress, RelativeEntry? RelEntry)> sorted;

        if (isRace)
        {
            sorted = allEntries
                .OrderBy(e => !e.IsOnTrack)                               // on-track first
                .ThenBy(e => e.IsOnTrack && e.Position == 0 ? int.MaxValue : e.Position)
                .ThenByDescending(e => e.Progress)
                .ThenBy(e => e.BestLap <= 0 ? float.MaxValue : e.BestLap)
                .ToList();
        }
        else
        {
            // Practice / qualify: sort everyone by best lap time; no lap = bottom
            sorted = allEntries
                .OrderBy(e => e.BestLap <= 0 ? float.MaxValue : e.BestLap)
                .ToList();
        }

        // ── Compute per-class positions for all session drivers ───────────────
        // Re-derive so garage drivers get sensible class positions too.
        var allClassPositions = new Dictionary<int, int>(sorted.Count);
        var groupedByClass    = sorted.GroupBy(e =>
        {
            driverByIdx.TryGetValue(e.CarIdx, out var d);
            return d?.CarClassId ?? 0;
        });
        foreach (var group in groupedByClass)
        {
            int rank = 1;
            foreach (var e in group) // already in sorted order
                allClassPositions[e.CarIdx] = rank++;
        }

        bool anyMultiClass = allEntries
            .Select(e => { driverByIdx.TryGetValue(e.CarIdx, out var d); return d?.CarClassId ?? 0; })
            .Distinct()
            .Count() > 1;

        // ── Determine leader for gap computation ──────────────────────────────
        float leaderProgress  = sorted.Count > 0 ? sorted[0].Progress : 0f;
        float leaderBestLap   = sorted.Count > 0 ? sorted[0].BestLap  : 0f;

        // ── Build StandingsEntry list ─────────────────────────────────────────
        var entries       = new List<StandingsEntry>(sorted.Count);
        float prevGap     = 0f;

        for (int rank = 0; rank < sorted.Count; rank++)
        {
            var (carIdx, isOnTrack, pos, bestLap, progress, relEntry) = sorted[rank];
            bool isLeader = rank == 0;

            driverByIdx.TryGetValue(carIdx, out var driver);

            // Gap to leader
            float gapToLeader, interval;
            int   lapDiff;

            if (isRace && isOnTrack)
            {
                float lapsBehind = Math.Max(0f, leaderProgress - progress);
                gapToLeader = isLeader ? 0f : Math.Max(0.001f, lapsBehind * snapshot.EstimatedLapTime);
                lapDiff     = isLeader ? 0  : (int)Math.Max(0, Math.Floor(lapsBehind));
            }
            else if (!isRace && bestLap > 0f && leaderBestLap > 0f)
            {
                // Practice: gap = delta between best lap times
                gapToLeader = isLeader ? 0f : Math.Max(0f, bestLap - leaderBestLap);
                lapDiff     = 0;
            }
            else
            {
                gapToLeader = isLeader ? 0f : 0f; // garage with no lap time — show LEADER / no gap
                lapDiff     = 0;
            }

            interval  = isLeader ? 0f : Math.Max(0f, gapToLeader - prevGap);
            prevGap   = gapToLeader;

            float bestLapSec = carIdx < snapshot.BestLapTimes.Length ? snapshot.BestLapTimes[carIdx] : 0f;
            float lastLapSec = carIdx < snapshot.LastLapTimes.Length ? snapshot.LastLapTimes[carIdx] : 0f;
            bool  isOnPit    = carIdx < snapshot.OnPitRoad.Length    && snapshot.OnPitRoad[carIdx];
            float pitLaneSec = carIdx < snapshot.PitLaneTimes.Length ? snapshot.PitLaneTimes[carIdx] : 0f;
            int   tire       = carIdx < snapshot.TireCompounds.Length ? snapshot.TireCompounds[carIdx] : 0;

            var classPos = allClassPositions.TryGetValue(carIdx, out var cp) ? cp : 0;

            // Display position: race = CarIdxPosition; practice = sort rank + 1
            int displayPos = isRace && pos > 0 ? pos : rank + 1;

            string carClass   = anyMultiClass ? (driver?.CarClass ?? "") : "";
            var    classColor = anyMultiClass ? (driver?.ClassColor ?? ColorConfig.White) : ColorConfig.White;

            entries.Add(new StandingsEntry
            {
                Position           = displayPos,
                ClassPosition      = classPos,
                CarNumber          = driver?.CarNumber      ?? carIdx.ToString(),
                DriverName         = driver?.UserName       ?? string.Empty,
                IRating            = driver?.IRating        ?? 0,
                LicenseClass       = driver?.LicenseClass   ?? LicenseClass.R,
                LicenseLevel       = driver?.LicenseLevel   ?? "R 0.00",
                CarClass           = carClass,
                ClassColor         = classColor,
                GapToLeaderSeconds = gapToLeader,
                Interval           = interval,
                LapDifference      = lapDiff,
                BestLapTime        = bestLapSec > 0 ? TimeSpan.FromSeconds(bestLapSec) : TimeSpan.Zero,
                LastLapTime        = lastLapSec > 0 ? TimeSpan.FromSeconds(lastLapSec) : TimeSpan.Zero,
                IsPlayer           = carIdx == snapshot.PlayerCarIdx,
                ClubName           = driver?.ClubName       ?? string.Empty,
                TeamName           = driver?.TeamName       ?? string.Empty,
                CarScreenName      = driver?.CarScreenName  ?? string.Empty,
                IsOnPitRoad        = isOnPit,
                IsOutLap           = carState.IsOnOutLap(carIdx),
                TireCompound       = tire,
                StintLaps          = carState.GetStintLaps(snapshot, carIdx),
                PositionsGained    = carState.GetPositionsGained(carIdx, displayPos),
                PitLaneTime        = pitLaneSec,
            });
        }

        return new StandingsData { Entries = entries };
    }
}
