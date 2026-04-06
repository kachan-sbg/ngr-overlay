using BenchmarkDotNet.Attributes;
using SimOverlay.Core.Config;

namespace SimOverlay.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks <see cref="OverlayConfig.Resolve"/> — called once per overlay per frame
/// (60 Hz × 3 overlays = 180 calls/sec).
///
/// Targets:
///   ResolveNoOverride   : 0 B alloc  (returns 'this' — no object created)
///   ResolveWithOverride : &lt; 500 B alloc (one new OverlayConfig per call)
/// </summary>
[MemoryDiagnoser]
public class ConfigResolveBenchmarks
{
    private OverlayConfig _noOverride = null!;
    private OverlayConfig _withOverride = null!;
    private OverlayConfig _disabledOverride = null!;

    [GlobalSetup]
    public void Setup()
    {
        _noOverride = new OverlayConfig
        {
            Id      = "relative",
            Width   = 500,
            Height  = 380,
        };

        _withOverride = new OverlayConfig
        {
            Id      = "relative",
            Width   = 500,
            Height  = 380,
            StreamOverride = new StreamOverrideConfig
            {
                Enabled          = true,
                Width            = 600,
                BackgroundColor  = ColorConfig.Black,
                ShowIRating      = false,
            },
        };

        // Override present but disabled — should take the fast path (return this)
        _disabledOverride = new OverlayConfig
        {
            Id     = "relative",
            Width  = 500,
            StreamOverride = new StreamOverrideConfig { Enabled = false },
        };
    }

    /// <summary>No stream override — returns 'this', zero allocations.</summary>
    [Benchmark(Baseline = true)]
    public OverlayConfig ResolveNoOverride() => _noOverride.Resolve(streamModeActive: false);

    /// <summary>Stream mode active, override enabled — allocates one new OverlayConfig.</summary>
    [Benchmark]
    public OverlayConfig ResolveWithOverride() => _withOverride.Resolve(streamModeActive: true);

    /// <summary>Stream mode active but override disabled — takes fast path, zero allocations.</summary>
    [Benchmark]
    public OverlayConfig ResolveOverrideDisabled() => _disabledOverride.Resolve(streamModeActive: true);
}
