using SimOverlay.Sim.Contracts;
using SimOverlay.Sim.LMU;
using SimOverlay.Sim.LMU.SharedMemory;

namespace SimOverlay.Sim.LMU.Tests;

/// <summary>
/// Unit tests for <see cref="LmuRelativeCalculator"/>.
/// Tests focus on the rF2-specific lap-distance-to-percentage normalisation
/// and the windowed relative list construction.
/// All tests are pure in-memory — no LMU process or shared memory required.
/// </summary>
public class LmuRelativeCalculatorTests
{
    private const double TrackLength = 6000.0; // 6 km
    private const double EstLap      = 120.0;  // 2-minute lap

    // ── Factory helpers ───────────────────────────────────────────────────────

    private static Rf2VehicleScoring MakeVehicle(
        int    slotId,
        string driverName,
        double lapDistMeters,
        int    totalLaps,
        int    place         = 0,
        bool   inGarage      = false,
        byte   underYellow   = 0)
    {
        // Build the expansion bytes for Place (V02 extension at [0..3]).
        var expansion = new byte[48];
        BitConverter.TryWriteBytes(expansion.AsSpan(0, 4), place);

        return new Rf2VehicleScoring
        {
            Id            = slotId,
            DriverName    = driverName,
            VehicleName   = "LMH_TestCar",
            TotalLaps     = (short)totalLaps,
            LapDist       = lapDistMeters,
            InGarageStall = (byte)(inGarage ? 1 : 0),
            UnderYellow   = underYellow,
            Expansion     = expansion,
            Pos           = [0.0, 0.0, 0.0],
            LocalVel      = [0.0, 0.0, 0.0],
            LocalAccel    = [0.0, 0.0, 0.0],
            Ori           = [1,0,0, 0,1,0, 0,0,1],
            LocalRot      = [0.0, 0.0, 0.0],
            LocalRotAccel = [0.0, 0.0, 0.0],
            UpgradePack   = new byte[16],
            PitGroup      = "",
        };
    }

    private static LmuDriverSnapshot MakeDriver(int slotId, string name, string vehicleClass = "LMH")
        => new(slotId, name, slotId.ToString(), vehicleClass,
               vehicleClass.GetHashCode(), null, false);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Compute_PlayerAlone_ReturnsOneEntry()
    {
        var vehicles = new[]
        {
            MakeVehicle(1, "Player", 3000.0, 5, place: 1),
        };
        var drivers  = new[] { MakeDriver(1, "Player") };

        var result = LmuRelativeCalculator.Compute(vehicles, drivers, 1, TrackLength, EstLap);

        Assert.Single(result.Entries);
        Assert.True(result.Entries[0].IsPlayer);
    }

    [Fact]
    public void Compute_LapDistNormalization_CorrectGap()
    {
        // Player at 3000 m (50 % of 6 km track).
        // Car 2 at 3600 m (60 % of track) = 10% ahead = 0.1 × 120s = 12s ahead (negative).
        var vehicles = new[]
        {
            MakeVehicle(1, "Player", 3000.0, 5, place: 2),
            MakeVehicle(2, "Car2",   3600.0, 5, place: 1),
        };
        var drivers = new[]
        {
            MakeDriver(1, "Player"),
            MakeDriver(2, "Car2"),
        };

        var result = LmuRelativeCalculator.Compute(vehicles, drivers, 1, TrackLength, EstLap);

        var car2Entry = result.Entries.First(e => e.DriverName == "Car2");
        // Car2 is 600 m = 10% of lap ahead → gap ≈ -12 s
        Assert.True(car2Entry.GapToPlayerSeconds < 0f, "Car ahead should have negative gap");
        Assert.InRange(car2Entry.GapToPlayerSeconds, -13f, -11f);
    }

