using NrgOverlay.Core.Config;
using NrgOverlay.Sim.Contracts;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace NrgOverlay.Sim.iRacing;

/// <summary>
/// Stateless decoder that converts an <see cref="IRSDKSharper.IRacingSdkSessionInfo"/>
/// (the parsed YAML from the iRacing SDK) into our domain types: a driver snapshot list
/// and a <see cref="SessionData"/>.  No SDK start/stop logic here вЂ” fully unit-testable.
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

        // в”Ђв”Ђ Drivers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        var rawDrivers = info?.DriverInfo?.Drivers;
        var drivers    = new List<DriverSnapshot>(rawDrivers?.Count ?? 0);

        // Class accumulator: classId в†’ (name, color, count)
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
                    CarIdx:        d.CarIdx,
                    UserName:      d.UserName         ?? string.Empty,
                    CarNumber:     d.CarNumber        ?? d.CarIdx.ToString(),
                    IRating:       d.IRating,
                    LicenseClass:  ParseLicenseClass(d.LicString),
                    LicenseLevel:  d.LicString        ?? "R 0.00",
                    IsSpectator:   d.IsSpectator != 0,
                    IsPaceCar:     d.CarIsPaceCar != 0,
                    CarClassId:    classId,
                    CarClass:      className,
                    ClassColor:    classColor,
                    TeamName:      d.TeamName         ?? string.Empty,
                    CarScreenName: d.CarScreenName    ?? string.Empty,
                    ClubName:      d.ClubName         ?? string.Empty,
                    FlairId:       d.FlairID,
                    ClubId:        d.ClubID,
                    UserId:        d.UserID
                ));
            }
        }

        // Build CarClassInfo list вЂ” only expose multiple classes when there truly are multiple
        var carClasses = classMap.Count > 1
            ? classMap.Select(kv => new CarClassInfo
              {
                  ClassId    = kv.Key,
                  ClassName  = kv.Value.Name,
                  ClassColor = kv.Value.Color,
                  CarCount   = kv.Value.Count,
              }).ToList()
            : (IReadOnlyList<CarClassInfo>)[];

        var activeDrivers = drivers.Where(d => !d.IsSpectator && !d.IsPaceCar).ToList();
        var playerCarIdx = info?.DriverInfo?.DriverCarIdx ?? 0;
        var playerClassId = activeDrivers.FirstOrDefault(d => d.CarIdx == playerCarIdx)?.CarClassId ?? 0;
        var classDrivers = playerClassId != 0
            ? activeDrivers.Where(d => d.CarClassId == playerClassId).ToList()
            : activeDrivers;

        var playerCountOverall = activeDrivers.Count;
        var playerCountInClass = classDrivers.Count;
        var sofOverall = ComputeSof(activeDrivers);
        var sofInClass = ComputeSof(classDrivers);

        // в”Ђв”Ђ Track / weather в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        var weekendInfo = info?.WeekendInfo;
        var trackName   = weekendInfo?.TrackDisplayName ?? string.Empty;
        var airTempC    = ParseTemperatureC(weekendInfo?.TrackAirTemp);
        var trackTempC  = ParseTemperatureC(weekendInfo?.TrackSurfaceTemp);

        // в”Ђв”Ђ Active session в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        var sessionType       = SessionType.Unknown;
        var sessionTimeLimit  = TimeSpan.Zero;
        var sessionBestLapTime = TimeSpan.Zero;
        var totalLaps         = 0;

        var sessions          = info?.SessionInfo?.Sessions;
        var currentSessionNum = data.GetInt("SessionNum");

        if (sessions != null && currentSessionNum >= 0 && currentSessionNum < sessions.Count)
        {
            var s            = sessions[currentSessionNum];
            sessionType      = ParseSessionType(s?.SessionType);
            sessionTimeLimit = ParseSessionTime(s?.SessionTime);
            sessionBestLapTime = ParseSessionBestLapTime(s);
            totalLaps        = ParseSessionLaps(s?.SessionLaps);
        }

        // в”Ђв”Ђ Live weather telemetry в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        var humidity          = data.GetFloat("RelativeHumidity");
        var weatherDeclaredWet = data.GetInt("WeatherDeclaredWet") != 0;
        var trackWetness      = data.GetInt("TrackWetness");
        var weekendOptions = weekendInfo?.WeekendOptions;
        var incidentDriveThroughLimit = ParseIncidentDriveThroughLimit(weekendOptions);
        var incidentDisqualificationLimit = ParseIncidentDisqualificationLimit(weekendOptions);

        var sessionTimeRemainSec = GetTelemetrySeconds(data, "SessionTimeRemain");
        var sessionTimeRemainValid = sessionTimeRemainSec >= 0f && sessionTimeRemainSec < 1e10f;
        var sessionTimeRemaining = sessionTimeRemainValid
            ? SafeTimeSpanFromSeconds(sessionTimeRemainSec)
            : TimeSpan.Zero;

        TimeSpan sessionElapsed;
        if (sessionTimeLimit > TimeSpan.Zero && sessionTimeRemaining > TimeSpan.Zero)
        {
            var boundedRemaining = sessionTimeRemaining > sessionTimeLimit
                ? sessionTimeLimit
                : sessionTimeRemaining;
            sessionElapsed = sessionTimeLimit - boundedRemaining;
        }
        else
        {
            // Fallback snapshot from telemetry. Filter absurd values that appear while
            // session telemetry is initializing (e.g. giant sentinel-like numbers).
            var sessionTimeSec = GetTelemetrySeconds(data, "SessionTime");
            sessionElapsed = sessionTimeSec > 0f && sessionTimeSec < 86400f * 7f
                ? SafeTimeSpanFromSeconds(sessionTimeSec)
                : TimeSpan.Zero;
        }

        // SessionTimeOfDay: seconds since midnight in the sim world (snapshot at YAML change time).
        var timeOfDaySec = GetTelemetrySeconds(data, "SessionTimeOfDay");
        TimeOnly? gameTimeOfDay = timeOfDaySec > 0
            ? TimeOnly.FromTimeSpan(TimeSpan.FromSeconds(timeOfDaySec % 86400))
            : null;

        var session = new SessionData
        {
            TrackName            = trackName,
            SessionType          = sessionType,
            SessionTimeLimit     = sessionTimeLimit,
            SessionTimeRemaining = sessionTimeRemaining,
            SessionTimeElapsed   = sessionElapsed,
            SessionBestLapTime   = sessionBestLapTime,
            TotalLaps            = totalLaps,
            AirTempC             = airTempC,
            TrackTempC           = trackTempC,
            GameTimeOfDay        = gameTimeOfDay,
            RelativeHumidity     = humidity,
            WeatherDeclaredWet   = weatherDeclaredWet,
            TrackWetness         = trackWetness,
            CarClasses           = carClasses,
            PlayerCountOverall = playerCountOverall,
            PlayerCountInClass = playerCountInClass,
            StrengthOfFieldOverall = sofOverall,
            StrengthOfFieldInClass = sofInClass,
            IncidentDriveThroughLimit = incidentDriveThroughLimit,
            IncidentDisqualificationLimit = incidentDisqualificationLimit,
        };

        return (drivers, session);
    }

    private static TimeSpan ParseSessionBestLapTime(object? session)
    {
        if (session is null)
            return TimeSpan.Zero;

        var bestSeconds = FindBestLapSecondsInCollectionProperty(session, "ResultsFastestLap");
        if (bestSeconds <= 0)
            bestSeconds = FindBestLapSecondsInCollectionProperty(session, "ResultsPositions");

        return bestSeconds > 0 ? SafeTimeSpanFromSeconds(bestSeconds) : TimeSpan.Zero;
    }

    private static double FindBestLapSecondsInCollectionProperty(object source, string propertyName)
    {
        if (!TryReadProperty(source, propertyName, out var raw) || raw is null || raw is string)
            return 0d;

        if (raw is not IEnumerable items)
            return 0d;

        double best = 0d;
        foreach (var item in items)
        {
            if (item is null) continue;
            if (!TryReadLapTimeSeconds(item, out var secs)) continue;
            if (secs <= 0d) continue;

            if (best <= 0d || secs < best)
                best = secs;
        }

        return best;
    }

    private static bool TryReadLapTimeSeconds(object source, out double seconds)
    {
        seconds = 0d;

        // Avoid generic "Time" fields (often position/interval/session values, not lap time).
        var candidates = new[] { "FastestTime", "FastestLapTime", "BestLapTime" };
        foreach (var propertyName in candidates)
        {
            if (!TryReadProperty(source, propertyName, out var raw) || raw is null)
                continue;

            switch (raw)
            {
                case double d when d > 0d:
                    seconds = d;
                    return true;
                case float f when f > 0f:
                    seconds = f;
                    return true;
                case int i when i > 0:
                    seconds = i;
                    return true;
                case long l when l > 0:
                    seconds = l;
                    return true;
                case string s when TryParseTimeStringSeconds(s, out var parsed) && parsed > 0d:
                    seconds = parsed;
                    return true;
            }
        }

        return false;
    }

    private static bool TryParseTimeStringSeconds(string raw, out double seconds)
    {
        seconds = 0d;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var trimmed = raw.Trim();
        if (double.TryParse(
                trimmed,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var directSeconds)
            && directSeconds > 0d)
        {
            seconds = directSeconds;
            return true;
        }

        var formats = new[]
        {
            @"m\:ss\.fff",
            @"m\:ss\.ffff",
            @"m\:ss\.fffff",
            @"m\:ss",
            @"mm\:ss\.fff",
            @"mm\:ss\.ffff",
            @"mm\:ss",
            @"h\:mm\:ss\.fff",
            @"h\:mm\:ss\.ffff",
            @"h\:mm\:ss",
            @"d\.h\:mm\:ss\.fff",
            @"d\.h\:mm\:ss",
        };
        if (TimeSpan.TryParseExact(
                trimmed,
                formats,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsedTimeSpan)
            && parsedTimeSpan > TimeSpan.Zero)
        {
            seconds = parsedTimeSpan.TotalSeconds;
            return true;
        }

        // iRacing can sometimes emit "0.1:39.873" style values.
        // Interpret as H:MM:SS(.fff) where the left side of '.' is hours.
        var dotHourMatch = Regex.Match(
            trimmed,
            @"^(?<h>\d+)\.(?<m>\d{1,2}):(?<s>\d{2})(?:\.(?<f>\d{1,4}))?$");
        if (dotHourMatch.Success
            && int.TryParse(dotHourMatch.Groups["h"].Value, out var h)
            && int.TryParse(dotHourMatch.Groups["m"].Value, out var m)
            && int.TryParse(dotHourMatch.Groups["s"].Value, out var s))
        {
            var fracRaw = dotHourMatch.Groups["f"].Value;
            var fracSec = 0d;
            if (!string.IsNullOrEmpty(fracRaw)
                && double.TryParse(
                    "0." + fracRaw,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsedFrac))
            {
                fracSec = parsedFrac;
            }

            seconds = (h * 3600d) + (m * 60d) + s + fracSec;
            return seconds > 0d;
        }

        var match = Regex.Match(trimmed, @"[-+]?\d+(\.\d+)?");
        if (!match.Success)
            return false;

        return double.TryParse(
            match.Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out seconds);
    }

    // в”Ђв”Ђ Helpers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

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

    /// <summary>Parses iRacing's temperature string, e.g. "24.44 C" в†’ 24.44f.</summary>
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
    /// Parses the first letter of an iRacing license string, e.g. "A 3.50" в†’ <see cref="LicenseClass.A"/>.
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
    /// Parses iRacing's session time string, e.g. "3600.0000 sec" в†’ TimeSpan(1 hour),
    /// or "unlimited" в†’ <see cref="TimeSpan.Zero"/>.
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
            ? SafeTimeSpanFromSeconds(secs)
            : TimeSpan.Zero;
    }

    /// <summary>
    /// Parses iRacing's session laps string, e.g. "32" в†’ 32, or "unlimited" в†’ 0.
    /// </summary>
    private static int ParseSessionLaps(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith("unlimited", StringComparison.OrdinalIgnoreCase))
            return 0;

        if (!int.TryParse(value.Trim(), out var laps))
            return 0;

        // iRacing uses 32767 as a sentinel for unlimited/unknown lap count in time-based sessions.
        if (laps >= short.MaxValue)
            return 0;

        return Math.Max(0, laps);
    }

    /// <summary>
    /// Converts seconds to TimeSpan with overflow protection.
    /// iRacing occasionally emits sentinel-like very large values while session data stabilizes.
    /// </summary>
    private static TimeSpan SafeTimeSpanFromSeconds(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds))
            return TimeSpan.Zero;

        const double MaxSec = 86400d * 30d; // 30 days
        return TimeSpan.FromSeconds(Math.Clamp(seconds, 0d, MaxSec));
    }

    /// <summary>
    /// iRacing SOF approximation: -ln(sum(exp(-iR/1600))) * 1600.
    /// Returns 0 when no positive iRating values are present.
    /// </summary>
    private static int ComputeSof(IReadOnlyList<DriverSnapshot> drivers)
    {
        if (drivers.Count == 0)
            return 0;

        double sum = 0d;
        foreach (var d in drivers)
        {
            if (d.IRating <= 0)
                continue;
            sum += Math.Exp(-d.IRating / 1600d);
        }

        if (sum <= 0d)
            return 0;

        var sof = -Math.Log(sum) * 1600d;
        if (double.IsNaN(sof) || double.IsInfinity(sof) || sof <= 0d)
            return 0;

        return (int)Math.Round(sof, MidpointRounding.AwayFromZero);
    }

    private static int ParseIncidentDriveThroughLimit(object? weekendOptions)
    {
        if (weekendOptions is null)
            return 0;

        if (TryReadIntProperty(weekendOptions, "IncidentLimit", out var incidentLimit))
            return incidentLimit;

        if (TryReadIntProperty(weekendOptions, "Incidents", out incidentLimit))
            return incidentLimit;

        if (TryReadStringProperty(weekendOptions, "DCRuleSet", out var dcRuleSet))
        {
            var directDt = Regex.Match(dcRuleSet, @"(?i)(?:dt|drive\s*through)[^\d]{0,20}(\d+)");
            if (directDt.Success && int.TryParse(directDt.Groups[1].Value, out var parsedDt) && parsedDt > 0)
                return parsedDt;
        }

        return 0;
    }

    private static int ParseIncidentDisqualificationLimit(object? weekendOptions)
    {
        if (weekendOptions is null)
            return 0;

        if (TryReadIntProperty(weekendOptions, "Disqualify", out var dqLimit))
            return dqLimit;

        if (TryReadIntProperty(weekendOptions, "DisqualifyAt", out dqLimit))
            return dqLimit;

        if (TryReadIntProperty(weekendOptions, "DqLimit", out dqLimit))
            return dqLimit;

        if (TryReadStringProperty(weekendOptions, "DCRuleSet", out var dcRuleSet))
        {
            var directDq = Regex.Match(dcRuleSet, @"(?i)(?:dq|disqualif\w*)[^\d]{0,20}(\d+)");
            if (directDq.Success && int.TryParse(directDq.Groups[1].Value, out var parsedDq) && parsedDq > 0)
                return parsedDq;
        }

        return 0;
    }

    private static bool TryReadIntProperty(object source, string propertyName, out int value)
    {
        value = 0;
        if (!TryReadProperty(source, propertyName, out var raw) || raw is null)
            return false;

        switch (raw)
        {
            case int i when i > 0:
                value = i;
                return true;
            case long l when l > 0 && l <= int.MaxValue:
                value = (int)l;
                return true;
            case float f when f > 0 && f <= int.MaxValue:
                value = (int)Math.Round(f, MidpointRounding.AwayFromZero);
                return true;
            case double d when d > 0 && d <= int.MaxValue:
                value = (int)Math.Round(d, MidpointRounding.AwayFromZero);
                return true;
            case string s:
                var m = Regex.Match(s, @"\d+");
                if (m.Success && int.TryParse(m.Value, out var parsed) && parsed > 0)
                {
                    value = parsed;
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    private static bool TryReadStringProperty(object source, string propertyName, out string value)
    {
        value = string.Empty;
        if (!TryReadProperty(source, propertyName, out var raw) || raw is null)
            return false;

        value = raw.ToString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadProperty(object source, string propertyName, out object? value)
    {
        value = null;

        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
        var prop = source.GetType().GetProperty(propertyName, flags);
        if (prop is null)
            return false;

        value = prop.GetValue(source);
        return value is not null;
    }

    private static float GetTelemetrySeconds(IRSDKSharper.IRacingSdkData data, string name)
    {
        if (!data.TelemetryDataProperties.ContainsKey(name))
            return 0f;

        var meta = data.TelemetryDataProperties[name];
        var varType = TryReadPropertyValue(meta, "VarType")?.ToString() ?? string.Empty;
        if (varType.Contains("Double", StringComparison.OrdinalIgnoreCase))
            return (float)data.GetDouble(name);

        return data.GetFloat(name);
    }

    private static object? TryReadPropertyValue(object source, string propertyName)
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
        var prop = source.GetType().GetProperty(propertyName, flags);
        return prop?.GetValue(source);
    }
}








