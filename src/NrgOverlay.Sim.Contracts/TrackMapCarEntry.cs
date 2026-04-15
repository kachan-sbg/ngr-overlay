namespace NrgOverlay.Sim.Contracts;

/// <summary>Per-car entry in a <see cref="TrackMapData"/> snapshot.</summary>
public sealed record TrackMapCarEntry(
    int    CarIndex,
    string CarNumber,
    int    Position,     // overall race position
    float  LapDistPct,  // 0.0вЂ“1.0 position around the track
    string CarClass,
    bool   IsPlayer,
    bool   IsInPit
);

