namespace SimOverlay.Sim.Contracts;

/// <summary>
/// Bitmask of pit services requested in the current session's pit menu.
/// Mapped from the sim's native service flags by the provider.
/// </summary>
[Flags]
public enum PitServiceFlags
{
    None               = 0,
    Fuel               = 1,
    LeftFrontTire      = 2,
    RightFrontTire     = 4,
    LeftRearTire       = 8,
    RightRearTire      = 16,
    AllTires           = LeftFrontTire | RightFrontTire | LeftRearTire | RightRearTire,
    WindshieldTearoff  = 32,
    FastRepair         = 64,
}
