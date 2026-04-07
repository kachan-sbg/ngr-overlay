using SimOverlay.Sim.iRacing;

namespace SimOverlay.Sim.iRacing.Tests;

/// <summary>
/// Unit tests for <see cref="FuelConsumptionTracker"/>.
/// All tests are pure in-memory — no SDK or iRacing process required.
/// </summary>
public class FuelConsumptionTrackerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private const int GreenFlags   = 0x0004; // irsdk_green
    private const int CautionFlags = 0x4000; // irsdk_caution

    /// <summary>
    /// Simulates a lap by feeding enough ticks to the tracker to cross the lap boundary.
    /// </summary>
    private static void SimulateLap(
        FuelConsumptionTracker tracker,
        int   lapNumber,
        float fuelAtStart,
        float fuelAtEnd,
        int   flags = GreenFlags,
        int   ticksPerLap = 5)
    {
        // Ticks before the lap boundary.
        var fuelStep = (fuelAtStart - fuelAtEnd) / ticksPerLap;
        var fuel     = fuelAtStart;
        for (int t = 0; t < ticksPerLap - 1; t++)
        {
            tracker.Update(lapNumber - 1, fuel, flags);
            fuel -= fuelStep;
        }
        // Final tick crosses the lap boundary.
        tracker.Update(lapNumber, fuelAtEnd, flags);
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    [Fact]
    public void BeforeAnyLapComplete_AverageIsZero()
    {
        var tracker = new FuelConsumptionTracker();
        tracker.Update(1, 40f, GreenFlags);
        Assert.Equal(0f, tracker.PerLapAverage);
    }

    // ── Green-flag lap recording ──────────────────────────────────────────────

    [Fact]
    public void SingleGreenLap_AverageEqualsConsumption()
    {
        var tracker = new FuelConsumptionTracker();
        // Initialise on lap 1.
        tracker.Update(1, 40f, GreenFlags);
        // Cross to lap 2 with 2.0 L consumed.
        tracker.Update(2, 38f, GreenFlags);

        Assert.Equal(2f, tracker.PerLapAverage, precision: 4);
    }

    [Fact]
    public void TwoGreenLaps_AverageIsCorrect()
    {
        var tracker = new FuelConsumptionTracker();
        tracker.Update(1, 40f, GreenFlags);
        tracker.Update(2, 38f, GreenFlags); // lap 1: 2.0 L
        tracker.Update(3, 36f, GreenFlags); // lap 2: 2.0 L

        Assert.Equal(2f, tracker.PerLapAverage, precision: 4);
    }

    // ── Caution lap exclusion ─────────────────────────────────────────────────

    [Fact]
    public void CautionLap_IsExcludedFromAverage()
    {
        var tracker = new FuelConsumptionTracker();

        // Lap 1 (green): 2.0 L consumed.
        tracker.Update(1, 40f, GreenFlags);
        tracker.Update(2, 38f, GreenFlags);

        // Lap 2 (caution): 1.0 L consumed — must not enter buffer.
        tracker.Update(2, 38f, CautionFlags); // flag raised mid-lap
        tracker.Update(3, 37f, GreenFlags);   // lap boundary

        // Only lap 1 should be in the buffer.
        Assert.Equal(2f, tracker.PerLapAverage, precision: 4);
    }

    [Fact]
    public void MixedGreenAndCautionLaps_AverageExcludesCautionLaps()
    {
        var tracker = new FuelConsumptionTracker();

        // Lap 1 (green): 2.0 L
        tracker.Update(1, 40f, GreenFlags);
        tracker.Update(2, 38f, GreenFlags);

        // Lap 2 (caution): 1.5 L — excluded
        tracker.Update(2, 38f, CautionFlags);
        tracker.Update(3, 36.5f, GreenFlags);

        // Lap 3 (green): 1.8 L
        tracker.Update(4, 34.7f, GreenFlags);

        // Average = (2.0 + 1.8) / 2 = 1.9 L
        Assert.Equal(1.9f, tracker.PerLapAverage, precision: 3);
    }

    // ── Rolling buffer ────────────────────────────────────────────────────────

    [Fact]
    public void BufferCapAt5Laps_OldestIsDropped()
    {
        var tracker = new FuelConsumptionTracker();

        // Lap 0 seed.
        tracker.Update(0, 50f, GreenFlags);

        // 5 green laps consuming 2.0 L each → fills the buffer.
        for (int lap = 1; lap <= 5; lap++)
            tracker.Update(lap, 50f - lap * 2f, GreenFlags);

        var afterFive = tracker.PerLapAverage;
        Assert.Equal(2f, afterFive, precision: 4);

        // 6th lap consumes 3.0 L → oldest (2.0) drops; new average = (2+2+2+2+3)/5 = 2.2
        tracker.Update(6, 50f - 5 * 2f - 3f, GreenFlags);
        Assert.Equal(2.2f, tracker.PerLapAverage, precision: 4);
    }

    // ── Pitstop refuel guard ──────────────────────────────────────────────────

    [Fact]
    public void PitstopRefuel_NegativeConsumption_IsNotRecorded()
    {
        var tracker = new FuelConsumptionTracker();

        // Lap 1 (green, normal): 2.0 L
        tracker.Update(1, 40f, GreenFlags);
        tracker.Update(2, 38f, GreenFlags);

        // Lap 2: fuel goes UP (refuel pit stop) — negative consumption must be ignored.
        tracker.Update(3, 50f, GreenFlags); // refuelled; buffer unchanged

        Assert.Equal(2f, tracker.PerLapAverage, precision: 4);
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsAllState()
    {
        var tracker = new FuelConsumptionTracker();
        tracker.Update(1, 40f, GreenFlags);
        tracker.Update(2, 38f, GreenFlags);
        Assert.True(tracker.PerLapAverage > 0f);

        tracker.Reset();
        Assert.Equal(0f, tracker.PerLapAverage);

        // After reset, a fresh lap should work normally.
        tracker.Update(1, 40f, GreenFlags);
        tracker.Update(2, 38f, GreenFlags);
        Assert.Equal(2f, tracker.PerLapAverage, precision: 4);
    }
}
