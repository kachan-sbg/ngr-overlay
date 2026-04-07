using System.Runtime.InteropServices;
using System.Text;

namespace SimOverlay.Sim.LMU.SharedMemory;

/// <summary>
/// Constants for the rF2/LMU shared memory layout.
/// </summary>
internal static class Rf2Structs
{
    /// <summary>Size of the version block at the start of every mapped file (2 × uint32).</summary>
    public const int VersionBlockSize = 8;

    /// <summary>Maximum number of vehicle slots in the scoring and telemetry arrays.</summary>
    public const int MaxVehicles = 128;
}

// ── Version block ─────────────────────────────────────────────────────────────

/// <summary>
/// Sits at offset 0 in every rF2 mapped file.
/// The writer increments <see cref="VersionUpdateBegin"/> before writing and
/// <see cref="VersionUpdateEnd"/> after.  If both values are equal the reader
/// obtained a consistent (non-torn) snapshot.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct Rf2MappedBufferVersionBlock
{
    public uint VersionUpdateBegin;  // incremented before write
    public uint VersionUpdateEnd;    // incremented after write; matches Begin when done
}

// ── Scoring ───────────────────────────────────────────────────────────────────

/// <summary>
/// Session-level data from <c>$rFactor2SMMP_Scoring$</c>.
/// <para>
/// Struct layout matches the 64-bit rF2/LMU InternalsPlugin with <c>#pragma pack(4)</c>.
/// Key assumptions: C++ <c>long</c> = 4 bytes (MSVC), <c>char*</c> pointer = 8 bytes (64-bit),
/// <c>bool</c> = 1 byte.  Doubles are aligned to 4 bytes (Pack=4 overrides natural 8-byte alignment).
/// </para>
/// <para>Offset key: starts at byte 8 in the mapped file (after the 8-byte version block).</para>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
internal struct Rf2ScoringInfo
{
    // [0]  64 bytes
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string TrackName;

    public int    Session;          // [64]  4 B   0=testday,1-4=practice,5-8=qualify,9=warmup,10-13=race
    public double CurrentET;        // [68]  8 B   current session time (seconds)
    public double EndET;            // [76]  8 B   session end time; ≤0 means laps-based
    public int    MaxLaps;          // [84]  4 B   max laps (0 = time-based)
    public double LapDist;          // [88]  8 B   track length (metres)

    // Pointer field (char* mResultsStream): 8 bytes on 64-bit — value is irrelevant in SHM.
    public long   ResultsStreamPtr; // [96]  8 B   (do not dereference)

    public int    NumVehicles;      // [104] 4 B
    public byte   GamePhase;        // [108] 1 B   0=before session, 5=green, 7=caution, 8=race over
    public sbyte  YellowFlagState;  // [109] 1 B   -1=invalid, 0=none, 1=pending, 2=pit, 3=resume

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public sbyte[] SectorFlag;      // [110] 3 B   per-sector yellow flags

    public byte StartLight;         // [113] 1 B
    public byte NumRedLights;       // [114] 1 B
    public byte InRealtime;         // [115] 1 B   1 = live (not paused / replay)

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string PlayerName;       // [116] 32 B

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string PlrFileName;      // [148] 64 B

    public double Darkness;         // [212] 8 B   0=max light, 1=max dark
    public double Raining;          // [220] 8 B   precipitation intensity 0–1
    public double Temperature;      // [228] 8 B   ambient air temperature (°C)
    public double MinPathWetness;   // [236] 8 B   minimum track wetness 0–1
    public double MaxPathWetness;   // [244] 8 B   maximum track wetness 0–1

    // Expansion area [252..507] — 256 bytes.
    // Known additions in current rF2/LMU:
    //   Expansion[0..7]  = TrackTemp (double, °C)
    //   Expansion[8..15] = AmbientHumidity (double, 0–1)
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
    public byte[] Expansion;        // [252] 256 B
    // Total struct size: 508 bytes

    // ── Expansion accessors ───────────────────────────────────────────────────

