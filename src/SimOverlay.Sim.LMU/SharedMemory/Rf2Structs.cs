using System.Runtime.InteropServices;

namespace SimOverlay.Sim.LMU.SharedMemory;

// ── Layout constants ──────────────────────────────────────────────────────────

/// <summary>
/// Byte-offset constants for the <c>LMU_Data</c> shared memory buffer.
/// All values are derived directly from the official Studio 397 plugin SDK headers
/// (<c>InternalsPlugin.hpp</c> / <c>SharedMemoryInterface.hpp</c>, copyright Studio 397 BV 2025)
/// with <c>#pragma pack(4)</c> and MSVC <c>long = 4 bytes</c>.
/// </summary>
internal static class LmuSharedMemoryLayout
{
    public const string DataFile              = "LMU_Data";
    public const int    MaxVehicles           = 104;

    // LmuScoringData starts at 1632 (after LmuGeneric + LmuPathData).
    // LmuScoringInfo is the first field of LmuScoringData.
    public const int    ScoringInfoOffset     = 1632;

    // ScoringVehiclesOffset derivation:
    //   InternalsPlugin.hpp: #pragma pack(push,4) at line 24, pop at line 1106.
    //   SharedMemoryInterface.hpp includes InternalsPlugin.hpp, then defines SharedMemoryScoringData
    //   AFTER the pack pop → SharedMemoryScoringData uses default (8-byte) packing on x64.
    //   ScoringInfoV01 was defined under pack(4), so its alignof = 4, sizeof = 548.
    //   Next member: size_t scoringStreamSize (alignof = 8).
    //   548 % 8 = 4 → 4 bytes of implicit padding inserted before size_t.
    //   Offset of vehScoringInfo = 548 + 4 (pad) + 8 (size_t) = 560.
    //   Absolute offset = 1632 + 560 = 2192.
    public const int    ScoringVehiclesOffset = 2192;

    public const int    VehicleScoringSize    = 584;

    // SharedMemoryScoringData total size (default packing):
    //   ScoringInfoV01(548) + padding(4) + size_t(8) + VehicleScoringInfoV01×104(60736) + scoringStream(65536) = 126832 bytes.
    // TelemetryHeaderOffset = 1632 + 126832 = 128464.
    public const int    TelemetryHeaderOffset = 128464;

    // SharedMemoryTelemetryData (default packing): uint8_t(1) + uint8_t(1) + bool(1) + 1-byte padding
    // (TelemInfoV01 was defined under pack4 → alignof=4; 3%4=3 → 1 byte pad) = 4 bytes before telemInfo[].
    public const int    TelemetryVehOffset    = 128468;  // TelemetryHeaderOffset + 4

    public const int    VehicleTelemSize      = 1888;

    // Per-vehicle telemetry field offsets within TelemInfoV01 (#pragma pack 4, MSVC long = 4 B).
    // Derived from InternalsPlugin.hpp (Studio 397 official SDK).
    public const int    Telem_Gear            = 352;  // long mGear
    public const int    Telem_EngineRPM       = 356;  // double mEngineRPM
    public const int    Telem_Throttle        = 388;  // double mUnfilteredThrottle
    public const int    Telem_Brake           = 396;  // double mUnfilteredBrake
    public const int    Telem_Steering        = 404;  // double mUnfilteredSteering
    public const int    Telem_Clutch          = 412;  // double mUnfilteredClutch
    public const int    Telem_Fuel            = 524;  // double mFuel
    public const int    Telem_FuelCapacity    = 608;  // double mFuelCapacity
    public const int    Telem_SpeedLimiter    = 748;  // bool mSpeedLimiterActive
}

// ── Shared vector type ────────────────────────────────────────────────────────

/// <summary>3D vector used in multiple LMU structs (3 × double = 24 bytes).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct LmuVect3
{
    public double X;
    public double Y;
    public double Z;
}

// ── Session-level scoring info ────────────────────────────────────────────────

