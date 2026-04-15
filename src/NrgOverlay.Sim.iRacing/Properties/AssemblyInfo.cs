using System.Runtime.CompilerServices;

// Allow the iRacing test and benchmark projects to access internal types such as
// IRacingRelativeCalculator, TelemetrySnapshot, and DriverSnapshot.
[assembly: InternalsVisibleTo("NrgOverlay.Sim.iRacing.Tests")]
[assembly: InternalsVisibleTo("NrgOverlay.Benchmarks")]

