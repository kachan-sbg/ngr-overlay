namespace SimOverlay.Sim.Contracts;

public sealed class RelativeEntry
{
    public int Position { get; init; }
    public string CarNumber { get; init; } = "";
    public string DriverName { get; init; } = "";
    public int IRating { get; init; }
    public LicenseClass LicenseClass { get; init; }
    public string LicenseLevel { get; init; } = ""; // e.g., "B 3.45"
    public float GapToPlayerSeconds { get; init; }  // negative = ahead of player
    public int LapDifference { get; init; }          // 0 = same lap, +1 = one lap ahead
    public bool IsPlayer { get; init; }
}
