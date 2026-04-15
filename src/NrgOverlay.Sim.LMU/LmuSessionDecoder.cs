using NrgOverlay.Core.Config;
using NrgOverlay.Sim.Contracts;
using NrgOverlay.Sim.LMU.SharedMemory;

namespace NrgOverlay.Sim.LMU;

/// <summary>
/// Stateless decoder: converts LMU scoring data to the sim-agnostic
/// <see cref="SessionData"/> and a per-driver <see cref="LmuDriverSnapshot"/> list.
/// </summary>
internal static class LmuSessionDecoder
{
    // Class colours assigned round-robin when the plugin does not supply its own.
    private static readonly ColorConfig[] FallbackClassColors =
    [
        new ColorConfig { R = 1.00f, G = 0.40f, B = 0.00f, A = 1f }, // orange  вЂ” GTE/GT3
        new ColorConfig { R = 0.20f, G = 0.60f, B = 1.00f, A = 1f }, // blue    вЂ” LMDh
        new ColorConfig { R = 0.00f, G = 0.80f, B = 0.40f, A = 1f }, // green   вЂ” GTE
        new ColorConfig { R = 1.00f, G = 0.90f, B = 0.00f, A = 1f }, // yellow  вЂ” LMP2
        new ColorConfig { R = 0.80f, G = 0.20f, B = 0.80f, A = 1f }, // purple  вЂ” Hypercar
        new ColorConfig { R = 0.00f, G = 0.80f, B = 0.80f, A = 1f }, // cyan    вЂ” misc
    ];

    /// <summary>
    /// Decodes scoring data into a <see cref="SessionData"/> and a driver snapshot list.
    /// </summary>
    public static (SessionData Session, IReadOnlyList<LmuDriverSnapshot> Drivers) Decode(
        LmuScoringInfo info,
        LmuVehicleScoring[] vehicles)
    {
        // в”Ђв”Ђ Classify vehicles by their VehicleClass string в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        var classMap = BuildClassMap(vehicles);

        // в”Ђв”Ђ Build driver snapshots в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        var drivers = new List<LmuDriverSnapshot>(vehicles.Length);
        foreach (ref readonly var v in vehicles.AsSpan())
        {
            if (!v.IsActive) continue;

            string vehicleClass = DeriveClass(v);
            int    classId      = vehicleClass.GetHashCode();
            classMap.TryGetValue(vehicleClass, out var classColor);

            drivers.Add(new LmuDriverSnapshot(
                SlotId:        v.Id,
                DriverName:    v.DriverName ?? string.Empty,
                CarNumber:     DeriveCarNumber(v),
                CountryCode:   string.Empty, // LMU shared memory currently exposes no driver country code.
                VehicleClass:  vehicleClass,
                CarClassId:    classId,
                ClassColor:    classColor,
                InGarageStall: v.InGarageStall != 0));
        }

        // в”Ђв”Ђ Build SessionData в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        var session = BuildSessionData(info, classMap);

        return (session, drivers);
    }

    // в”Ђв”Ђ Private helpers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private static Dictionary<string, ColorConfig> BuildClassMap(LmuVehicleScoring[] vehicles)
    {
        var uniqueClasses = new List<string>();
        foreach (ref readonly var v in vehicles.AsSpan())
        {
            if (!v.IsActive) continue;
            var cls = DeriveClass(v);
            if (!uniqueClasses.Contains(cls))
                uniqueClasses.Add(cls);
        }

        var map = new Dictionary<string, ColorConfig>(uniqueClasses.Count);
        for (int i = 0; i < uniqueClasses.Count; i++)
            map[uniqueClasses[i]] = FallbackClassColors[i % FallbackClassColors.Length];

        return map;
    }