/// <summary>
/// Session-level data from the LMU_Data shared memory buffer.
/// Starts at offset <see cref="LmuSharedMemoryLayout.ScoringInfoOffset"/> (1632).
/// Total size: 548 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
internal struct LmuScoringInfo
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string TrackName;            // [0]   64 B

    public int    Session;              // [64]   4 B   0=testday,1-4=practice,5-8=qualify,9=warmup,10-13=race
    public double CurrentET;           // [68]   8 B   current session elapsed time (seconds)
    public double EndET;               // [76]   8 B   session end time; ≤0 means laps-based
    public int    MaxLaps;             // [84]   4 B   max laps (0 = time-based)
    public double LapDist;             // [88]   8 B   track length (metres)

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] ResultsStreamPtr;    // [96]   8 B   pointer (do not dereference)

    public int    NumVehicles;         // [104]  4 B
    public byte   GamePhase;           // [108]  1 B   0=before,5=green,7=caution,8=race over
    public sbyte  YellowFlagState;     // [109]  1 B   -1=invalid,0=none,1=pending,2=pit,3=resume

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public byte[] SectorFlag;          // [110]  3 B   per-sector yellow flag bytes

    public byte   StartLight;          // [113]  1 B
    public byte   NumRedLights;        // [114]  1 B
    public byte   InRealtime;          // [115]  1 B   1 = live (not paused / replay)

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string PlayerName;          // [116] 32 B

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string PlrFileName;         // [148] 64 B

    public double DarkCloud;           // [212]  8 B
    public double Raining;             // [220]  8 B   precipitation intensity 0–1
    public double AmbientTempC;        // [228]  8 B   ambient air temperature (°C)
    public double TrackTempC;          // [236]  8 B   track surface temperature (°C)
    public LmuVect3 Wind;              // [244] 24 B   wind vector; magnitude = wind speed m/s
    public double MinPathWetness;      // [268]  8 B   minimum track wetness 0–1
    public double MaxPathWetness;      // [276]  8 B   maximum track wetness 0–1
    public byte   GameMode;            // [284]  1 B
    public byte   IsPasswordProtected; // [285]  1 B
    public ushort ServerPort;          // [286]  2 B
    public uint   ServerPublicIP;      // [288]  4 B
    public int    MaxPlayers;          // [292]  4 B

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string ServerName;          // [296] 32 B

    public float  StartET;             // [328]  4 B
    public double AvgPathWetness;      // [332]  8 B
    public float  SessionTimeRemaining;// [340]  4 B
    public float  TimeOfDay;           // [344]  4 B
    public byte   IsFixedSetup;        // [348]  1 B
    public byte   TrackGripLevel;      // [349]  1 B
    public byte   CloudCoverage;       // [350]  1 B
    public byte   TrackLimitsStepsPerPenalty; // [351] 1 B
    public byte   TrackLimitsStepsPerPoint;   // [352] 1 B

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 187)]
    public byte[] Expansion;           // [353] 187 B

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] VehiclePointer;      // [540]  8 B
    // Total: 548 bytes

    // ── Convenience properties ────────────────────────────────────────────────

    /// <summary>Wind speed in m/s (magnitude of the Wind vector).</summary>
    public readonly float WindSpeedMps =>
        (float)Math.Sqrt(Wind.X * Wind.X + Wind.Y * Wind.Y + Wind.Z * Wind.Z);

    /// <summary>
    /// Wind direction in degrees (0–360), measured clockwise from north (+Z axis).
    /// Returns 0 when there is no wind.
    /// </summary>
    public readonly float WindDirectionDeg
    {
        get
        {
            if (Wind.X == 0.0 && Wind.Z == 0.0) return 0f;
            double deg = Math.Atan2(Wind.X, Wind.Z) * (180.0 / Math.PI);
            if (deg < 0.0) deg += 360.0;
            return (float)deg;
        }
    }
}

// ── Per-vehicle scoring ───────────────────────────────────────────────────────

/// <summary>
/// Per-vehicle scoring data from the LMU_Data shared memory buffer.
/// Array starts at offset <see cref="LmuSharedMemoryLayout.ScoringVehiclesOffset"/> (2192).
/// Each entry is 584 bytes; up to <see cref="LmuSharedMemoryLayout.MaxVehicles"/> (104) entries.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
internal struct LmuVehicleScoring
{
    public int    Id;                  // [0]    4 B   slot ID

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DriverName;          // [4]   32 B

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string VehicleName;         // [36]  64 B

