using SimOverlay.Core.Config;

namespace SimOverlay.Sim.Contracts;

/// <summary>
/// Describes a car class present in the current session.
/// Populated by the sim provider from session metadata and published as part of
/// <see cref="SessionData.CarClasses"/>.
/// </summary>
public sealed class CarClassInfo
{
    public int ClassId { get; init; }
    /// <summary>Short display name, e.g. "GTP", "LMP2", "GT3".</summary>
    public string ClassName { get; init; } = "";
    public ColorConfig ClassColor { get; init; } = ColorConfig.White;
    public int CarCount { get; init; }
}
