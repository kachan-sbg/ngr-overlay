using BenchmarkDotNet.Attributes;
using SimOverlay.Core;
using SimOverlay.Sim.Contracts;

namespace SimOverlay.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks <see cref="SimDataBus.Publish{T}"/> — called on every data tick
/// (RelativeData at ~10 Hz, DriverData at ~60 Hz).
///
/// Targets:
///   Mean   &lt; 1 µs   (budget: negligible vs. data work)
///   Alloc  = 0 B    (ImmutableArray snapshot read — no allocation on hot path)
/// </summary>
[MemoryDiagnoser]
public class SimDataBusBenchmarks
{
    private SimDataBus _bus1 = null!;
    private SimDataBus _bus3 = null!;
    private RelativeData _relativePayload = null!;
    private DriverData _driverPayload = null!;

    [GlobalSetup]
    public void Setup()
    {
        _relativePayload = new RelativeData { Entries = [] };
        _driverPayload = new DriverData
        {
            Position         = 5,
            Lap              = 12,
            LastLapTime      = TimeSpan.FromSeconds(94.521),
            BestLapTime      = TimeSpan.FromSeconds(93.887),
            LapDeltaVsBestLap = -0.234f,
        };

        // Bus with one subscriber — typical: one overlay per message type
        _bus1 = new SimDataBus();
        _bus1.Subscribe<RelativeData>(_ => { });

        // Bus with three subscribers — all three overlays active
        _bus3 = new SimDataBus();
        _bus3.Subscribe<RelativeData>(_ => { });
        _bus3.Subscribe<RelativeData>(_ => { });
        _bus3.Subscribe<RelativeData>(_ => { });
    }

    /// <summary>One subscriber — most common case (RelativeOverlay listening for RelativeData).</summary>
    [Benchmark(Baseline = true)]
    public void Publish1Subscriber() => _bus1.Publish(_relativePayload);

    /// <summary>Three subscribers — stress case.</summary>
    [Benchmark]
    public void Publish3Subscribers() => _bus3.Publish(_relativePayload);

    /// <summary>No subscribers — publish to a type nobody is listening to.</summary>
    [Benchmark]
    public void PublishNoSubscribers() => _bus1.Publish(_driverPayload);
}