    /// <summary>
    /// Track temperature in °C from the V02 expansion area.
    /// Returns <see cref="float.NaN"/> if the field is not populated (all-zero bytes).
    /// </summary>
    public readonly float TrackTempC
    {
        get
        {
            if (Expansion is not { Length: >= 8 }) return float.NaN;
            double v = BitConverter.ToDouble(Expansion, 0);
            // If the field is unpopulated it will be 0.0; treat as unavailable.
            return v is > 0.0 and < 100.0 ? (float)v : float.NaN;
        }
    }
}

// ── Per-vehicle scoring ───────────────────────────────────────────────────────

/// <summary>
/// Per-vehicle data from <c>$rFactor2SMMP_Scoring$</c>.
/// <para>
/// Array starts immediately after <see cref="Rf2ScoringInfo"/> in the mapped file.
/// Each entry is 512 bytes; 128 entries are allocated regardless of actual entrant count.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
internal struct Rf2VehicleScoring
{
    public int    Id;               // [0]   4 B   slot ID (not necessarily sequential)

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DriverName;       // [4]   32 B

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string VehicleName;      // [36]  64 B  vehicle model name (used as class fallback)

    public short  TotalLaps;        // [100]  2 B  completed laps
    public sbyte  Sector;           // [102]  1 B  0=sector3, 1=sector1, 2=sector2
    public sbyte  FinishStatus;     // [103]  1 B  0=none, 1=finished, 2=dnf, 3=dq

    public double LapDist;          // [104]  8 B  distance into current lap (metres, 0 → TrackLength)
    public double PathLateral;      // [112]  8 B  lateral from left path edge
    public double TrackEdge;        // [120]  8 B  distance to track edge

    public double BestST1;          // [128]  8 B  best sector 1 time (seconds)
    public double BestST2;          // [136]  8 B  best sector 2 time
    public double BestST3;          // [144]  8 B  best sector 3 time
    public double BestLapTime;      // [152]  8 B  best lap time (seconds)
    public double LastLapTime;      // [160]  8 B  last lap time (seconds)

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] Pos;            // [168]  24 B world position (metres)

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] LocalVel;       // [192]  24 B velocity (m/s) in local vehicle coords

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] LocalAccel;     // [216]  24 B acceleration in local coords

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
    public double[] Ori;            // [240]  72 B orientation matrix (3×3 row-major)

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] LocalRot;       // [312]  24 B rotation (rad/s) in local coords

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] LocalRotAccel;  // [336]  24 B rotational acceleration

    public int    ExpectedStopLap;  // [360]  4 B
    public int    PitStopCount;     // [364]  4 B  pit stops made
    public float  FuelCapacity;     // [368]  4 B  tank capacity (litres)
    public float  FuelLevel;        // [372]  4 B  relative fuel (0.0–1.0)
    public float  EngineMaxRPM;     // [376]  4 B
    public byte   ScheduledStops;   // [380]  1 B
    public byte   Headlights;       // [381]  1 B
    public byte   PitState;         // [382]  1 B  0=none,1=req,2=entering,3=stopped,4=exiting
    public byte   ServerScored;     // [383]  1 B
    public byte   IndividualPhase;  // [384]  1 B
    // 3 implicit padding bytes [385..387] to align Qualification to 4-byte boundary

    public int    Qualification;    // [388]  4 B  1-based grid position (0 = n/a)
    public double TimeIntoLap;      // [392]  8 B  time elapsed in current lap (seconds)
    public double EstimatedLapTime; // [400]  8 B  estimated lap time (seconds)

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 24)]
    public string PitGroup;         // [408]  24 B pit group / team name

    public byte Flag;               // [432]  1 B  flag shown to this vehicle
    public byte UnderYellow;        // [433]  1 B  non-zero if under yellow
    public byte CountLapFlag;       // [434]  1 B
    public byte InGarageStall;      // [435]  1 B  non-zero if in garage (not on track)

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] UpgradePack;      // [436]  16 B

    public float  PitLapDist;       // [452]  4 B
    public float  BestLapSector1;   // [456]  4 B
    public float  BestLapSector2;   // [460]  4 B

    // Expansion area [464..511] — 48 bytes.
    // V02 additions: Place (int) at [0..3], VehicleClass (char[32]) at [4..35].
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
    public byte[] Expansion;        // [464]  48 B
    // Total struct size: 512 bytes

    // ── Convenience properties ────────────────────────────────────────────────

    /// <summary>
    /// Race position (1-based) from the V02 expansion.
    /// Returns 0 if not populated by the running plugin version.
    /// </summary>
    public readonly int Place =>
        Expansion is { Length: >= 4 } ? BitConverter.ToInt32(Expansion, 0) : 0;

    /// <summary>
    /// Vehicle class name from the V02 expansion (e.g. "LMH", "LMDh").
    /// Falls back to an empty string when not populated.
    /// </summary>
    public readonly string VehicleClass =>
        Expansion is { Length: >= 36 }
            ? Encoding.ASCII.GetString(Expansion, 4, 32).TrimEnd('\0')
            : string.Empty;

    /// <summary>Absolute fuel level in litres (FuelLevel × FuelCapacity).</summary>
    public readonly float FuelLiters => FuelLevel * FuelCapacity;

    /// <summary>
    /// Speed in m/s computed from the local velocity vector.
    /// Returns 0 if <see cref="LocalVel"/> is null (uninitialized slot).
    /// </summary>
    public readonly float SpeedMps => LocalVel is { Length: >= 3 }
        ? MathF.Sqrt((float)(LocalVel[0] * LocalVel[0]
                           + LocalVel[1] * LocalVel[1]
                           + LocalVel[2] * LocalVel[2]))
        : 0f;

    /// <summary>
    /// True when this slot is occupied (driver name present and lap distance valid).
    /// </summary>
    public readonly bool IsActive =>
        !string.IsNullOrEmpty(DriverName) && LapDist >= 0.0;
}

