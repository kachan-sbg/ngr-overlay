using SimOverlay.Core.Config;
using SimOverlay.Sim.Contracts;

namespace SimOverlay.Sim.iRacing;

/// <summary>
/// Stateless decoder that converts an <see cref="IRSDKSharper.IRacingSdkSessionInfo"/>
/// (the parsed YAML from the iRacing SDK) into our domain types: a driver snapshot list
/// and a <see cref="SessionData"/>.  No SDK start/stop logic here — fully unit-testable.
/// </summary>
internal static class IRacingSessionDecoder
{
    /// <summary>
    /// Decodes session info and returns both the driver list and the current session summary.
    /// </summary>
    public static (IReadOnlyList<DriverSnapshot> Drivers, SessionData Session) Decode(
        IRSDKSharper.IRacingSdkData data)
    {
        var info = data.SessionInfo;

        // ── Drivers ───────────────────────────────────────────────────────────
        var rawDrivers = info?.DriverInfo?.Drivers;
        var drivers    = new List<DriverSnapshot>(rawDrivers?.Count ?? 0);

        // Class accumulator: classId → (name, color, count)
        var classMap = new Dictionary<int, (string Name, ColorConfig Color, int Count)>();

        if (rawDrivers != null)
        {
            foreach (var d in rawDrivers)
            {
                if (d is null) continue;

                var classId    = d.CarClassID;
                var className  = d.CarClassShortName ?? string.Empty;
                var classColor = RgbIntToColor(d.CarClassColor);

                // Accumulate class info (first driver in each class wins for name/color)
                if (!classMap.TryGetValue(classId, out var existing))
                    classMap[classId] = (className, classColor, 1);
                else
                    classMap[classId] = (existing.Name, existing.Color, existing.Count + 1);

                drivers.Add(new DriverSnapshot(
                    CarIdx:       d.CarIdx,
                    UserName:     d.UserName    ?? string.Empty,
                    CarNumber:    d.CarNumber   ?? d.CarIdx.ToString(),
                    IRating:      d.IRating,
                    LicenseClass: ParseLicenseClass(d.LicString),
                    LicenseLevel: d.LicString   ?? "R 0.00",
                    IsSpectator:  d.IsSpectator != 0,
                    IsPaceCar:    d.CarIsPaceCar != 0,
                    CarClassId:   classId,
                    CarClass:     className,
                    ClassColor:   classColor
                ));
            }
        }

        // Build CarClassInfo list — only expose multiple classes when there truly are multiple
        var carClasses = classMap.Count > 1
            ? classMap.Select(kv => new CarClassInfo
              {
                  ClassId    = kv.Key,
                  ClassName  = kv.Value.Name,
                  ClassColor = kv.Value.Color,
                  CarCount   = kv.Value.Count,
              }).ToList()
            : (IReadOnlyList<CarClassInfo>)[];

        // ── Track / weather ───────────────────────────────────────────────────
        var weekendInfo = info?.WeekendInfo;
        var trackName   = weekendInfo?.TrackDisplayName ?? string.Empty;
        var airTempC    = ParseTemperatureC(weekendInfo?.TrackAirTemp);
        var trackTempC  = ParseTemperatureC(weekendInfo?.TrackSurfaceTemp);

        // ── Active session ────────────────────────────────────────────────────
        var sessionType          = SessionType.Unknown;
        var sessionTimeRemaining = TimeSpan.Zero;
        var totalLaps            = 0;

        var sessions          = info?.SessionInfo?.Sessions;
        var currentSessionNum = data.GetInt("SessionNum");

        if (sessions != null && currentSessionNum >= 0 && currentSessionNum < sessions.Count)
        {
            var s            = sessions[currentSessionNum];
            sessionType          = ParseSessionType(s?.SessionType);
            sessionTimeRemaining = ParseSessionTime(s?.SessionTime);
            totalLaps            = ParseSessionLaps(s?.SessionLaps);
        }

        // ── Live weather telemetry ────────────────────────────────────────────
        var humidity          = data.GetFloat("RelativeHumidity");
        var weatherDeclaredWet = data.GetInt("WeatherDeclaredWet") != 0;
        var trackWetness      = data.GetInt("TrackWetness");

        var session = new SessionData
        {
            TrackName            = trackName,
            SessionType          = sessionType,
            SessionTimeRemaining = sessionTimeRemaining,
            TotalLaps            = totalLaps,
            AirTempC             = airTempC,
            TrackTempC           = trackTempC,
            RelativeHumidity     = humidity,
            WeatherDeclaredWet   = weatherDeclaredWet,
            TrackWetness         = trackWetness,
            CarClasses           = carClasses,
        };

        return (drivers, session);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts an iRacing class colour string to a <see cref="ColorConfig"/>.
    /// iRacing stores colours as decimal or hex strings, e.g. "16711680" or "0xFF0000".
    /// Falls back to white when the value is absent, zero, or unparseable.
    /// </summary>
    private static ColorConfig RgbIntToColor(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ColorConfig.White;

        int rgb;
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(trimmed[2..], System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out rgb))
                return ColorConfig.White;
        }
        else
        {
            if (!int.TryParse(trimmed, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out rgb))
                return ColorConfig.White;
        }

