using SimOverlay.Core.Config;
using SimOverlay.Sim.Contracts;

namespace SimOverlay.Sim.iRacing;

/// <summary>
/// Immutable per-driver data extracted from the iRacing YAML session string.
/// Cached by <see cref="IRacingSessionDecoder"/> and consumed by
/// <see cref="IRacingRelativeCalculator"/> to annotate relative entries.
/// </summary>
internal sealed record DriverSnapshot(
    int    CarIdx,
    string UserName,
    string CarNumber,
    int    IRating,
    LicenseClass LicenseClass,
    string LicenseLevel,
    bool   IsSpectator,
    bool   IsPaceCar,
    int    CarClassId   = 0,
    string CarClass     = "",
    ColorConfig? ClassColor = null);
