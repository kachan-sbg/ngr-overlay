using NrgOverlay.Sim.Contracts;
using NrgOverlay.Sim.iRacing;

namespace NrgOverlay.Sim.iRacing.Tests;

/// <summary>
/// Unit tests for <see cref="IRacingRelativeCalculator"/>.
/// All tests are pure in-memory вЂ” no SDK or iRacing process required.
/// </summary>
public class RelativeCalculatorTests
{
    // в”Ђв”Ђ Helpers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>
    /// Builds a minimal <see cref="TelemetrySnapshot"/> for tests.
    /// Arrays are exactly MaxCars (64) in length; all slots default to -1 (off-track).
    /// </summary>
    private static TelemetrySnapshot MakeSnapshot(
        int   playerCarIdx,
        float estLapTime,
        (int carIdx, float pct, int lap, int pos)[] cars)
    {
        const int MaxCars = 64;
        var pcts     = Enumerable.Repeat(-1f, MaxCars).ToArray();
        var laps     = new int[MaxCars];
        var pos      = new int[MaxCars];
        // Default TrackSurfaces to 3 (OnTrack) for all slots that have valid pct;
        // slots left at pct=-1 are treated as NotInWorld (-1) by the calculator anyway.
        var surfaces = Enumerable.Repeat(-1, MaxCars).ToArray();

        foreach (var (idx, pct, lap, position) in cars)
        {
            pcts[idx]     = pct;
            laps[idx]     = lap;
            pos[idx]      = position;
            surfaces[idx] = 3; // OnTrack
        }

        return new TelemetrySnapshot(
            PlayerCarIdx:     playerCarIdx,
            LapDistPcts:      pcts,
            Positions:        pos,
            Laps:             laps,
            EstimatedLapTime: estLapTime,
            BestLapTimes:     new float[MaxCars],
            LastLapTimes:     new float[MaxCars],
            TrackSurfaces:    surfaces,
            OnPitRoad:        new bool[MaxCars],
            F2Times:          new float[MaxCars],
            PitStopCounts:    new int[MaxCars],
            PitLaneTimes:     new float[MaxCars],
            TireCompounds:    new int[MaxCars]);
    }

    /// <summary>Creates a simple <see cref="DriverSnapshot"/> for a given car index.</summary>
    private static DriverSnapshot MakeDriver(
        int carIdx,
        bool isSpectator = false,
        bool isPaceCar   = false,
        int carClassId   = 0,
        string carClass  = "") =>
        new(carIdx, $"Driver{carIdx}", carIdx.ToString(), 1500, LicenseClass.B, "B 3.00",
            isSpectator, isPaceCar, carClassId, carClass);