        if (rgb == 0) return ColorConfig.White;
        return new ColorConfig
        {
            R = ((rgb >> 16) & 0xFF) / 255f,
            G = ((rgb >>  8) & 0xFF) / 255f,
            B = ( rgb        & 0xFF) / 255f,
            A = 1f,
        };
    }

    /// <summary>Parses iRacing's temperature string, e.g. "24.44 C" → 24.44f.</summary>
    private static float ParseTemperatureC(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0f;

        var spaceIdx = value.IndexOf(' ');
        var numStr   = spaceIdx >= 0 ? value[..spaceIdx] : value;

        return float.TryParse(
            numStr,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var t) ? t : 0f;
    }

    /// <summary>
    /// Parses the first letter of an iRacing license string, e.g. "A 3.50" → <see cref="LicenseClass.A"/>.
    /// </summary>
    private static LicenseClass ParseLicenseClass(string? licString)
    {
        if (string.IsNullOrWhiteSpace(licString)) return LicenseClass.R;

        return licString[0] switch
        {
            'R' or 'r' => LicenseClass.R,
            'D' or 'd' => LicenseClass.D,
            'C' or 'c' => LicenseClass.C,
            'B' or 'b' => LicenseClass.B,
            'A' or 'a' => LicenseClass.A,
            'P' or 'p' => LicenseClass.Pro,
            'W' or 'w' => LicenseClass.WC,
            _          => LicenseClass.R,
        };
    }

    /// <summary>Maps an iRacing session type string to <see cref="SessionType"/>.</summary>
    private static SessionType ParseSessionType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return SessionType.Unknown;

        return raw.Trim().ToUpperInvariant() switch
        {
            "PRACTICE"                              => SessionType.Practice,
            "QUALIFY" or "OPEN QUALIFY"
                      or "LONE QUALIFY"             => SessionType.Qualify,
            "RACE" or "OPEN RACE"                   => SessionType.Race,
            "TIME TRIAL"                            => SessionType.TimeTrial,
            "WARMUP"                                => SessionType.Warmup,
            _                                       => SessionType.Unknown,
        };
    }

    /// <summary>
    /// Parses iRacing's session time string, e.g. "3600.0000 sec" → TimeSpan(1 hour),
    /// or "unlimited" → <see cref="TimeSpan.Zero"/>.
    /// </summary>
    private static TimeSpan ParseSessionTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith("unlimited", StringComparison.OrdinalIgnoreCase))
            return TimeSpan.Zero;

        var spaceIdx = value.IndexOf(' ');
        var numStr   = spaceIdx >= 0 ? value[..spaceIdx] : value;

        return double.TryParse(
            numStr,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var secs)
            ? TimeSpan.FromSeconds(secs)
            : TimeSpan.Zero;
    }

    /// <summary>
    /// Parses iRacing's session laps string, e.g. "32" → 32, or "unlimited" → 0.
    /// </summary>
    private static int ParseSessionLaps(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith("unlimited", StringComparison.OrdinalIgnoreCase))
            return 0;

        return int.TryParse(value.Trim(), out var laps) ? laps : 0;
    }
}