// ── Telemetry (partial) ───────────────────────────────────────────────────────

/// <summary>
/// Reads the first section of a vehicle telemetry entry from <c>$rFactor2SMMP_Telemetry$</c>,
/// covering the fields needed for throttle/brake/steering/gear/RPM.
/// <para>
/// Only the fields up to and including the unfiltered driver inputs are marshalled;
/// the remainder of each entry (wheel physics etc.) is skipped via the stride-based reader.
/// Stride is computed from the mapped file size at runtime rather than hardcoded.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
internal struct Rf2VehicleTelemetryInputs
{
    public int    Id;               // [0]   4 B  slot ID (matches scoring Id)
    public double DeltaTime;        // [4]   8 B
    public double ElapsedTime;      // [12]  8 B
    public int    LapNumber;        // [20]  4 B

    public double LapStartET;       // [24]  8 B

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string VehicleName;      // [32]  64 B

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string TrackName;        // [96]  64 B

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] Pos;            // [160] 24 B

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] LocalVel;       // [184] 24 B  speed = magnitude

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] LocalAccel;     // [208] 24 B

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
    public double[] Ori;            // [232] 72 B

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] LocalRot;       // [304] 24 B

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] LocalRotAccel;  // [328] 24 B

    public int    Gear;             // [352]  4 B  -1=reverse, 0=neutral, 1+=forward
    public double EngineRPM;        // [356]  8 B
    public double EngineWaterTemp;  // [364]  8 B
    public double EngineOilTemp;    // [372]  8 B
    public double ClutchRPM;        // [380]  8 B

    public double UnfilteredThrottle; // [388] 8 B  0.0–1.0
    public double UnfilteredBrake;    // [396] 8 B  0.0–1.0
    public double UnfilteredSteering; // [404] 8 B  -1.0–1.0 (left negative)
    public double UnfilteredClutch;   // [412] 8 B  0.0–1.0
    // Total marshalled: 420 bytes; remainder of the full struct skipped by stride reader.
}