    [Fact]
    public void Compute_WrapAtStartFinish_GapIsSmall()
    {
        // Player at 100 m (just past S/F), Car 2 at 5900 m (just before S/F).
        // Their on-track gap is 200 m = 200/6000 * 120s ≈ 4s ahead (negative).
        var vehicles = new[]
        {
            MakeVehicle(1, "Player", 100.0,  5),
            MakeVehicle(2, "Car2",   5900.0, 4), // one lap behind — same total lap count
        };
        var drivers = new[] { MakeDriver(1, "Player"), MakeDriver(2, "Car2") };

        var result = LmuRelativeCalculator.Compute(vehicles, drivers, 1, TrackLength, EstLap);

        var car2 = result.Entries.First(e => e.DriverName == "Car2");
        // Should detect wrap: Car2 is actually ~3.3 s ahead (not 196 m behind = 196/6000*120≈3.9s)
        Assert.True(Math.Abs(car2.GapToPlayerSeconds) < 5f,
            "S/F wrap should produce small gap, not full-lap gap");
    }

    [Fact]
    public void Compute_GarageVehicle_Excluded()
    {
        var vehicles = new[]
        {
            MakeVehicle(1, "Player",  3000.0, 5),
            MakeVehicle(2, "InGarage", 0.0,   5, inGarage: true),
        };
        var drivers = new[] { MakeDriver(1, "Player"), MakeDriver(2, "InGarage") };

        var result = LmuRelativeCalculator.Compute(vehicles, drivers, 1, TrackLength, EstLap);

        Assert.DoesNotContain(result.Entries, e => e.DriverName == "InGarage");
    }

    [Fact]
    public void Compute_LicenseIsUnknown_ForAllEntries()
    {
        var vehicles = new[]
        {
            MakeVehicle(1, "Player", 3000.0, 5),
            MakeVehicle(2, "Car2",   3100.0, 5),
        };
        var drivers = new[] { MakeDriver(1, "Player"), MakeDriver(2, "Car2") };

        var result = LmuRelativeCalculator.Compute(vehicles, drivers, 1, TrackLength, EstLap);

        Assert.All(result.Entries, e =>
        {
            Assert.Equal(LicenseClass.Unknown, e.LicenseClass);
            Assert.Equal("", e.LicenseLevel);
            Assert.Equal(0, e.IRating);
        });
    }

    [Fact]
    public void Compute_MultiClass_SetsCarClassAndClassPosition()
    {
        var vehicles = new[]
        {
            MakeVehicle(1, "LMH1",   3000.0, 5, place: 1),
            MakeVehicle(2, "LMDh1",  2900.0, 5, place: 2),
            MakeVehicle(3, "LMDh2",  2800.0, 5, place: 3),
        };
        var drivers = new[]
        {
            MakeDriver(1, "LMH1",  vehicleClass: "LMH"),
            MakeDriver(2, "LMDh1", vehicleClass: "LMDh"),
            MakeDriver(3, "LMDh2", vehicleClass: "LMDh"),
        };

        var result = LmuRelativeCalculator.Compute(vehicles, drivers, 1, TrackLength, EstLap);

        var lmdhEntries = result.Entries.Where(e => e.CarClass == "LMDh").ToList();
        Assert.Equal(2, lmdhEntries.Count);

        // Class positions should be 1 and 2 within LMDh.
        var classPosSet = lmdhEntries.Select(e => e.ClassPosition).OrderBy(x => x).ToList();
        Assert.Equal([1, 2], classPosSet);
    }

    [Fact]
    public void Compute_TrackLengthZero_ReturnsEmpty()
    {
        var vehicles = new[] { MakeVehicle(1, "Player", 100.0, 1) };
        var drivers  = new[] { MakeDriver(1, "Player") };

        var result = LmuRelativeCalculator.Compute(vehicles, drivers, 1, 0.0, EstLap);

        Assert.Empty(result.Entries);
    }

    [Fact]
    public void Compute_PlayerNotInVehicles_ReturnsEmpty()
    {
        // Player slot 99 not in vehicle list.
        // A relative overlay requires a player reference to compute meaningful gaps,
        // so the result is empty when the player cannot be located.
        var vehicles = new[]
        {
            MakeVehicle(1, "Car1", 1000.0, 3),
            MakeVehicle(2, "Car2", 2000.0, 3),
        };
        var drivers = new[] { MakeDriver(1, "Car1"), MakeDriver(2, "Car2") };

        var result = LmuRelativeCalculator.Compute(vehicles, drivers, 99, TrackLength, EstLap);

        Assert.Empty(result.Entries);
    }
}
