using SimOverlay.Core.Config;
using SimOverlay.Sim.Contracts;

namespace SimOverlay.Sim.LMU;

/// <summary>
/// Immutable per-driver data extracted from the rF2/LMU scoring vehicle entry.
/// Cached by <see cref="LmuPoller"/> and consumed by <see cref="LmuRelativeCalculator"/>
/// to annotate relative entries.
/// <para>
/// LMU has no iRating, license class, or incident system.  Those fields carry
/// their defined unavailable sentinels: <see cref="IRating"/> = 0,
/// <see cref="License"/> = <see cref="LicenseClass.Unknown"/>, <see cref="LicenseLevel"/> = "".
/// </para>
/// </summary>
internal sealed record LmuDriverSnapshot(
    int          SlotId,
    string       DriverName,
    string       CarNumber,    // slot ID as string (rF2 has no dedicated car-number field)
    string       VehicleClass, // from V02 expansion; falls back to VehicleName
    int          CarClassId,   // stable hash of VehicleClass string (for grouping)
    ColorConfig? ClassColor,   // assigned per class by LmuSessionDecoder
    bool         InGarageStall);