    /// <summary>
    /// Derives a human-readable class name from a vehicle entry.
    /// Uses <see cref="LmuVehicleScoring.VehicleClass"/> directly (it is a native field
    /// in the new struct, not an expansion).  Falls back to the first token of
    /// <see cref="LmuVehicleScoring.VehicleName"/> if empty.
    /// </summary>
    internal static string DeriveClass(in LmuVehicleScoring v)
    {
        var cls = v.VehicleClass;
        if (!string.IsNullOrWhiteSpace(cls))
            return cls;

        // Fallback: first token of VehicleName (e.g. "LMDh_Porsche_963" в†’ "LMDh").
        var name = v.VehicleName ?? "";
        int sep  = name.IndexOfAny(['_', ' ', '-']);
        return sep > 0 ? name[..sep] : name;
    }

    private static string DeriveCarNumber(in LmuVehicleScoring v)
    {
        var fromVehicleName = ExtractCarNumberToken(v.VehicleName);
        if (!string.IsNullOrWhiteSpace(fromVehicleName))
            return fromVehicleName;

        return v.Id > 0 ? v.Id.ToString() : "--";
    }

    private static string ExtractCarNumberToken(string? vehicleName)
    {
        if (string.IsNullOrWhiteSpace(vehicleName)) return string.Empty;

        var s = vehicleName;
        for (int i = 0; i < s.Length - 1; i++)
        {
            if (s[i] != '#') continue;

            int start = i + 1;
            int end = start;
            while (end < s.Length && char.IsDigit(s[end])) end++;
            if (end > start)
                return s[start..end];
        }

        return string.Empty;
    }

    private static SessionData BuildSessionData(
        LmuScoringInfo info,
        Dictionary<string, ColorConfig> classMap)
    {
        float airTemp   = (float)info.AmbientTempC;
        float trackTemp = (float)info.TrackTempC;

        // Compute session time remaining: EndET > 0 means time-based.
        TimeSpan timeRemaining = info.EndET > 0 && info.CurrentET > 0
            ? TimeSpan.FromSeconds(Math.Max(0, info.EndET - info.CurrentET))
            : TimeSpan.Zero;
        TimeSpan timeElapsed = info.CurrentET > 0
            ? TimeSpan.FromSeconds(info.CurrentET)
            : TimeSpan.Zero;

        // Build car class list if multi-class (>1 unique class).
        var classes = new List<CarClassInfo>(classMap.Count);
        if (classMap.Count > 1)
        {
            foreach (var (name, color) in classMap)
            {
                classes.Add(new CarClassInfo
                {
                    ClassId    = name.GetHashCode(),
                    ClassName  = name,
                    ClassColor = color,
                    CarCount   = 0, // counted by caller if needed
                });
            }
        }

        return new SessionData
        {
            TrackName            = info.TrackName ?? "",
            SessionType          = MapSessionType(info.Session),
            SessionTimeRemaining = timeRemaining,
            SessionTimeElapsed   = timeElapsed,
            TotalLaps            = info.MaxLaps > 0 ? info.MaxLaps : 0,
            AirTempC             = airTemp,
            TrackTempC           = trackTemp,
            GameTimeOfDay        = null, // not exposed by LMU scoring
            RelativeHumidity     = 0f,
            WeatherDeclaredWet   = info.Raining > 0.3,
            TrackWetness         = MapWetness(info.MaxPathWetness),
            CarClasses           = classes,
        };
    }

    /// <summary>
    /// Maps LMU session integer to <see cref="SessionType"/>.
    /// </summary>
    internal static SessionType MapSessionType(int rfSession) => rfSession switch
    {
        0             => SessionType.Practice,
        >= 1 and <= 4 => SessionType.Practice,
        >= 5 and <= 8 => SessionType.Qualify,
        9             => SessionType.Warmup,
        >= 10         => SessionType.Race,
        _             => SessionType.Practice,
    };

    /// <summary>
    /// Maps <c>MaxPathWetness</c> (0вЂ“1) to the 0вЂ“7 iRacing-compatible wetness scale.
    /// 0 = unknown, 1 = dry вЂ¦ 7 = extremely wet.
    /// </summary>
    internal static int MapWetness(double maxWetness)
    {
        if (maxWetness <= 0.0) return 1;
        return Math.Clamp((int)(maxWetness * 6.0) + 1, 1, 7);
    }
}

