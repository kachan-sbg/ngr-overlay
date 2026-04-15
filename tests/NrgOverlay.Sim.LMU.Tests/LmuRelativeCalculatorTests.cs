using NrgOverlay.Sim.Contracts;
using NrgOverlay.Sim.LMU;
using NrgOverlay.Sim.LMU.SharedMemory;

namespace NrgOverlay.Sim.LMU.Tests;

/// <summary>
/// Unit tests for <see cref="LmuRelativeCalculator"/>.
/// Tests focus on the LMU lap-distance-to-percentage normalisation
/// and the windowed relative list construction.
/// All tests are pure in-memory вЂ” no LMU process or shared memory required.
/// </summary>
public class LmuRelativeCalculatorTests
{
    private const double TrackLength = 6000.0; // 6 km
    private const double EstLap      = 120.0;  // 2-minute lap

    // в”Ђв”Ђ Factory helpers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>
    /// Creates an <see cref="LmuVehicleScoring"/> entry for testing.
    /// <paramref name="isPlayer"/> = true sets the <c>IsPlayer</c> flag so the
    /// calculator can locate the player without a slot ID lookup.
    /// </summary>
    private static LmuVehicleScoring MakeVehicle(
        int    slotId,
        string driverName,
        double lapDistMeters,
        int    totalLaps,
        int    place        = 0,
        bool   inGarage     = false,
        byte   underYellow  = 0,
        bool   isPlayer     = false,
        string vehicleClass = "LMH")
    {
        return new LmuVehicleScoring
        {
            Id            = slotId,
            DriverName    = driverName,
            VehicleName   = "LMH_TestCar",
            TotalLaps     = (short)totalLaps,
            LapDist       = lapDistMeters,
            Place         = (byte)(place > 0 && place <= 255 ? place : 0),
            InGarageStall = (byte)(inGarage ? 1 : 0),
            UnderYellow   = underYellow,
            IsPlayer      = (byte)(isPlayer ? 1 : 0),
            VehicleClass  = vehicleClass,
            UpgradePack   = new byte[16],
            PitGroup      = "",
            Expansion     = new byte[4],
        };
    }

    private static LmuDriverSnapshot MakeDriver(int slotId, string name, string vehicleClass = "LMH")
        => new(slotId, name, slotId.ToString(), vehicleClass,
               vehicleClass.GetHashCode(), null, false);

    // в”Ђв”Ђ Tests в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void Compute_PlayerAlone_ReturnsOneEntry()
    {
        var vehicles = new[]
        {
            MakeVehicle(1, "Player", 3000.0, 5, place: 1, isPlayer: true),
        };
        var drivers = new[] { MakeDriver(1, "Player") };

        var result = LmuRelativeCalculator.Compute(vehicles, drivers, 1, TrackLength, EstLap);

        Assert.Single(result.Relative.Entries);
        Assert.True(result.Relative.Entries[0].IsPlayer);
    }

    [Fact]
    public void Compute_LapDistNormalization_CorrectGap()
    {
        // Player at 3000 m (50 % of 6 km track).
        // Car 2 at 3600 m (60 % of track) = 10% ahead = 0.1 Г— 120s = 12s ahead (negative).
        var vehicles = new[]
        {
            MakeVehicle(1, "Player", 3000.0, 5, place: 2, isPlayer: true),
            MakeVehicle(2, "Car2",   3600.0, 5, place: 1),
        };
        var drivers = new[]
        {
            MakeDriver(1, "Player"),
            MakeDriver(2, "Car2"),
        };

        var result = LmuRelativeCalculator.Compute(vehicles, drivers, 1, TrackLength, EstLap);

        var car2Entry = result.Relative.Entries.First(e => e.DriverName == "Car2");
        // Car2 is 600 m = 10% of lap ahead в†’ gap в‰€ -12 s
        Assert.True(car2Entry.GapToPlayerSeconds < 0f, "Car ahead should have negative gap");
        Assert.InRange(car2Entry.GapToPlayerSeconds, -13f, -11f);
    }

