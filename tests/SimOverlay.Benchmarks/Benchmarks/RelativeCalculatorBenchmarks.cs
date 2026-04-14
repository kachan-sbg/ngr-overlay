using BenchmarkDotNet.Attributes;
using SimOverlay.Sim.Contracts;
using SimOverlay.Sim.iRacing;

namespace SimOverlay.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks <see cref="IRacingRelativeCalculator.Compute"/> — the data-path
/// computation that runs at ~10 Hz whenever iRacing is in session.
///
/// Targets:
///   Mean (40 cars) &lt; 50 µs  (budget: 100 µs per tick, 50% headroom)
///   Alloc           measured  (not zero — builds Dictionary + List; acceptable at 10 Hz)
/// </summary>
[MemoryDiagnoser]
public class RelativeCalculatorBenchmarks
{
    private const int MaxCars = 64;

    private TelemetrySnapshot _snapshot40 = null!;
    private TelemetrySnapshot _snapshot15 = null!;
    private TelemetrySnapshot _snapshot1 = null!;

    private IReadOnlyList<DriverSnapshot> _drivers40 = null!;
    private IReadOnlyList<DriverSnapshot> _drivers15 = null!;
    private IReadOnlyList<DriverSnapshot> _drivers1 = null!;

    private readonly CarStateTracker           _carState   = new();
    private          IRacingRelativeCalculator _calculator = null!;

    [GlobalSetup]
    public void Setup()
    {
        _snapshot40 = MakeSnapshot(playerIdx: 0, carCount: 40);
        _snapshot15 = MakeSnapshot(playerIdx: 0, carCount: 15);
        _snapshot1  = MakeSnapshot(playerIdx: 0, carCount: 1);

        _drivers40 = MakeDrivers(40);
        _drivers15 = MakeDrivers(15);
        _drivers1  = MakeDrivers(1);

        _calculator = new IRacingRelativeCalculator();
    }

    /// <summary>Worst case: full 40-car field (e.g. iRacing oval with AI).</summary>
    [Benchmark(Baseline = true)]
    public (RelativeData, StandingsData) Compute40Cars() =>
        _calculator.Compute(_snapshot40, _drivers40, _carState);

    /// <summary>Typical road-course field size.</summary>
    [Benchmark]
    public (RelativeData, StandingsData) Compute15Cars() =>
        _calculator.Compute(_snapshot15, _drivers15, _carState);

    /// <summary>Edge case: single car (e.g. testing alone).</summary>
    [Benchmark]
    public (RelativeData, StandingsData) Compute1Car() =>
        _calculator.Compute(_snapshot1, _drivers1, _carState);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TelemetrySnapshot MakeSnapshot(int playerIdx, int carCount)
    {
        var pcts = new float[MaxCars];
        var laps = new int[MaxCars];
        var pos  = new int[MaxCars];

        for (int i = 0; i < MaxCars; i++) pcts[i] = -1f;

        for (int i = 0; i < carCount; i++)
        {
            // Spread cars evenly around the track; player sits at 0.5
            pcts[i] = (0.5f + i * (1f / carCount)) % 1f;
            laps[i] = 5;
            pos[i]  = i + 1;
        }

        var surfaces = new int[MaxCars];
        for (int i = 0; i < MaxCars; i++) surfaces[i] = i < carCount ? 3 : -1; // OnTrack / NotInWorld

        return new TelemetrySnapshot(
            PlayerCarIdx:     playerIdx,
            LapDistPcts:      pcts,
            Positions:        pos,
            Laps:             laps,
            EstimatedLapTime: 90f,
            BestLapTimes:     new float[MaxCars],
            LastLapTimes:     new float[MaxCars],
            TrackSurfaces:    surfaces,
            OnPitRoad:        new bool[MaxCars],
            F2Times:          new float[MaxCars],
            PitStopCounts:    new int[MaxCars],
            PitLaneTimes:     new float[MaxCars],
            TireCompounds:    new int[MaxCars]);
    }

    private static IReadOnlyList<DriverSnapshot> MakeDrivers(int count)
    {
        var list = new List<DriverSnapshot>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(new DriverSnapshot(
                CarIdx:       i,
                UserName:     $"Driver{i:D2}",
                CarNumber:    $"{i:D2}",
                IRating:      3000 + i * 50,
                LicenseClass: (LicenseClass)(i % 7),
                LicenseLevel: $"{(char)('A' + i % 5)}{i % 9 + 1}.{i % 99:D2}",
                IsSpectator:  false,
                IsPaceCar:    false));
        }
        return list;
    }
}