    public short  TotalLaps;           // [100]  2 B   completed laps
    public sbyte  Sector;              // [102]  1 B   0=sector3,1=sector1,2=sector2
    public sbyte  FinishStatus;        // [103]  1 B   0=none,1=finished,2=dnf,3=dq
    public double LapDist;             // [104]  8 B   distance into current lap (metres)
    public double PathLateral;         // [112]  8 B
    public double TrackEdge;           // [120]  8 B
    public double BestSector1;         // [128]  8 B
    public double BestSector2;         // [136]  8 B
    public double BestLapTime;         // [144]  8 B   best lap time (seconds)
    public double LastSector1;         // [152]  8 B
    public double LastSector2;         // [160]  8 B
    public double LastLapTime;         // [168]  8 B
    public double CurSector1;          // [176]  8 B
    public double CurSector2;          // [184]  8 B
    public short  NumPitstops;         // [192]  2 B
    public short  NumPenalties;        // [194]  2 B
    public byte   IsPlayer;            // [196]  1 B   1 = this is the player's vehicle
    public sbyte  Control;             // [197]  1 B
    public byte   InPits;              // [198]  1 B   1 = between pit entrance and exit
    public byte   Place;               // [199]  1 B   1-based overall position

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string VehicleClass;        // [200] 32 B

    public double TimeBehindNext;      // [232]  8 B
    public int    LapsBehindNext;      // [240]  4 B
    public double TimeBehindLeader;    // [244]  8 B
    public int    LapsBehindLeader;    // [252]  4 B
    public double LapStartET;          // [256]  8 B
    public LmuVect3 Pos;               // [264] 24 B   world position (metres)
    public LmuVect3 LocalVel;          // [288] 24 B   velocity (m/s) in local vehicle coords
    public LmuVect3 LocalAccel;        // [312] 24 B
    public LmuVect3 Ori0;              // [336] 24 B   orientation matrix row 0
    public LmuVect3 Ori1;              // [360] 24 B   orientation matrix row 1
    public LmuVect3 Ori2;              // [384] 24 B   orientation matrix row 2
    public LmuVect3 LocalRot;          // [408] 24 B
    public LmuVect3 LocalRotAccel;     // [432] 24 B
    public byte   Headlights;          // [456]  1 B
    public byte   PitState;            // [457]  1 B   0=none,1=req,2=entering,3=stopped,4=exiting
    public byte   ServerScored;        // [458]  1 B
    public byte   IndividualPhase;     // [459]  1 B
    public int    Qualification;       // [460]  4 B
    public double TimeIntoLap;         // [464]  8 B
    public double EstimatedLapTime;    // [472]  8 B

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 24)]
    public string PitGroup;            // [480] 24 B

    public byte   Flag;                // [504]  1 B
    public byte   UnderYellow;         // [505]  1 B
    public byte   CountLapFlag;        // [506]  1 B
    public byte   InGarageStall;       // [507]  1 B   1 = in garage stall (not on track)

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] UpgradePack;         // [508] 16 B

    public float  PitLapDist;          // [524]  4 B
    public float  BestLapSector1;      // [528]  4 B
    public float  BestLapSector2;      // [532]  4 B
    public ulong  SteamId;             // [536]  8 B

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string VehFilename;         // [544] 32 B

    public short  AttackMode;          // [576]  2 B
    public byte   FuelFraction;        // [578]  1 B   0x00=0%, 0xFF=100%
    public byte   DrsState;            // [579]  1 B

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public byte[] Expansion;           // [580]  4 B
    // Total: 584 bytes

    // ── Convenience properties ────────────────────────────────────────────────

    /// <summary>Speed in m/s computed from the local velocity vector.</summary>
    public readonly float SpeedMps =>
        (float)Math.Sqrt(LocalVel.X * LocalVel.X
                       + LocalVel.Y * LocalVel.Y
                       + LocalVel.Z * LocalVel.Z);

    /// <summary>
    /// True when this slot is occupied (driver name present and lap distance valid).
    /// </summary>
    public readonly bool IsActive =>
        !string.IsNullOrEmpty(DriverName) && LapDist >= 0.0;
}

// ── Snapshot record ───────────────────────────────────────────────────────────

/// <summary>Immutable snapshot of LMU scoring data for one polling tick.</summary>
internal sealed record LmuScoringSnapshot(
    LmuScoringInfo      Info,
    LmuVehicleScoring[] Vehicles);
