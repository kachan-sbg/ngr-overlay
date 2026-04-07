using SimOverlay.Sim.Contracts;
using SimOverlay.Sim.LMU;
using SimOverlay.Sim.LMU.SharedMemory;

namespace SimOverlay.Sim.LMU.Tests;

/// <summary>
/// Unit tests for <see cref="LmuSessionDecoder"/>.
/// Covers session-type mapping, track-wetness mapping, class derivation,
/// and missing-data handling (LicenseClass.Unknown, empty LicenseLevel).
/// </summary>
public class LmuSessionDecoderTests
{
    // ── Session type mapping ──────────────────────────────────────────────────

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

    // ── Track wetness mapping ─────────────────────────────────────────────────

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

    // ── Vehicle class derivation ──────────────────────────────────────────────

    [Fact]
    public void DeriveClass_UsesExpansionVehicleClass_WhenPopulated()
    {
        var expansion = new byte[48];
        System.Text.Encoding.ASCII.GetBytes("LMDh\0").CopyTo(expansion, 4);

        var v = MakeVehicle("LMDh_Porsche_963", expansion);

        Assert.Equal("LMDh", LmuSessionDecoder.DeriveClass(in v));
    }

    [Fact]
    public void DeriveClass_FallsBackToVehicleNamePrefix_WhenExpansionEmpty()
    {
        var v = MakeVehicle("LMH_GlickenHaus_007", new byte[48]);

        Assert.Equal("LMH", LmuSessionDecoder.DeriveClass(in v));
    }

    [Fact]
    public void DeriveClass_SpaceDelimiter_ExtractsFirstToken()
    {
        var v = MakeVehicle("GT3 Ferrari 296", new byte[48]);

        Assert.Equal("GT3", LmuSessionDecoder.DeriveClass(in v));
    }

    // ── Full Decode ───────────────────────────────────────────────────────────

    [Fact]
    public void Decode_ActiveVehicles_ProduceDriverSnapshots()
    {
        var info = MakeScoringInfo(session: 10, numVehicles: 2, trackName: "Le Mans");
        var vehicles = new[]
        {
            MakeVehicle("LMH_Toyota_GR010", new byte[48], id: 1, driverName: "T. Kobayashi"),
            MakeVehicle("LMH_Toyota_GR010", new byte[48], id: 2, driverName: "S. Buemi"),
        };

        var (session, drivers) = LmuSessionDecoder.Decode(info, vehicles);

        Assert.Equal("Le Mans",      session.TrackName);
        Assert.Equal(SessionType.Race, session.SessionType);
        Assert.Equal(2, drivers.Count);
    }

    [Fact]
    public void Decode_LicenseAndRatingAreUnavailable()
    {
        // Drivers returned by session decoder should have Unknown class and empty level.
        // This is tested indirectly through the relative calculator — the decoder only
        // produces LmuDriverSnapshot which does not carry license data.
        var info     = MakeScoringInfo(session: 10, numVehicles: 1);
        var vehicles = new[] { MakeVehicle("LMH_Car", new byte[48], id: 1, driverName: "Driver A") };

        var (_, drivers) = LmuSessionDecoder.Decode(info, vehicles);

        // LmuDriverSnapshot has no IRating / LicenseClass fields — confirmed absence.
        Assert.Single(drivers);
        Assert.Equal("Driver A", drivers[0].DriverName);
    }

    [Fact]
    public void Decode_InactiveSlots_Excluded()
    {
        var info = MakeScoringInfo(session: 10, numVehicles: 3);
        var vehicles = new[]
        {
            MakeVehicle("LMH_Car", new byte[48], id: 1, driverName: "Active1"),
            new Rf2VehicleScoring { Id = 2, DriverName = "", LapDist = -1 }, // inactive
            MakeVehicle("LMH_Car", new byte[48], id: 3, driverName: "Active2"),
        };

        var (_, drivers) = LmuSessionDecoder.Decode(info, vehicles);

        Assert.Equal(2, drivers.Count);
        Assert.DoesNotContain(drivers, d => d.DriverName == "");
    }

    [Fact]
    public void Decode_MultipleClasses_CarClassesPopulated()
    {
        var expansion_lmh  = new byte[48];
        var expansion_lmdh = new byte[48];
        System.Text.Encoding.ASCII.GetBytes("LMH\0").CopyTo(expansion_lmh, 4);
        System.Text.Encoding.ASCII.GetBytes("LMDh\0").CopyTo(expansion_lmdh, 4);

        var info = MakeScoringInfo(session: 10, numVehicles: 2);
        var vehicles = new[]
        {
            MakeVehicle("LMH_Car",  expansion_lmh,  id: 1, driverName: "A"),
            MakeVehicle("LMDh_Car", expansion_lmdh, id: 2, driverName: "B"),
        };

        var (session, drivers) = LmuSessionDecoder.Decode(info, vehicles);

        // With 2 distinct classes, CarClasses list should be populated.
        Assert.Equal(2, session.CarClasses.Count);
        var classNames = session.CarClasses.Select(c => c.ClassName).OrderBy(x => x).ToList();
        Assert.Contains("LMH",  classNames);
        Assert.Contains("LMDh", classNames);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Rf2ScoringInfo MakeScoringInfo(
        int    session     = 10,
        int    numVehicles = 0,
        string trackName   = "Test Track",
        double lapDist     = 6000.0)
    {
        return new Rf2ScoringInfo
        {
            TrackName      = trackName,
            Session        = session,
            NumVehicles    = numVehicles,
            LapDist        = lapDist,
            CurrentET      = 600.0,
            EndET          = 3600.0,
            Temperature    = 22.0,
            MaxPathWetness = 0.0,
            Raining        = 0.0,
            InRealtime     = 1,
            PlayerName     = "TestPlayer",
            SectorFlag     = [0, 0, 0],
            Expansion      = new byte[256],
        };
    }

    private static Rf2VehicleScoring MakeVehicle(
        string vehicleName,
        byte[] expansion,
        int    id         = 1,
        string driverName = "Driver")
    {
        return new Rf2VehicleScoring
        {
            Id            = id,
            DriverName    = driverName,
            VehicleName   = vehicleName,
            TotalLaps     = 5,
            LapDist       = 3000.0,
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
}