    // в”Ђв”Ђ Core gap computation в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void Player_IsMarkedAndPresent()
    {
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.5f, 1, 1),
            (1, 0.6f, 1, 2),
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1) };

        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());

        var player = result.Relative.Entries.Single(e => e.IsPlayer);
        Assert.Equal("0", player.CarNumber);
    }

    [Fact]
    public void CarAhead_HasNegativeGap()
    {
        // Car 1 is 10 % of a 90-second lap ahead в†’ вЂ“9 s gap
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.50f, 1, 2),
            (1, 0.60f, 1, 1),   // 10 % ahead of player в†’ вЂ“9 s
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1) };

        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());

        var ahead = result.Relative.Entries.Single(e => !e.IsPlayer);
        Assert.True(ahead.GapToPlayerSeconds < 0, "Car ahead should have a negative gap.");
        Assert.Equal(-9f, ahead.GapToPlayerSeconds, precision: 3);
    }

    [Fact]
    public void CarBehind_HasPositiveGap()
    {
        // Car 1 is 10 % of a 90-second lap behind в†’ +9 s gap
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.50f, 1, 1),
            (1, 0.40f, 1, 2),   // 10 % behind player в†’ +9 s
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1) };

        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());

        var behind = result.Relative.Entries.Single(e => !e.IsPlayer);
        Assert.True(behind.GapToPlayerSeconds > 0, "Car behind should have a positive gap.");
        Assert.Equal(9f, behind.GapToPlayerSeconds, precision: 3);
    }

    [Fact]
    public void OutputIsSortedAheadThenPlayerThenBehind()
    {
        // Car 1 is ahead, car 0 is player, car 2 is behind.
        var snapshot = MakeSnapshot(0, 60f, [
            (0, 0.50f, 1, 2),
            (1, 0.70f, 1, 1),   // ahead
            (2, 0.30f, 1, 3),   // behind
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1), MakeDriver(2) };

        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());
        var gaps   = result.Relative.Entries.Select(e => e.GapToPlayerSeconds).ToList();

        // Gaps must be monotonically non-decreasing.
        for (int i = 1; i < gaps.Count; i++)
            Assert.True(gaps[i] >= gaps[i - 1],
                $"Entry {i} gap ({gaps[i]}) should be >= entry {i - 1} gap ({gaps[i - 1]}).");

        // Player gap is exactly 0.
        Assert.Equal(0f, result.Relative.Entries.Single(e => e.IsPlayer).GapToPlayerSeconds);
    }

    // в”Ђв”Ђ Start/finish-line wrap-around в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void WrapAround_CarJustAheadAcrossLine_SmallNegativeGap()
    {
        // Player at 98 %, car 1 at 2 % on the SAME lap.
        // Going forward from the player (+2 % more crosses the line, +2 % more reaches the car):
        // the car is 4 % AHEAD of the player on track.
        // delta = 0.02 в€’ 0.98 = в€’0.96 в†’ < в€’0.5 в†’ add 1 в†’ +0.04 в†’ gap = в€’(0.04 Г— 90) = в€’3.6 s.
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.98f, 5, 1),
            (1, 0.02f, 5, 2),   // 4 % ahead after wrap в†’ в€’3.6 s
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1) };

        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());
        var carGap = result.Relative.Entries.Single(e => !e.IsPlayer).GapToPlayerSeconds;

        Assert.True(carGap < 0f, "Car physically ahead of S/F line should have negative gap.");
        Assert.Equal(-3.6f, carGap, precision: 3);
    }

    [Fact]
    public void WrapAround_CarJustBehindAcrossLine_SmallPositiveGap()
    {
        // Player at 2 % (lap 5, just crossed), car 1 at 98 % (lap 4, not yet crossed).
        // Going forward from the car (+2 % crosses the line, +2 % reaches the player):
        // the car is 4 % BEHIND the player on track.
        // delta = 0.98 в€’ 0.02 = +0.96 в†’ > +0.5 в†’ subtract 1 в†’ в€’0.04 в†’ gap = в€’(в€’0.04 Г— 90) = +3.6 s.
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.02f, 5, 2),
            (1, 0.98f, 4, 1),   // 4 % behind after wrap в†’ +3.6 s
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1) };

        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());
        var carGap = result.Relative.Entries.Single(e => !e.IsPlayer).GapToPlayerSeconds;

        Assert.True(carGap > 0f, "Car physically behind S/F line should have positive gap.");
        Assert.Equal(3.6f, carGap, precision: 3);
    }

    [Fact]
    public void WrapAround_TwoCarsSymmetricAroundLine_GapsAreEqual_OppositeSign()
    {
        // Player at 0.5.  Car 1 at 0.9 is 40 % ahead  в†’ gap = в€’(0.4 Г— 60) = в€’24 s.
        //                 Car 2 at 0.1 is 40 % behind в†’ gap = в€’(в€’0.4 Г— 60) = +24 s.
        var snapshot = MakeSnapshot(0, 60f, [
            (0, 0.50f, 3, 2),
            (1, 0.90f, 3, 3),   // 40 % ahead в†’ в€’24 s
            (2, 0.10f, 3, 1),   // 40 % behind в†’ +24 s
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1), MakeDriver(2) };

        var result    = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());
        var gapCar1   = result.Relative.Entries.Single(e => e.CarNumber == "1").GapToPlayerSeconds;
        var gapCar2   = result.Relative.Entries.Single(e => e.CarNumber == "2").GapToPlayerSeconds;

        Assert.Equal(-24f, gapCar1, precision: 3);
        Assert.Equal( 24f, gapCar2, precision: 3);
    }

    // в”Ђв”Ђ Lap difference в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void LapDifference_IsCorrectlyRecorded()
    {
        // Car 1 is a lap ahead of the player.
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.5f, 3, 2),
            (1, 0.5f, 4, 1),   // same pct, one lap ahead в†’ lapDiff = +1
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1) };

        var result    = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());
        var lapDiff   = result.Relative.Entries.Single(e => !e.IsPlayer).LapDifference;

        Assert.Equal(1, lapDiff);
    }

    [Fact]
    public void LapDifference_LappedCar_IsNegative()
    {
        // Car 1 is a lap behind the player.
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.5f, 3, 1),
            (1, 0.5f, 2, 2),   // one lap behind в†’ lapDiff = в€’1
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1) };

        var result  = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());
        var lapDiff = result.Relative.Entries.Single(e => !e.IsPlayer).LapDifference;

        Assert.Equal(-1, lapDiff);
    }

    // в”Ђв”Ђ Window selection в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void OutputContainsAllOnTrackEntries()
    {
        // Calculator now returns all on-track cars; windowing is done in the overlay.
        const int carCount = 20;
        var cars = Enumerable.Range(0, carCount)
            .Select(i => (carIdx: i, pct: i / (float)carCount, lap: 1, pos: i + 1))
            .ToArray();

        var snapshot = MakeSnapshot(0, 90f, cars);
        var drivers  = Enumerable.Range(0, carCount).Select(i => MakeDriver(i)).ToArray();

        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());

        Assert.Equal(carCount, result.Relative.Entries.Count);
    }

    [Fact]
    public void PlayerIsAlwaysInTheWindow()
    {
        // 20 cars; player (car 0) is at one extreme.
        var cars = Enumerable.Range(0, 20)
            .Select(i => (carIdx: i, pct: i / 20f, lap: 1, pos: i + 1))
            .ToArray();

        var snapshot = MakeSnapshot(0, 90f, cars);
        var drivers  = Enumerable.Range(0, 20).Select(i => MakeDriver(i)).ToArray();

        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());

        Assert.Contains(result.Relative.Entries, e => e.IsPlayer);
    }

    // в”Ђв”Ђ Filtering в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void Spectators_AreExcluded()
    {
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.5f, 1, 1),
            (1, 0.6f, 1, 0),   // spectators typically have position 0
        ]);
        var drivers = new[]
        {
            MakeDriver(0),
            MakeDriver(1, isSpectator: true),   // should be filtered out
        };

        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());

        Assert.DoesNotContain(result.Relative.Entries, e => e.CarNumber == "1");
    }

    [Fact]
    public void PaceCar_IsExcluded()
    {
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.5f, 1, 1),
            (1, 0.4f, 1, 0),
        ]);
        var drivers = new[]
        {
            MakeDriver(0),
            MakeDriver(1, isPaceCar: true),   // should be filtered out
        };

        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());

        Assert.DoesNotContain(result.Relative.Entries, e => e.CarNumber == "1");
    }

    [Fact]
    public void CarsWithNegativePct_IncludedAsGarageEntries()
    {
        // Negative pct = car is in the garage (irsdk_NotInWorld).
        // Registered session drivers should still appear in the relative as garage entries
        // so the user can see connected drivers who haven't spawned yet.
        var snapshot = MakeSnapshot(0, 90f, [
            (0,  0.5f, 1, 1),
            (1, -1.0f, 0, 0),   // in garage
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1) };

        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());

        var garageEntry = result.Relative.Entries.Single(e => e.CarNumber == "1");
        Assert.True(garageEntry.IsInGarage);
    }

    // в”Ђв”Ђ Driver info join в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void DriverInfo_IsJoinedCorrectly()
    {
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.5f, 1, 1),
            (1, 0.6f, 1, 2),
        ]);
        var drivers = new[]
        {
            new DriverSnapshot(0, "Alice", "99",  2400, LicenseClass.A, "A 4.12", false, false),
            new DriverSnapshot(1, "Bob",   "12", 1800, LicenseClass.B, "B 2.00", false, false),
        };

        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());

        var alice = result.Relative.Entries.Single(e => e.IsPlayer);
        Assert.Equal("Alice",  alice.DriverName);
        Assert.Equal("99",     alice.CarNumber);
        Assert.Equal(2400,     alice.IRating);
        Assert.Equal(LicenseClass.A, alice.LicenseClass);
        Assert.Equal("A 4.12", alice.LicenseLevel);

        var bob = result.Relative.Entries.Single(e => !e.IsPlayer);
        Assert.Equal("Bob", bob.DriverName);
        Assert.Equal("12",  bob.CarNumber);
    }

    [Fact]
    public void UnknownDriver_FallsBackToCarIdxAsNumber()
    {
        // Car 1 has no entry in the driver list.
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.5f, 1, 1),
            (1, 0.6f, 1, 2),
        ]);
        var drivers = new[] { MakeDriver(0) };   // no entry for car 1

        var result  = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());
        var unknown = result.Relative.Entries.Single(e => !e.IsPlayer);

        Assert.Equal("1", unknown.CarNumber);   // falls back to carIdx.ToString()
        Assert.Equal(string.Empty, unknown.DriverName);
    }

    // в”Ђв”Ђ Edge cases в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void PlayerNotOnTrack_IncludedAsGarageEntry()
    {
        // Player's pct is -1 (in garage) вЂ” the player entry must still appear in the relative
        // so the widget is not empty, and on-track cars are still shown around them.
        var snapshot = MakeSnapshot(0, 90f, [
            (1, 0.3f, 1, 1),
            (2, 0.6f, 1, 2),
            // car 0 (player) is absent в†’ defaults to pct = в€’1
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1), MakeDriver(2) };

        // Should not throw; player is present as a garage entry.
        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());

        Assert.True(result.Relative.Entries.Count <= 15);
        var player = result.Relative.Entries.Single(e => e.IsPlayer);
        Assert.True(player.IsInGarage);
        Assert.Equal(0f, player.GapToPlayerSeconds);
    }

    [Fact]
    public void SingleCar_IsPlayerOnly_ReturnsOneEntry()
    {
        var snapshot = MakeSnapshot(0, 90f, [(0, 0.5f, 1, 1)]);
        var drivers  = new[] { MakeDriver(0) };

        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());

        Assert.Single(result.Relative.Entries);
        Assert.True(result.Relative.Entries[0].IsPlayer);
        Assert.Equal(0f, result.Relative.Entries[0].GapToPlayerSeconds);
    }

    // в”Ђв”Ђ Multi-class в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void MultiClass_ClassPositions_AreCorrect()
    {
        // GTP: cars 0 (P1), 2 (P3) в†’ class positions 1, 2
        // GT3: cars 1 (P2), 3 (P4) в†’ class positions 1, 2
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.70f, 1, 1),   // GTP leader (player)
            (1, 0.65f, 1, 2),   // GT3 leader
            (2, 0.60f, 1, 3),   // GTP P2
            (3, 0.55f, 1, 4),   // GT3 P2
        ]);
        var drivers = new[]
        {
            MakeDriver(0, carClassId: 1, carClass: "GTP"),
            MakeDriver(1, carClassId: 2, carClass: "GT3"),
            MakeDriver(2, carClassId: 1, carClass: "GTP"),
            MakeDriver(3, carClassId: 2, carClass: "GT3"),
        };

        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());

        var gtp1 = result.Relative.Entries.Single(e => e.CarNumber == "0");
        var gt31 = result.Relative.Entries.Single(e => e.CarNumber == "1");
        var gtp2 = result.Relative.Entries.Single(e => e.CarNumber == "2");
        var gt32 = result.Relative.Entries.Single(e => e.CarNumber == "3");

        Assert.Equal(1, gtp1.ClassPosition);
        Assert.Equal(1, gt31.ClassPosition);
        Assert.Equal(2, gtp2.ClassPosition);
        Assert.Equal(2, gt32.ClassPosition);
    }

    [Fact]
    public void MultiClass_CarClassAndColor_ArePopulated()
    {
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.70f, 1, 1),
            (1, 0.65f, 1, 2),
        ]);
        var drivers = new[]
        {
            MakeDriver(0, carClassId: 1, carClass: "GTP"),
            MakeDriver(1, carClassId: 2, carClass: "GT3"),
        };

        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());

        Assert.Equal("GTP", result.Relative.Entries.Single(e => e.CarNumber == "0").CarClass);
        Assert.Equal("GT3", result.Relative.Entries.Single(e => e.CarNumber == "1").CarClass);
    }

    [Fact]
    public void SingleClass_CarClassIsEmpty_ClassPositionEqualsOverallPosition()
    {
        // All cars in same class в†’ single-class session rules apply
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.70f, 1, 1),
            (1, 0.65f, 1, 2),
            (2, 0.60f, 1, 3),
        ]);
        var drivers = new[]
        {
            MakeDriver(0, carClassId: 1, carClass: "GT3"),
            MakeDriver(1, carClassId: 1, carClass: "GT3"),
            MakeDriver(2, carClassId: 1, carClass: "GT3"),
        };

        var result = new IRacingRelativeCalculator().Compute(snapshot, drivers, new CarStateTracker());

        // CarClass must be empty in single-class session
        Assert.All(result.Relative.Entries, e => Assert.Equal("", e.CarClass));

        // ClassPosition should equal the class-internal rank (1, 2, 3)
        var p1 = result.Relative.Entries.Single(e => e.CarNumber == "0");
        var p2 = result.Relative.Entries.Single(e => e.CarNumber == "1");
        var p3 = result.Relative.Entries.Single(e => e.CarNumber == "2");
        Assert.Equal(1, p1.ClassPosition);
        Assert.Equal(2, p2.ClassPosition);
        Assert.Equal(3, p3.ClassPosition);
    }
}

