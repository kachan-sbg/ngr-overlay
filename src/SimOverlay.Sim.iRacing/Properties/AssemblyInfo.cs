using System.Runtime.CompilerServices;

// Allow the iRacing test project to access internal types such as
// IRacingRelativeCalculator, TelemetrySnapshot, and DriverSnapshot.
[assembly: InternalsVisibleTo("SimOverlay.Sim.iRacing.Tests")]