    [Fact]
    public void Compute_WrapAtStartFinish_GapIsSmall()
    {
        // Player at 100 m (just past S/F), Car 2 at 5900 m (just before S/F).
        // Their on-track gap is 200 m = 200/6000 * 120s в‰€ 4s ahead (negative).
        var vehicles = new[]
        {
            MakeVehicle(1, "Player", 100.0,  5, isPlayer: true),
            MakeVehicle(2, "Car2",   5900.0, 4), // one lap behind вЂ” same total lap count
        };
        var drivers = new[] { MakeDriver(1, "Player"), MakeDriver(2, "Car2") };

        var result = LmuRelativeCalculator.Compute(vehicles, drivers, 1, TrackLength, EstLap);

        var car2 = result.Relative.Entries.First(e => e.DriverName == "Car2");
        // Should detect wrap: Car2 is actually ~3.3 s ahead (not 196 m behind = 196/6000*120в‰€3.9s)
        Assert.True(Math.Abs(car2.GapToPlayerSeconds) < 5f,
            "S/F wrap should produce small gap, not full-lap gap");
    }

    [Fact]
    public void Compute_GarageVehicle_Excluded()
    {
        var vehicles = new[]
        {
            MakeVehicle(1, "Player",   3000.0, 5, isPlayer: true),
            MakeVehicle(2, "InGarage", 0.0,   5, inGarage: true),
        };
        var drivers = new[] { MakeDriver(1, "Player"), MakeDriver(2, "InGarage") };

        var result = LmuRelativeCalculator.Compute(vehicles, drivers, 1, TrackLength, EstLap);

        Assert.DoesNotContain(result.Relative.Entries, e => e.DriverName == "InGarage");
    }

    [Fact]
    public void Compute_LicenseIsUnknown_ForAllEntries()
    {
        var vehicles = new[]
        {
            MakeVehicle(1, "Player", 3000.0, 5, isPlayer: true),
            MakeVehicle(2, "Car2",   3100.0, 5),
        };
        var drivers = new[] { MakeDriver(1, "Player"), MakeDriver(2, "Car2") };

        var result = LmuRelativeCalculator.Compute(vehicles, drivers, 1, TrackLength, EstLap);

        Assert.All(result.Relative.Entries, e =>
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
            MakeVehicle(1, "LMH1",  3000.0, 5, place: 1, isPlayer: true,  vehicleClass: "LMH"),
            MakeVehicle(2, "LMDh1", 2900.0, 5, place: 2, vehicleClass: "LMDh"),
            MakeVehicle(3, "LMDh2", 2800.0, 5, place: 3, vehicleClass: "LMDh"),
        };
        var drivers = new[]
        {
            MakeDriver(1, "LMH1",  vehicleClass: "LMH"),
            MakeDriver(2, "LMDh1", vehicleClass: "LMDh"),
            MakeDriver(3, "LMDh2", vehicleClass: "LMDh"),
        };

        var result = LmuRelativeCalculator.Compute(vehicles, drivers, 1, TrackLength, EstLap);

        var lmdhEntries = result.Relative.Entries.Where(e => e.CarClass == "LMDh").ToList();
        Assert.Equal(2, lmdhEntries.Count);

        // Class positions should be 1 and 2 within LMDh.
        var classPosSet = lmdhEntries.Select(e => e.ClassPosition).OrderBy(x => x).ToList();
        Assert.Equal([1, 2], classPosSet);
    }

    [Fact]
    public void Compute_TrackLengthZero_ReturnsEmpty()
    {
        var vehicles = new[] { MakeVehicle(1, "Player", 100.0, 1, isPlayer: true) };
        var drivers  = new[] { MakeDriver(1, "Player") };

        var result = LmuRelativeCalculator.Compute(vehicles, drivers, 1, 0.0, EstLap);

        Assert.Empty(result.Relative.Entries);
    }

    [Fact]
    public void Compute_PlayerNotInVehicles_ReturnsEmpty()
    {
        // No vehicle has IsPlayer set вЂ” player cannot be located.
        var vehicles = new[]
        {
            MakeVehicle(1, "Car1", 1000.0, 3),
            MakeVehicle(2, "Car2", 2000.0, 3),
        };
        var drivers = new[] { MakeDriver(1, "Car1"), MakeDriver(2, "Car2") };

        var result = LmuRelativeCalculator.Compute(vehicles, drivers, 99, TrackLength, EstLap);

        Assert.Empty(result.Relative.Entries);
    }
}

