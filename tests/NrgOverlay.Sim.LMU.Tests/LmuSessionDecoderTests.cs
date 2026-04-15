using NrgOverlay.Sim.Contracts;
using NrgOverlay.Sim.LMU;
using NrgOverlay.Sim.LMU.SharedMemory;

namespace NrgOverlay.Sim.LMU.Tests;

/// <summary>
/// Unit tests for <see cref="LmuSessionDecoder"/>.
/// Covers session-type mapping, track-wetness mapping, class derivation,
/// and missing-data handling (LicenseClass.Unknown, empty LicenseLevel).
/// </summary>
public class LmuSessionDecoderTests
{
    // в”Ђв”Ђ Session type mapping в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Theory]
    [InlineData(0,  SessionType.Practice)]
    [InlineData(1,  SessionType.Practice)]
    [InlineData(4,  SessionType.Practice)]
    [InlineData(5,  SessionType.Qualify)]
    [InlineData(8,  SessionType.Qualify)]
    [InlineData(9,  SessionType.Warmup)]
    [InlineData(10, SessionType.Race)]
    [InlineData(13, SessionType.Race)]
    public void MapSessionType_ReturnsExpected(int rfSession, SessionType expected)
    {
        Assert.Equal(expected, LmuSessionDecoder.MapSessionType(rfSession));
    }

    // в”Ђв”Ђ Track wetness mapping в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Theory]
    [InlineData(0.00, 1)] // dry
    [InlineData(0.01, 1)] // still dry (< 1/6)
    [InlineData(0.20, 2)] // mostly dry
    [InlineData(0.50, 4)] // lightly wet
    [InlineData(0.99, 6)] // very wet
    [InlineData(1.00, 7)] // extremely wet
    public void MapWetness_MapsCorrectly(double maxWetness, int expectedScale)
    {
        Assert.Equal(expectedScale, LmuSessionDecoder.MapWetness(maxWetness));
    }

    // в”Ђв”Ђ Vehicle class derivation в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void DeriveClass_UsesVehicleClassField_WhenPopulated()
    {
        // VehicleClass is now a direct struct field in LmuVehicleScoring.
        var v = MakeVehicle("LMDh_Porsche_963", vehicleClass: "LMDh");

        Assert.Equal("LMDh", LmuSessionDecoder.DeriveClass(in v));
    }

    [Fact]
    public void DeriveClass_FallsBackToVehicleNamePrefix_WhenVehicleClassEmpty()
    {
        var v = MakeVehicle("LMH_GlickenHaus_007", vehicleClass: "");

        Assert.Equal("LMH", LmuSessionDecoder.DeriveClass(in v));
    }

    [Fact]
    public void DeriveClass_SpaceDelimiter_ExtractsFirstToken()
    {
        var v = MakeVehicle("GT3 Ferrari 296", vehicleClass: "");

        Assert.Equal("GT3", LmuSessionDecoder.DeriveClass(in v));
    }

    // в”Ђв”Ђ Full Decode в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    [Fact]
    public void Decode_ActiveVehicles_ProduceDriverSnapshots()
    {
        var info = MakeScoringInfo(session: 10, numVehicles: 2, trackName: "Le Mans");
        var vehicles = new[]
        {
            MakeVehicle("LMH_Toyota_GR010", id: 1, driverName: "T. Kobayashi"),
            MakeVehicle("LMH_Toyota_GR010", id: 2, driverName: "S. Buemi"),
        };

        var (session, drivers) = LmuSessionDecoder.Decode(info, vehicles);

        Assert.Equal("Le Mans",        session.TrackName);
        Assert.Equal(SessionType.Race, session.SessionType);
        Assert.Equal(2, drivers.Count);
    }

    [Fact]
    public void Decode_LicenseAndRatingAreUnavailable()
    {
        var info     = MakeScoringInfo(session: 10, numVehicles: 1);
        var vehicles = new[] { MakeVehicle("LMH_Car", id: 1, driverName: "Driver A") };

        var (_, drivers) = LmuSessionDecoder.Decode(info, vehicles);

        Assert.Single(drivers);
        Assert.Equal("Driver A", drivers[0].DriverName);
    }

    [Fact]
    public void Decode_InactiveSlots_Excluded()
    {
        var info = MakeScoringInfo(session: 10, numVehicles: 3);
        var vehicles = new[]
        {
            MakeVehicle("LMH_Car", id: 1, driverName: "Active1"),
            new LmuVehicleScoring { Id = 2, DriverName = "", LapDist = -1, UpgradePack = new byte[16], Expansion = new byte[4], PitGroup = "" }, // inactive
            MakeVehicle("LMH_Car", id: 3, driverName: "Active2"),
        };

        var (_, drivers) = LmuSessionDecoder.Decode(info, vehicles);

        Assert.Equal(2, drivers.Count);
        Assert.DoesNotContain(drivers, d => d.DriverName == "");
    }

    [Fact]
    public void Decode_MultipleClasses_CarClassesPopulated()
    {
        var info = MakeScoringInfo(session: 10, numVehicles: 2);
        var vehicles = new[]
        {
            MakeVehicle("LMH_Car",  id: 1, driverName: "A", vehicleClass: "LMH"),
            MakeVehicle("LMDh_Car", id: 2, driverName: "B", vehicleClass: "LMDh"),
        };

        var (session, drivers) = LmuSessionDecoder.Decode(info, vehicles);

        Assert.Equal(2, session.CarClasses.Count);
        var classNames = session.CarClasses.Select(c => c.ClassName).OrderBy(x => x).ToList();
        Assert.Contains("LMH",  classNames);
        Assert.Contains("LMDh", classNames);
    }

    // в”Ђв”Ђ Helpers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private static LmuScoringInfo MakeScoringInfo(
        int    session     = 10,
        int    numVehicles = 0,
        string trackName   = "Test Track",
        double lapDist     = 6000.0)
    {
        return new LmuScoringInfo
        {
            TrackName      = trackName,
            Session        = session,
            NumVehicles    = numVehicles,
            LapDist        = lapDist,
            CurrentET      = 600.0,
            EndET          = 3600.0,
            AmbientTempC   = 22.0,
            TrackTempC     = 30.0,
            MaxPathWetness = 0.0,
            Raining        = 0.0,
            InRealtime     = 1,
            PlayerName     = "TestPlayer",
            SectorFlag     = new byte[3],
            ResultsStreamPtr = new byte[8],
            Expansion      = new byte[187],
            VehiclePointer = new byte[8],
        };
    }

    private static LmuVehicleScoring MakeVehicle(
        string vehicleName,
        int    id           = 1,
        string driverName   = "Driver",
        string vehicleClass = "LMH")
    {
        return new LmuVehicleScoring
        {
            Id           = id,
            DriverName   = driverName,
            VehicleName  = vehicleName,
            VehicleClass = vehicleClass,
            TotalLaps    = 5,
            LapDist      = 3000.0,
            UpgradePack  = new byte[16],
            PitGroup     = "",
            Expansion    = new byte[4],
        };
    }
}

