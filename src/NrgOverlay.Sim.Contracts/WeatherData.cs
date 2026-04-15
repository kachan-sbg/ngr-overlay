namespace NrgOverlay.Sim.Contracts;

/// <summary>
/// Current weather conditions, published at в‰¤1 Hz while connected.
/// No forecast вЂ” current conditions only for Alpha.
/// </summary>
public sealed record WeatherData(
    float AirTempC,
    float TrackTempC,
    float WindSpeedMps,
    float WindDirectionDeg,  // 0=N, 90=E, 180=S, 270=W
    float Humidity,          // 0.0вЂ“1.0
    int?  SkyCoverage,       // 0=clear, 1=partly cloudy, 2=mostly cloudy, 3=overcast; null = not available from this sim
    float TrackWetness,      // 0.0вЂ“1.0 (normalised from sim's 0вЂ“7 enum)
    bool  IsPrecipitating
);

