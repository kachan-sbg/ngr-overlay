namespace SimOverlay.Sim.Contracts;

/// <summary>
/// Real-time driver telemetry, published at 60 Hz while connected.
/// </summary>
public sealed record TelemetryData(
    float Throttle,               // 0.0–1.0
    float Brake,                  // 0.0–1.0
    float Clutch,                 // 0.0–1.0
    float SteeringAngle,          // radians, negative = left
    float SpeedMps,               // meters per second
    int   Gear,                   // -1=R, 0=N, 1–8
    float Rpm,
    float FuelLevelLiters,
    float FuelConsumptionPerLap,  // rolling average over last N green-flag laps
    float LastLapFuelLiters,      // fuel used in the last completed green-flag lap; 0 = none yet
    int   IncidentCount           // driver's session incident total
);
