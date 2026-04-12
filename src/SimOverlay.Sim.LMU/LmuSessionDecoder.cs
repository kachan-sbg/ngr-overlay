using SimOverlay.Core.Config;
using SimOverlay.Sim.Contracts;
using SimOverlay.Sim.LMU.SharedMemory;

namespace SimOverlay.Sim.LMU;

/// <summary>
/// Stateless decoder: converts LMU scoring data to the sim-agnostic
/// <see cref="SessionData"/> and a per-driver <see cref="LmuDriverSnapshot"/> list.
/// </summary>
internal static class LmuSessionDecoder
{
    // Class colours assigned round-robin when the plugin does not supply its own.
    private static readonly ColorConfig[] FallbackClassColors =
    [
        new ColorConfig { R = 1.00f, G = 0.40f, B = 0.00f, A = 1f }, // orange  — GTE/GT3
        new ColorConfig { R = 0.20f, G = 0.60f, B = 1.00f, A = 1f }, // blue    — LMDh
        new ColorConfig { R = 0.00f, G = 0.80f, B = 0.40f, A = 1f }, // green   — GTE
        new ColorConfig { R = 1.00f, G = 0.90f, B = 0.00f, A = 1f }, // yellow  — LMP2
        new ColorConfig { R = 0.80f, G = 0.20f, B = 0.80f, A = 1f }, // purple  — Hypercar
        new ColorConfig { R = 0.00f, G = 0.80f, B = 0.80f, A = 1f }, // cyan    — misc
    ];

    /// <summary>
    /// Decodes scoring data into a <see cref="SessionData"/> and a driver snapshot list.
    /// </summary>
    public static (SessionData Session, IReadOnlyList<LmuDriverSnapshot> Drivers) Decode(
        LmuScoringInfo info,
        LmuVehicleScoring[] vehicles)
    {
        // ── Classify vehicles by their VehicleClass string ────────────────────
        var classMap = BuildClassMap(vehicles);

        // ── Build driver snapshots ────────────────────────────────────────────
        var drivers = new List<LmuDriverSnapshot>(vehicles.Length);
        foreach (ref readonly var v in vehicles.AsSpan())
        {
            if (!v.IsActive) continue;

            string vehicleClass = DeriveClass(v);
            int    classId      = vehicleClass.GetHashCode();
            classMap.TryGetValue(vehicleClass, out var classColor);

            drivers.Add(new LmuDriverSnapshot(
                SlotId:        v.Id,
                DriverName:    v.DriverName,
                CarNumber:     v.Id.ToString(),
                VehicleClass:  vehicleClass,
                CarClassId:    classId,
                ClassColor:    classColor,
                InGarageStall: v.InGarageStall != 0));
        }

        // ── Build SessionData ─────────────────────────────────────────────────
        var session = BuildSessionData(info, classMap);

        return (session, drivers);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

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

        // Fallback: first token of VehicleName (e.g. "LMDh_Porsche_963" → "LMDh").
        var name = v.VehicleName ?? "";
        int sep  = name.IndexOfAny(['_', ' ', '-']);
        return sep > 0 ? name[..sep] : name;
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
    /// Maps <c>MaxPathWetness</c> (0–1) to the 0–7 iRacing-compatible wetness scale.
    /// 0 = unknown, 1 = dry … 7 = extremely wet.
    /// </summary>
    internal static int MapWetness(double maxWetness)
    {
        if (maxWetness <= 0.0) return 1;
        return Math.Clamp((int)(maxWetness * 6.0) + 1, 1, 7);
    }
}
