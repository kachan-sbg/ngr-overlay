// NrgOverlay Performance Benchmarks
//
// Run (Release build required for accurate results):
//   dotnet run -c Release --project tests/NrgOverlay.Benchmarks
//
// To run a specific benchmark class:
//   dotnet run -c Release --project tests/NrgOverlay.Benchmarks -- --filter *Relative*
//
// Results are written to: BenchmarkDotNet.Artifacts/results/
// A JSON export is included automatically for baseline comparisons.
//
// Targets вЂ” regressions must be investigated before merging:
//   RelativeCalculatorBenchmarks.Compute40Cars  < 50 Вµs
//   SimDataBusBenchmarks.Publish1Subscriber     < 1 Вµs,   0 B alloc
//   ConfigResolveBenchmarks.ResolveNoOverride   0 B alloc (returns this)
//   ConfigResolveBenchmarks.ResolveWithOverride < 500 B alloc

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Running;

var config = DefaultConfig.Instance
    .AddExporter(JsonExporter.Full);

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);

