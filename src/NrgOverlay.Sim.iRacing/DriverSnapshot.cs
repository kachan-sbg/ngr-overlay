using NrgOverlay.Core.Config;
using NrgOverlay.Sim.Contracts;

namespace NrgOverlay.Sim.iRacing;

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
    int    CarClassId     = 0,
    string CarClass       = "",
    ColorConfig? ClassColor = null,
    string TeamName       = "",
    string CarScreenName  = "",
    string ClubName       = "",
    int    FlairId        = 0,
    int    ClubId         = 0,
    int    UserId         = 0,
    string CountryCode    = "");

