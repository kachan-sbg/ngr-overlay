using SimOverlay.Sim.Contracts;
using SimOverlay.Sim.iRacing;

namespace SimOverlay.Sim.iRacing.Tests;

/// <summary>
/// Unit tests for <see cref="IRacingRelativeCalculator"/>.
/// All tests are pure in-memory — no SDK or iRacing process required.
/// </summary>
public class RelativeCalculatorTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

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
        var pcts  = Enumerable.Repeat(-1f, MaxCars).ToArray();
        var laps  = new int[MaxCars];
        var pos   = new int[MaxCars];

        foreach (var (idx, pct, lap, position) in cars)
        {
            pcts[idx] = pct;
            laps[idx] = lap;
            pos[idx]  = position;
        }

        return new TelemetrySnapshot(
            PlayerCarIdx:    playerCarIdx,
            LapDistPcts:     pcts,
            Positions:       pos,
            Laps:            laps,
            EstimatedLapTime: estLapTime);
    }

    /// <summary>Creates a simple <see cref="DriverSnapshot"/> for a given car index.</summary>
    private static DriverSnapshot MakeDriver(int carIdx, bool isSpectator = false, bool isPaceCar = false) =>
        new(carIdx, $"Driver{carIdx}", carIdx.ToString(), 1500, LicenseClass.B, "B 3.00",
            isSpectator, isPaceCar);

    // ── Core gap computation ──────────────────────────────────────────────────

    [Fact]
    public void Player_IsMarkedAndPresent()
    {
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.5f, 1, 1),
            (1, 0.6f, 1, 2),
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1) };

        var result = IRacingRelativeCalculator.Compute(snapshot, drivers);

        var player = result.Entries.Single(e => e.IsPlayer);
        Assert.Equal("0", player.CarNumber);
    }

    [Fact]
    public void CarAhead_HasNegativeGap()
    {
        // Car 1 is 10 % of a 90-second lap ahead → –9 s gap
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.50f, 1, 2),
            (1, 0.60f, 1, 1),   // 10 % ahead of player → –9 s
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1) };

        var result = IRacingRelativeCalculator.Compute(snapshot, drivers);

        var ahead = result.Entries.Single(e => !e.IsPlayer);
        Assert.True(ahead.GapToPlayerSeconds < 0, "Car ahead should have a negative gap.");
        Assert.Equal(-9f, ahead.GapToPlayerSeconds, precision: 3);
    }

    [Fact]
    public void CarBehind_HasPositiveGap()
    {
        // Car 1 is 10 % of a 90-second lap behind → +9 s gap
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.50f, 1, 1),
            (1, 0.40f, 1, 2),   // 10 % behind player → +9 s
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1) };

        var result = IRacingRelativeCalculator.Compute(snapshot, drivers);

        var behind = result.Entries.Single(e => !e.IsPlayer);
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

        var result = IRacingRelativeCalculator.Compute(snapshot, drivers);
        var gaps   = result.Entries.Select(e => e.GapToPlayerSeconds).ToList();

        // Gaps must be monotonically non-decreasing.
        for (int i = 1; i < gaps.Count; i++)
            Assert.True(gaps[i] >= gaps[i - 1],
                $"Entry {i} gap ({gaps[i]}) should be >= entry {i - 1} gap ({gaps[i - 1]}).");

        // Player gap is exactly 0.
        Assert.Equal(0f, result.Entries.Single(e => e.IsPlayer).GapToPlayerSeconds);
    }

    // ── Start/finish-line wrap-around ─────────────────────────────────────────

    [Fact]
    public void WrapAround_CarJustBehindAcrossLine_SmallPositiveGap()
    {
        // Player at 98 %, car 1 at 2 % on the SAME lap.
        // The car has NOT yet crossed the line — it is 4 % of the lap BEHIND the player.
        // delta = 0.02 − 0.98 = −0.96 → < −0.5 → add 1 → +0.04 → gap = +3.6 s (behind).
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.98f, 5, 1),
            (1, 0.02f, 5, 2),   // 4 % behind after wrap → +3.6 s
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1) };

        var result = IRacingRelativeCalculator.Compute(snapshot, drivers);
        var carGap = result.Entries.Single(e => !e.IsPlayer).GapToPlayerSeconds;

        Assert.True(carGap > 0f, "Car behind S/F line should have positive gap.");
        Assert.Equal(3.6f, carGap, precision: 3);
    }

    [Fact]
    public void WrapAround_CarJustAheadAcrossLine_SmallNegativeGap()
    {
        // Player at 2 %, car 1 at 98 % — the car just crossed the S/F line and is 4 % AHEAD.
        // delta = 0.98 − 0.02 = +0.96 → > +0.5 → subtract 1 → −0.04 → gap = −3.6 s (ahead).
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.02f, 5, 2),
            (1, 0.98f, 4, 1),   // 4 % ahead after wrap → −3.6 s
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1) };

        var result = IRacingRelativeCalculator.Compute(snapshot, drivers);
        var carGap = result.Entries.Single(e => !e.IsPlayer).GapToPlayerSeconds;

        Assert.True(carGap < 0f, "Car ahead of S/F line should have negative gap.");
        Assert.Equal(-3.6f, carGap, precision: 3);
    }

    [Fact]
    public void WrapAround_TwoCarsSymmetricAroundLine_GapsAreEqual_OppositeSign()
    {
        // Player at 0.5, one car at 0.9 (+0.4 * 60 = +24 s), one car at 0.1 (-0.4 * 60 = -24 s).
        var snapshot = MakeSnapshot(0, 60f, [
            (0, 0.50f, 3, 2),
            (1, 0.90f, 3, 3),   // +0.4 * 60 = +24 s
            (2, 0.10f, 3, 1),   // −0.4 * 60 = −24 s
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1), MakeDriver(2) };

        var result    = IRacingRelativeCalculator.Compute(snapshot, drivers);
        var gapCar1   = result.Entries.Single(e => e.CarNumber == "1").GapToPlayerSeconds;
        var gapCar2   = result.Entries.Single(e => e.CarNumber == "2").GapToPlayerSeconds;

        Assert.Equal( 24f, gapCar1, precision: 3);
        Assert.Equal(-24f, gapCar2, precision: 3);
    }

    // ── Lap difference ────────────────────────────────────────────────────────

    [Fact]
    public void LapDifference_IsCorrectlyRecorded()
    {
        // Car 1 is a lap ahead of the player.
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.5f, 3, 2),
            (1, 0.5f, 4, 1),   // same pct, one lap ahead → lapDiff = +1
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1) };

        var result    = IRacingRelativeCalculator.Compute(snapshot, drivers);
        var lapDiff   = result.Entries.Single(e => !e.IsPlayer).LapDifference;

        Assert.Equal(1, lapDiff);
    }

    [Fact]
    public void LapDifference_LappedCar_IsNegative()
    {
        // Car 1 is a lap behind the player.
        var snapshot = MakeSnapshot(0, 90f, [
            (0, 0.5f, 3, 1),
            (1, 0.5f, 2, 2),   // one lap behind → lapDiff = −1
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1) };

        var result  = IRacingRelativeCalculator.Compute(snapshot, drivers);
        var lapDiff = result.Entries.Single(e => !e.IsPlayer).LapDifference;

        Assert.Equal(-1, lapDiff);
    }

    // ── Window selection ──────────────────────────────────────────────────────

    [Fact]
    public void OutputContainsAtMost15Entries()
    {
        // Create 20 cars spread evenly around the track.
        var cars = Enumerable.Range(0, 20)
            .Select(i => (carIdx: i, pct: i / 20f, lap: 1, pos: i + 1))
            .ToArray();

        var snapshot = MakeSnapshot(0, 90f, cars);
        var drivers  = Enumerable.Range(0, 20).Select(MakeDriver).ToArray();

        var result = IRacingRelativeCalculator.Compute(snapshot, drivers);

        Assert.True(result.Entries.Count <= 15);
    }

    [Fact]
    public void PlayerIsAlwaysInTheWindow()
    {
        // 20 cars; player (car 0) is at one extreme.
        var cars = Enumerable.Range(0, 20)
            .Select(i => (carIdx: i, pct: i / 20f, lap: 1, pos: i + 1))
            .ToArray();

        var snapshot = MakeSnapshot(0, 90f, cars);
        var drivers  = Enumerable.Range(0, 20).Select(MakeDriver).ToArray();

        var result = IRacingRelativeCalculator.Compute(snapshot, drivers);

        Assert.Contains(result.Entries, e => e.IsPlayer);
    }

    // ── Filtering ─────────────────────────────────────────────────────────────

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

        var result = IRacingRelativeCalculator.Compute(snapshot, drivers);

        Assert.DoesNotContain(result.Entries, e => e.CarNumber == "1");
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

        var result = IRacingRelativeCalculator.Compute(snapshot, drivers);

        Assert.DoesNotContain(result.Entries, e => e.CarNumber == "1");
    }

    [Fact]
    public void CarsWithNegativePct_AreExcluded()
    {
        // Negative pct signals that the car is not on track (in pit box, not started, etc.)
        var snapshot = MakeSnapshot(0, 90f, [
            (0,  0.5f, 1, 1),
            (1, -1.0f, 0, 0),   // not on track
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1) };

        var result = IRacingRelativeCalculator.Compute(snapshot, drivers);

        Assert.DoesNotContain(result.Entries, e => e.CarNumber == "1");
    }

    // ── Driver info join ──────────────────────────────────────────────────────

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

        var result = IRacingRelativeCalculator.Compute(snapshot, drivers);

        var alice = result.Entries.Single(e => e.IsPlayer);
        Assert.Equal("Alice",  alice.DriverName);
        Assert.Equal("99",     alice.CarNumber);
        Assert.Equal(2400,     alice.IRating);
        Assert.Equal(LicenseClass.A, alice.LicenseClass);
        Assert.Equal("A 4.12", alice.LicenseLevel);

        var bob = result.Entries.Single(e => !e.IsPlayer);
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

        var result  = IRacingRelativeCalculator.Compute(snapshot, drivers);
        var unknown = result.Entries.Single(e => !e.IsPlayer);

        Assert.Equal("1", unknown.CarNumber);   // falls back to carIdx.ToString()
        Assert.Equal(string.Empty, unknown.DriverName);
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void PlayerNotOnTrack_ReturnsUpToMaxEntries()
    {
        // Player's pct is -1 (not on track) — calculator should return whatever is available.
        var snapshot = MakeSnapshot(0, 90f, [
            (1, 0.3f, 1, 1),
            (2, 0.6f, 1, 2),
            // car 0 (player) is absent → defaults to pct = −1
        ]);
        var drivers = new[] { MakeDriver(0), MakeDriver(1), MakeDriver(2) };

        // Should not throw; may return up to 15 entries.
        var result = IRacingRelativeCalculator.Compute(snapshot, drivers);

        Assert.True(result.Entries.Count <= 15);
        Assert.DoesNotContain(result.Entries, e => e.IsPlayer);
    }

    [Fact]
    public void SingleCar_IsPlayerOnly_ReturnsOneEntry()
    {
        var snapshot = MakeSnapshot(0, 90f, [(0, 0.5f, 1, 1)]);
        var drivers  = new[] { MakeDriver(0) };

        var result = IRacingRelativeCalculator.Compute(snapshot, drivers);

        Assert.Single(result.Entries);
        Assert.True(result.Entries[0].IsPlayer);
        Assert.Equal(0f, result.Entries[0].GapToPlayerSeconds);
    }
}
