using SimOverlay.Core.Config;
using SimOverlay.Sim.Contracts;
using SimOverlay.Sim.LMU.SharedMemory;

namespace SimOverlay.Sim.LMU;

/// <summary>
/// Pure static calculator: converts LMU scoring data to a sorted
/// <see cref="RelativeData"/> and <see cref="StandingsData"/>.
/// <para>
/// Lap distance is in metres (0 → TrackLength).  Distances are normalised to
/// [0, 1] before the gap calculation, then the same wrapping logic applies as
/// the iRacing calculator.
/// </para>
/// </summary>
internal static class LmuRelativeCalculator
{
    private const int MaxEntries = 15;

    private sealed record CarCandidate(
        int                SlotId,
        float              Gap,
        int                OverallPosition,
        int                LapDiff,
        LmuDriverSnapshot? Driver);

    /// <summary>
    /// Computes the relative display list and full-field standings.
    /// </summary>
    /// <param name="vehicles">Live scoring vehicle array.</param>
    /// <param name="drivers">Driver list built from the latest session decode.</param>
    /// <param name="playerSlotId">Slot ID of the player vehicle (used for <see cref="RelativeEntry.IsPlayer"/>).</param>
    /// <param name="trackLengthMeters">Track length in metres (must be &gt; 0).</param>
    /// <param name="estimatedLapTime">Estimated lap time in seconds; used for gap conversion.</param>
    public static (RelativeData Relative, StandingsData Standings) Compute(
        LmuVehicleScoring[]              vehicles,
        IReadOnlyList<LmuDriverSnapshot> drivers,
        int                              playerSlotId,
        double                           trackLengthMeters,
        double                           estimatedLapTime)
    {
        if (trackLengthMeters <= 0) return (new RelativeData(), new StandingsData());
        if (estimatedLapTime  <= 0) estimatedLapTime = 90.0;

        // Build O(1) lookup: SlotId → LmuDriverSnapshot
        var driverBySlot = new Dictionary<int, LmuDriverSnapshot>(drivers.Count);
        foreach (var d in drivers)
            driverBySlot[d.SlotId] = d;

        // Find player's lap distance by IsPlayer flag.
        float playerPct = -1f;
        int   playerLap = 0;
        foreach (ref readonly var v in vehicles.AsSpan())
        {
            if (v.IsPlayer != 0)
            {
                playerPct = (float)(v.LapDist / trackLengthMeters);
                playerLap = v.TotalLaps;
                break;
            }
        }

        if (playerPct < 0f) return (new RelativeData(), new StandingsData());

        // ── Pass 1: collect on-track cars ─────────────────────────────────────
        var allCars = new List<CarCandidate>(vehicles.Length);
        foreach (ref readonly var v in vehicles.AsSpan())
        {
            if (!v.IsActive || v.InGarageStall != 0) continue;

            float pct   = (float)(v.LapDist / trackLengthMeters);
            float delta = pct - playerPct;

            // Wrap delta to [-0.5, 0.5] at the start/finish line.
            if (delta >  0.5f) delta -= 1f;
            if (delta < -0.5f) delta += 1f;

            float gapSeconds = (float)(-delta * estimatedLapTime);
            int   lapDiff    = v.TotalLaps - playerLap;

            // Overall position: directly from Place field (1-based byte).
            int pos = v.Place;

            driverBySlot.TryGetValue(v.Id, out var driver);
            allCars.Add(new CarCandidate(v.Id, gapSeconds, pos, lapDiff, driver));
        }

        // ── Pass 2: compute per-class positions ───────────────────────────────
        var classPositionBySlot = new Dictionary<int, int>(allCars.Count);
        foreach (var group in allCars.GroupBy(c => c.Driver?.CarClassId ?? 0))
        {
            var sorted = group
                .OrderBy(c => c.OverallPosition == 0 ? int.MaxValue : c.OverallPosition)
                .ToList();
            for (int rank = 0; rank < sorted.Count; rank++)
                classPositionBySlot[sorted[rank].SlotId] = rank + 1;
        }

        int distinctClasses = allCars
            .Select(c => c.Driver?.CarClassId ?? 0)
            .Distinct()
            .Count();
        bool isMultiClass = distinctClasses > 1;

        // Build best-lap lookup from vehicle array.
        var bestLapBySlot = new Dictionary<int, double>(allCars.Count);
        foreach (ref readonly var v in vehicles.AsSpan())
            if (v.BestLapTime > 0) bestLapBySlot[v.Id] = v.BestLapTime;

        // ── Build relative entry list ─────────────────────────────────────────
        var candidates = new List<(float Gap, int SlotId, RelativeEntry Entry)>(allCars.Count);
        foreach (var car in allCars)
        {
            var driver        = car.Driver;
            var classPosition = classPositionBySlot.TryGetValue(car.SlotId, out var cp)
                ? cp
                : car.OverallPosition;

            candidates.Add((car.Gap, car.SlotId, new RelativeEntry
            {
                Position           = car.OverallPosition,
                CarNumber          = driver?.CarNumber ?? car.SlotId.ToString(),
                DriverName         = driver?.DriverName ?? string.Empty,
                IRating            = 0,
                LicenseClass       = LicenseClass.Unknown,
                LicenseLevel       = string.Empty,
                GapToPlayerSeconds = car.Gap,
                LapDifference      = car.LapDiff,
                IsPlayer           = car.SlotId == playerSlotId,
                CarClass           = isMultiClass ? (driver?.VehicleClass ?? "") : "",
                ClassPosition      = classPosition,
                ClassColor         = isMultiClass
                    ? (driver?.ClassColor ?? ColorConfig.White)
                    : ColorConfig.White,
            }));
        }

        candidates.Sort((a, b) => a.Gap.CompareTo(b.Gap));

        // ── Build standings (all cars, sorted by overall position) ────────────
        var standingsSorted = candidates
            .OrderBy(c => c.Entry.Position == 0 ? int.MaxValue : c.Entry.Position)
            .ToList();

        StandingsData standings;
        if (standingsSorted.Count == 0)
        {
            standings = new StandingsData();
        }
        else
        {
            float leaderGap     = standingsSorted[0].Entry.GapToPlayerSeconds;
            int   leaderLapDiff = standingsSorted[0].Entry.LapDifference;
            var   standingsEntries = new List<StandingsEntry>(standingsSorted.Count);

            foreach (var (gap, slotId, rel) in standingsSorted)
            {
                bestLapBySlot.TryGetValue(slotId, out double bestSec);
                standingsEntries.Add(new StandingsEntry
                {
                    Position           = rel.Position,
                    ClassPosition      = rel.ClassPosition,
                    CarNumber          = rel.CarNumber,
                    DriverName         = rel.DriverName,
                    IRating            = 0,
                    CarClass           = isMultiClass ? rel.CarClass : "",
                    ClassColor         = rel.ClassColor,
                    GapToLeaderSeconds = gap - leaderGap,
                    LapDifference      = rel.LapDifference - leaderLapDiff,
                    BestLapTime        = bestSec > 0 ? TimeSpan.FromSeconds(bestSec) : TimeSpan.Zero,
                    IsPlayer           = rel.IsPlayer,
                });
            }
            standings = new StandingsData { Entries = standingsEntries };
        }

        // ── Select relative window ────────────────────────────────────────────
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
}
