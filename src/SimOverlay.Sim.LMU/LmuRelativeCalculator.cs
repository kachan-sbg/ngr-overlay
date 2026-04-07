using SimOverlay.Core.Config;
using SimOverlay.Sim.Contracts;
using SimOverlay.Sim.LMU.SharedMemory;

namespace SimOverlay.Sim.LMU;

/// <summary>
/// Pure static calculator: converts rF2/LMU scoring data to a sorted
/// <see cref="RelativeData"/> suitable for the relative overlay.
/// <para>
/// Key difference from the iRacing calculator: lap distance is in metres
/// (0 → TrackLength) rather than a 0–1 percentage.  Distances are normalised
/// to [0, 1] before the gap calculation, then the same wrapping logic applies.
/// </para>
/// </summary>
internal static class LmuRelativeCalculator
{
    private const int MaxEntries = 15;

    private sealed record CarCandidate(
        int              SlotId,
        float            Gap,
        int              OverallPosition,
        int              LapDiff,
        LmuDriverSnapshot? Driver);

    /// <summary>
    /// Computes the relative display list.
    /// </summary>
    /// <param name="vehicles">Live scoring vehicle array.</param>
    /// <param name="drivers">Driver list built from the latest session decode.</param>
    /// <param name="playerSlotId">Slot ID of the player vehicle.</param>
    /// <param name="trackLengthMeters">Track length in metres (must be > 0).</param>
    /// <param name="estimatedLapTime">Estimated lap time in seconds; used for gap conversion.</param>
    public static RelativeData Compute(
        Rf2VehicleScoring[]     vehicles,
        IReadOnlyList<LmuDriverSnapshot> drivers,
        int                     playerSlotId,
        double                  trackLengthMeters,
        double                  estimatedLapTime)
    {
        if (trackLengthMeters <= 0) return new RelativeData();
        if (estimatedLapTime  <= 0) estimatedLapTime = 90.0;

        // Build O(1) lookup: SlotId → LmuDriverSnapshot
        var driverBySlot = new Dictionary<int, LmuDriverSnapshot>(drivers.Count);
        foreach (var d in drivers)
            driverBySlot[d.SlotId] = d;

        // Find player's lap distance.
        float playerPct  = -1f;
        int   playerLap  = 0;
        foreach (ref readonly var v in vehicles.AsSpan())
        {
            if (v.Id == playerSlotId)
            {
                playerPct = (float)(v.LapDist / trackLengthMeters);
                playerLap = v.TotalLaps;
                break;
            }
        }

        if (playerPct < 0f) return new RelativeData();

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

            // Overall position: use Place from V02 expansion; 0 if not available.
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

        // ── Build relative entry list ─────────────────────────────────────────
        var candidates = new List<(float Gap, RelativeEntry Entry)>(allCars.Count);
        foreach (var car in allCars)
        {
            var driver        = car.Driver;
            var classPosition = classPositionBySlot.TryGetValue(car.SlotId, out var cp)
                ? cp
                : car.OverallPosition;

            candidates.Add((car.Gap, new RelativeEntry
            {
                Position           = car.OverallPosition,
                CarNumber          = driver?.CarNumber ?? car.SlotId.ToString(),
                DriverName         = driver?.DriverName ?? string.Empty,
                IRating            = 0,                        // unavailable in LMU
                LicenseClass       = LicenseClass.Unknown,     // unavailable in LMU
                LicenseLevel       = string.Empty,             // unavailable in LMU
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

        int playerIdx = candidates.FindIndex(c => c.Entry.IsPlayer);
        if (playerIdx < 0)
        {
            return new RelativeData
            {
                Entries = candidates.Take(MaxEntries).Select(c => c.Entry).ToList()
            };
        }

        int half  = MaxEntries / 2;
        int start = Math.Max(0, playerIdx - half);
        int end   = Math.Min(candidates.Count, start + MaxEntries);
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
