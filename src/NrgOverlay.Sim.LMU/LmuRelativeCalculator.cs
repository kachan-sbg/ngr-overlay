using NrgOverlay.Core.Config;
using NrgOverlay.Sim.Contracts;
using NrgOverlay.Sim.LMU.SharedMemory;

namespace NrgOverlay.Sim.LMU;

/// <summary>
/// Pure static calculator: converts LMU scoring data to a sorted
/// <see cref="RelativeData"/> and <see cref="StandingsData"/>.
/// <para>
/// Lap distance is in metres (0 в†’ TrackLength).  Distances are normalised to
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

        // Build O(1) lookup: SlotId в†’ LmuDriverSnapshot
        var driverBySlot = new Dictionary<int, LmuDriverSnapshot>(drivers.Count);
        foreach (var d in drivers)
            driverBySlot[d.SlotId] = d;

        var vehicleBySlot = new Dictionary<int, LmuVehicleScoring>(vehicles.Length);
        foreach (ref readonly var v in vehicles.AsSpan())
            if (v.IsActive) vehicleBySlot[v.Id] = v;

        // Find player's lap distance by IsPlayer flag.
        float             playerPct = -1f;
        int               playerLap = 0;
        LmuVehicleScoring playerVehicle = default;
        bool              hasPlayerVehicle = false;
        foreach (ref readonly var v in vehicles.AsSpan())
        {
            if (v.IsPlayer != 0)
            {
                playerVehicle = v;
                hasPlayerVehicle = true;
                playerPct = (float)(v.LapDist / trackLengthMeters);
                playerLap = v.TotalLaps;
                break;
            }
        }

        if (playerPct < 0f) return (new RelativeData(), new StandingsData());

        bool useLeaderTiming = hasPlayerVehicle && vehicles.Any(v =>
            v.IsActive
            && v.InGarageStall == 0
            && (v.TimeBehindLeader > 0.001 || v.LapsBehindLeader != 0));

        // в”Ђв”Ђ Pass 1: collect on-track cars в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        var allCars = new List<CarCandidate>(vehicles.Length);
        foreach (ref readonly var v in vehicles.AsSpan())
        {
            if (!v.IsActive || v.InGarageStall != 0) continue;

            float gapSeconds;
            int   lapDiff;
            if (useLeaderTiming)
            {
                // Prefer native LMU timing deltas when available.
                gapSeconds = (float)(v.TimeBehindLeader - playerVehicle.TimeBehindLeader);
                lapDiff = v.LapsBehindLeader - playerVehicle.LapsBehindLeader;
            }
            else
            {
                float pct   = (float)(v.LapDist / trackLengthMeters);
                float delta = pct - playerPct;

                // Wrap delta to [-0.5, 0.5] at the start/finish line.
                if (delta >  0.5f) delta -= 1f;
                if (delta < -0.5f) delta += 1f;

                gapSeconds = (float)(-delta * estimatedLapTime);
                lapDiff = v.TotalLaps - playerLap;
            }

            // Overall position: directly from Place field (1-based byte).
            int pos = v.Place;

            driverBySlot.TryGetValue(v.Id, out var driver);
            allCars.Add(new CarCandidate(v.Id, gapSeconds, pos, lapDiff, driver));
        }

        // в”Ђв”Ђ Pass 2: compute per-class positions в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

        // в”Ђв”Ђ Build relative entry list в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        var candidates = new List<(float Gap, int SlotId, RelativeEntry Entry)>(allCars.Count);
        foreach (var car in allCars)
        {
            var driver        = car.Driver;
            vehicleBySlot.TryGetValue(car.SlotId, out var liveVehicle);
            var classPosition = classPositionBySlot.TryGetValue(car.SlotId, out var cp)
                ? cp
                : car.OverallPosition;

            var driverName = ResolveDriverName(driver, liveVehicle, car.SlotId);
            var carNumber  = ResolveCarNumber(driver, liveVehicle, car.SlotId);

            candidates.Add((car.Gap, car.SlotId, new RelativeEntry
            {
                Position           = car.OverallPosition,
                CarNumber          = carNumber,
                DriverName         = driverName,
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
                CountryCode        = driver?.CountryCode ?? string.Empty,
            }));
        }

        candidates.Sort((a, b) => a.Gap.CompareTo(b.Gap));

        // в”Ђв”Ђ Build standings (all cars, sorted by overall position) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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
                    CountryCode        = rel.CountryCode,
                });
            }
            standings = new StandingsData { Entries = standingsEntries };
        }

        // в”Ђв”Ђ Select relative window в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

    private static string ResolveDriverName(
        LmuDriverSnapshot? snapshot,
        in LmuVehicleScoring liveVehicle,
        int slotId)
    {
        if (!string.IsNullOrWhiteSpace(snapshot?.DriverName))
            return snapshot.DriverName;

        if (!string.IsNullOrWhiteSpace(liveVehicle.DriverName))
            return liveVehicle.DriverName;

        return $"Driver {slotId}";
    }

    private static string ResolveCarNumber(
        LmuDriverSnapshot? snapshot,
        in LmuVehicleScoring liveVehicle,
        int slotId)
    {
        if (!string.IsNullOrWhiteSpace(snapshot?.CarNumber) && snapshot.CarNumber != "0")
            return snapshot.CarNumber;

        var fromVehicleName = ExtractCarNumberToken(liveVehicle.VehicleName);
        if (!string.IsNullOrWhiteSpace(fromVehicleName))
            return fromVehicleName;

        return slotId > 0 ? slotId.ToString() : "--";
    }

    private static string ExtractCarNumberToken(string? vehicleName)
    {
        if (string.IsNullOrWhiteSpace(vehicleName)) return string.Empty;

        var s = vehicleName;
        for (int i = 0; i < s.Length - 1; i++)
        {
            if (s[i] != '#') continue;
            int start = i + 1;
            int end = start;
            while (end < s.Length && char.IsDigit(s[end])) end++;
            if (end > start)
                return s[start..end];
        }

        return string.Empty;
    }
}

