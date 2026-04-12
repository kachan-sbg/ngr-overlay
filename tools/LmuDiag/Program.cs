// LmuDiag — live shared memory inspector for Le Mans Ultimate.
//
// Run this while LMU is open. Compare each displayed value against the in-game HUD
// to confirm all struct offsets are correct.
//
// Press S to save a snapshot to LmuDiag_<timestamp>.txt
// Press Q to quit

using System.IO.MemoryMappedFiles;
using System.Text;

// ── Constants (self-contained — must stay in sync with LmuSharedMemoryLayout) ─

const string MMF = "LMU_Data";

// Absolute buffer offsets
const int SCORING_INFO     = 1632;   // start of ScoringInfoV01
const int SCORING_VEHICLES = 2192;   // 1632 + 560: ScoringInfoV01(548) + pad(4) + size_t(8)
const int VEH_SCORING_SIZE = 584;    // sizeof VehicleScoringInfoV01  (pack4)
const int MAX_VEHICLES     = 104;
const int TELEM_HEADER     = 128464; // start of SharedMemoryTelemetryData
const int TELEM_VEHICLES   = 128468; // TELEM_HEADER + 4 (3 header bytes + 1 pad)
const int TELEM_VEH_SIZE   = 1888;   // sizeof TelemInfoV01 (pack4)

// ScoringInfoV01 field offsets (pack4, from SCORING_INFO):
const int SI_TrackName     = 0;    // char[64]
const int SI_Session       = 64;   // long(4)
const int SI_CurrentET     = 68;   // double
const int SI_TrackLen      = 88;   // double  (mLapDist = track length in metres)
const int SI_NumVehicles   = 104;  // long(4)
const int SI_GamePhase     = 108;  // uint8
const int SI_InRealtime    = 115;  // bool
const int SI_PlayerName    = 116;  // char[32]

// VehicleScoringInfoV01 field offsets (pack4, from vehicle base):
const int VS_Id            = 0;    // long(4)
const int VS_DriverName    = 4;    // char[32]
const int VS_VehicleName   = 36;   // char[64]
const int VS_TotalLaps     = 100;  // short(2)
const int VS_LapDist       = 104;  // double
const int VS_BestLapTime   = 144;  // double
const int VS_LastLapTime   = 168;  // double
const int VS_NumPitstops   = 192;  // short(2)
const int VS_IsPlayer      = 196;  // bool
const int VS_Place         = 199;  // uint8
const int VS_VehicleClass  = 200;  // char[32]
const int VS_TimeBehindLdr = 244;  // double
const int VS_LocalVelX     = 288;  // double  (local velocity x)
const int VS_LocalVelY     = 296;  // double  (local velocity y)
const int VS_LocalVelZ     = 304;  // double  (local velocity z)
const int VS_PitState      = 457;  // uint8  (0=none,1=req,2=entering,3=stopped,4=exiting)
const int VS_InGarageStall = 507;  // bool
const int VS_FuelFraction  = 578;  // uint8  (0x00=0%, 0xFF=100%)
const int VS_EstLapTime    = 472;  // double

// TelemInfoV01 field offsets (pack4, from vehicle base):
const int TI_Gear          = 352;  // long(4)  mGear
const int TI_EngineRPM     = 356;  // double   mEngineRPM
const int TI_UnfThrottle   = 388;  // double   mUnfilteredThrottle
const int TI_FiltThrottle  = 420;  // double   mFilteredThrottle
const int TI_UnfBrake      = 396;  // double   mUnfilteredBrake
const int TI_FiltBrake     = 428;  // double   mFilteredBrake
const int TI_UnfSteering   = 404;  // double   mUnfilteredSteering
const int TI_UnfClutch     = 412;  // double   mUnfilteredClutch
const int TI_Fuel          = 524;  // double   mFuel (litres)
const int TI_FuelCapacity  = 608;  // double   mFuelCapacity (litres)
const int TI_SpeedLimiter  = 748;  // bool     mSpeedLimiterActive

// ── Entry point ───────────────────────────────────────────────────────────────

Console.OutputEncoding = Encoding.UTF8;
Console.CursorVisible  = false;

Console.WriteLine("LmuDiag — waiting for LMU_Data shared memory...");
Console.WriteLine("Press Q to quit, S to save snapshot.");

while (true)
{
    if (Console.KeyAvailable)
    {
        var k = Console.ReadKey(true).Key;
        if (k == ConsoleKey.Q) break;
    }

    MemoryMappedFile? mmf = null;
    try { mmf = MemoryMappedFile.OpenExisting(MMF); }
    catch { Thread.Sleep(1000); continue; }

    Console.Clear();
    Console.WriteLine($"LmuDiag  [{MMF} OPEN]  S=snapshot  Q=quit");

    using (mmf)
    using (var view = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
    {
        while (true)
        {
            if (Console.KeyAvailable)
            {
                var k = Console.ReadKey(true).Key;
                if (k == ConsoleKey.Q) goto done;
                if (k == ConsoleKey.S) SaveSnapshot(view);
            }

            unsafe
            {
                byte* buf = null;
                view.SafeMemoryMappedViewHandle.AcquirePointer(ref buf);
                try
                {
                    Console.SetCursorPosition(0, 0);
                    var sb = new StringBuilder(4096);
                    RenderDisplay(buf, sb);
                    Console.Write(sb);
                }
                finally { view.SafeMemoryMappedViewHandle.ReleasePointer(); }
            }

            Thread.Sleep(500);
        }
    }
}
done:
Console.CursorVisible = true;

// ── Rendering ─────────────────────────────────────────────────────────────────

static unsafe void RenderDisplay(byte* buf, StringBuilder sb)
{
    sb.AppendLine($"LmuDiag  [LMU_Data OPEN]  S=snapshot  Q=quit  {DateTime.Now:HH:mm:ss}    ");
    sb.AppendLine();

    // ── Scoring info ──────────────────────────────────────────────────────────
    string trackName  = ReadStr(buf, SCORING_INFO + SI_TrackName, 64);
    int    session    = *(int*)  (buf + SCORING_INFO + SI_Session);
    double currentET  = *(double*)(buf + SCORING_INFO + SI_CurrentET);
    double trackLen   = *(double*)(buf + SCORING_INFO + SI_TrackLen);
    int    numVeh     = *(int*)  (buf + SCORING_INFO + SI_NumVehicles);
    byte   gamePhase  = *(buf + SCORING_INFO + SI_GamePhase);
    byte   inRealtime = *(buf + SCORING_INFO + SI_InRealtime);
    string playerName = ReadStr(buf, SCORING_INFO + SI_PlayerName, 32);

    sb.AppendLine("─── Scoring Info (offset 1632) " + new string('─', 40));
    sb.AppendLine($"  Track          : {V(trackName)}");
    sb.AppendLine($"  Player name    : {V(playerName)}");
    sb.AppendLine($"  Session        : {session}  ({SessionName(session)})");
    sb.AppendLine($"  CurrentET      : {currentET:F1} s");
    sb.AppendLine($"  Track length   : {trackLen:F0} m");
    sb.AppendLine($"  NumVehicles    : {numVeh}  {Plausible(numVeh >= 0 && numVeh <= 104)}");
    sb.AppendLine($"  GamePhase      : {gamePhase}  ({GamePhaseName(gamePhase)})");
    sb.AppendLine($"  InRealtime     : {inRealtime}  {(inRealtime != 0 ? "[ACTIVE]" : "[monitor/paused]")}");
    sb.AppendLine();

    // ── Find player vehicle ───────────────────────────────────────────────────
    int playerIdx = -1;
    for (int i = 0; i < Math.Min(numVeh, MAX_VEHICLES); i++)
    {
        long vbase = SCORING_VEHICLES + (long)i * VEH_SCORING_SIZE;
        if (*(buf + vbase + VS_IsPlayer) != 0) { playerIdx = i; break; }
    }

    if (playerIdx < 0)
    {
        sb.AppendLine("─── Player Vehicle ─────────────────────────────────────────────────");
        sb.AppendLine("  IsPlayer flag not set in any vehicle slot.");
        sb.AppendLine();
    }
    else
    {
        long vb = SCORING_VEHICLES + (long)playerIdx * VEH_SCORING_SIZE;

        int    slotId      = *(int*)   (buf + vb + VS_Id);
        string driverName  = ReadStr(buf, (int)(vb + VS_DriverName), 32);
        string vehName     = ReadStr(buf, (int)(vb + VS_VehicleName), 64);
        string vehClass    = ReadStr(buf, (int)(vb + VS_VehicleClass), 32);
        short  totalLaps   = *(short*) (buf + vb + VS_TotalLaps);
        double lapDist     = *(double*)(buf + vb + VS_LapDist);
        double bestLap     = *(double*)(buf + vb + VS_BestLapTime);
        double lastLap     = *(double*)(buf + vb + VS_LastLapTime);
        double estLap      = *(double*)(buf + vb + VS_EstLapTime);
        short  pitstops    = *(short*) (buf + vb + VS_NumPitstops);
        byte   place       = *(buf + vb + VS_Place);
        byte   pitState    = *(buf + vb + VS_PitState);
        byte   inGarage    = *(buf + vb + VS_InGarageStall);
        byte   fuelFrac    = *(buf + vb + VS_FuelFraction);
        double timeBehLdr  = *(double*)(buf + vb + VS_TimeBehindLdr);

        double velX = *(double*)(buf + vb + VS_LocalVelX);
        double velY = *(double*)(buf + vb + VS_LocalVelY);
        double velZ = *(double*)(buf + vb + VS_LocalVelZ);
        double speedMps = Math.Sqrt(velX * velX + velY * velY + velZ * velZ);
        double lapPct   = trackLen > 0 ? lapDist / trackLen * 100.0 : 0;

        sb.AppendLine($"─── Player Vehicle Scoring (scoring array idx={playerIdx}, offset {SCORING_VEHICLES + playerIdx * VEH_SCORING_SIZE}) " + new string('─', 10));
        sb.AppendLine($"  Slot ID        : {slotId}");
        sb.AppendLine($"  Driver         : {V(driverName)}");
        sb.AppendLine($"  Vehicle        : {V(vehName)}");
        sb.AppendLine($"  Class          : {V(vehClass)}");
        sb.AppendLine($"  Position       : P{place}    gap to leader: {FormatGap(timeBehLdr)}");
        sb.AppendLine($"  Laps done      : {totalLaps}");
        sb.AppendLine($"  LapDist        : {lapDist:F1} m / {trackLen:F0} m  ({lapPct:F1}%)");
        sb.AppendLine($"  Best lap       : {FormatTime(bestLap)}");
        sb.AppendLine($"  Last lap       : {FormatTime(lastLap)}");
        sb.AppendLine($"  Est lap        : {FormatTime(estLap)}");
        sb.AppendLine($"  Speed (LocalVel): {speedMps * 3.6:F1} km/h  ({speedMps:F2} m/s)");
        sb.AppendLine($"  PitState       : {pitState}  ({PitStateName(pitState)})   InGarage: {inGarage}");
        sb.AppendLine($"  Pitstops       : {pitstops}");
        sb.AppendLine($"  FuelFraction   : {fuelFrac} / 255  ({fuelFrac / 255.0 * 100:F1}%)");
        sb.AppendLine();
    }

    // ── Telemetry header ──────────────────────────────────────────────────────
    byte telemActive    = *(buf + TELEM_HEADER + 0);
    byte telemPlayerIdx = *(buf + TELEM_HEADER + 1);
    byte telemHasVeh    = *(buf + TELEM_HEADER + 2);

    sb.AppendLine($"─── Telemetry Header (offset {TELEM_HEADER}) " + new string('─', 40));
    sb.AppendLine($"  activeVehicles : {telemActive}");
    sb.AppendLine($"  playerVehIdx   : {telemPlayerIdx}  {Plausible(telemPlayerIdx < MAX_VEHICLES)}");
    sb.AppendLine($"  playerHasVeh   : {telemHasVeh}");
    if (playerIdx >= 0)
        sb.AppendLine($"  scoring idx    : {playerIdx}  {(playerIdx == telemPlayerIdx ? "[matches]" : "[MISMATCH — scoring idx != telem idx]")}");
    sb.AppendLine();

    // ── Player telemetry ──────────────────────────────────────────────────────
    if (telemHasVeh != 0 && telemPlayerIdx < MAX_VEHICLES)
    {
        long tb = TELEM_VEHICLES + (long)telemPlayerIdx * TELEM_VEH_SIZE;

        int   gear        = *(int*)   (buf + tb + TI_Gear);
        double rpm        = *(double*)(buf + tb + TI_EngineRPM);
        double unfThrottle= *(double*)(buf + tb + TI_UnfThrottle);
        double filtThrottle=*(double*)(buf + tb + TI_FiltThrottle);
        double unfBrake   = *(double*)(buf + tb + TI_UnfBrake);
        double filtBrake  = *(double*)(buf + tb + TI_FiltBrake);
        double unfSteer   = *(double*)(buf + tb + TI_UnfSteering);
        double unfClutch  = *(double*)(buf + tb + TI_UnfClutch);
        double fuel       = *(double*)(buf + tb + TI_Fuel);
        double fuelCap    = *(double*)(buf + tb + TI_FuelCapacity);
        byte   limiterOn  = *(buf + tb + TI_SpeedLimiter);

        sb.AppendLine($"─── Player Telemetry (telem array idx={telemPlayerIdx}, offset {TELEM_VEHICLES + telemPlayerIdx * TELEM_VEH_SIZE}) " + new string('─', 8));
        sb.AppendLine($"  Gear           : {GearName(gear)}    {Plausible(gear >= -1 && gear <= 12)}");
        sb.AppendLine($"  Engine RPM     : {rpm:F0}    {Plausible(rpm >= 0 && rpm < 25000)}");
        sb.AppendLine($"  Throttle (unf) : {unfThrottle:F3}    filtered: {filtThrottle:F3}");
        sb.AppendLine($"  Brake    (unf) : {unfBrake:F3}    filtered: {filtBrake:F3}");
        sb.AppendLine($"  Steering (unf) : {unfSteer:F3}");
        sb.AppendLine($"  Clutch   (unf) : {unfClutch:F3}");
        sb.AppendLine($"  Fuel           : {fuel:F2} L    {Plausible(fuel >= 0 && fuel <= 200)}");
        sb.AppendLine($"  FuelCapacity   : {fuelCap:F1} L    {Plausible(fuelCap > 0 && fuelCap <= 200)}");
        sb.AppendLine($"  SpeedLimiter   : {limiterOn}    (offset {TI_SpeedLimiter} within TelemInfoV01)");
        sb.AppendLine();
    }
    else
    {
        sb.AppendLine("─── Player Telemetry ───────────────────────────────────────────────");
        sb.AppendLine("  playerHasVehicle = 0 or idx out of range.");
        sb.AppendLine();
    }

    // ── All vehicles (compact) ────────────────────────────────────────────────
    sb.AppendLine("─── All Vehicles (compact) " + new string('─', 50));
    sb.AppendLine("  P   Name                             Class            Laps    BestLap   Speed");
    int n = Math.Min(numVeh, MAX_VEHICLES);
    for (int i = 0; i < n; i++)
    {
        long vb   = SCORING_VEHICLES + (long)i * VEH_SCORING_SIZE;
        byte  pos = *(buf + vb + VS_Place);
        bool  isP = *(buf + vb + VS_IsPlayer) != 0;
        string nm = ReadStr(buf, (int)(vb + VS_DriverName), 32);
        string cl = ReadStr(buf, (int)(vb + VS_VehicleClass), 32);
        short  lp = *(short*)(buf + vb + VS_TotalLaps);
        double bl = *(double*)(buf + vb + VS_BestLapTime);
        double vx = *(double*)(buf + vb + VS_LocalVelX);
        double vy = *(double*)(buf + vb + VS_LocalVelY);
        double vz = *(double*)(buf + vb + VS_LocalVelZ);
        double sp = Math.Sqrt(vx*vx + vy*vy + vz*vz) * 3.6;

        string marker = isP ? "*" : " ";
        sb.AppendLine($"  {marker}{pos,2}  {nm,-32}  {cl,-16}  {lp,4}  {FormatTime(bl),9}  {sp,6:F1}");
    }
    if (numVeh == 0) sb.AppendLine("  (no vehicles)");
    sb.AppendLine();

    // ── Raw bytes at key offsets ──────────────────────────────────────────────
    sb.AppendLine("─── Raw byte anchors " + new string('─', 56));
    AppendRawBytes(sb, buf, "NumVehicles (+104)",   SCORING_INFO + SI_NumVehicles,   4);
    AppendRawBytes(sb, buf, "InRealtime  (+115)",   SCORING_INFO + SI_InRealtime,    1);
    AppendRawBytes(sb, buf, "GamePhase   (+108)",   SCORING_INFO + SI_GamePhase,     1);
    AppendRawBytes(sb, buf, "VehSlot0.IsPlayer",    SCORING_VEHICLES + VS_IsPlayer,  1);
    AppendRawBytes(sb, buf, "TelemHdr active/idx/has", TELEM_HEADER,                 3);
    if (telemPlayerIdx < MAX_VEHICLES)
        AppendRawBytes(sb, buf, $"Telem[{telemPlayerIdx}].Gear (+352)", TELEM_VEHICLES + telemPlayerIdx * TELEM_VEH_SIZE + TI_Gear, 4);
}

// ── Snapshot ──────────────────────────────────────────────────────────────────

static unsafe void SaveSnapshot(MemoryMappedViewAccessor view)
{
    byte* buf = null;
    view.SafeMemoryMappedViewHandle.AcquirePointer(ref buf);
    try
    {
        var sb = new StringBuilder(8192);
        sb.AppendLine($"LmuDiag snapshot — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        RenderDisplay(buf, sb);
        string path = $"LmuDiag_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        File.WriteAllText(path, sb.ToString());
        Console.SetCursorPosition(0, Console.WindowHeight - 1);
        Console.Write($"Saved: {path}                    ");
    }
    finally { view.SafeMemoryMappedViewHandle.ReleasePointer(); }
}

// ── Helpers ───────────────────────────────────────────────────────────────────

static unsafe string ReadStr(byte* buf, int offset, int maxLen)
{
    int len = 0;
    while (len < maxLen && buf[offset + len] != 0) len++;
    return len == 0 ? "" : Encoding.Latin1.GetString(buf + offset, len);
}

static unsafe void AppendRawBytes(StringBuilder sb, byte* buf, string label, long offset, int count)
{
    var hex = new StringBuilder();
    for (int i = 0; i < count; i++) hex.Append($"{buf[offset + i]:X2} ");
    sb.AppendLine($"  {label,-32} @{offset,7}: {hex}");
}

static string V(string s) => string.IsNullOrEmpty(s) ? "(empty)" : s;

static string Plausible(bool ok) => ok ? "✓" : "✗ UNEXPECTED";

static string FormatTime(double t)
{
    if (t <= 0) return "  --:--.---";
    int m = (int)(t / 60);
    double s = t - m * 60;
    return $"{m}:{s:00.000}";
}

static string FormatGap(double t) => t <= 0 ? "leader" : $"+{t:F3}s";

static string GearName(int g) => g switch { -1 => "R", 0 => "N", _ => g.ToString() };

static string SessionName(int s) => s switch
{
    0 => "Test day",
    1 or 2 or 3 or 4 => $"Practice {s}",
    5 or 6 or 7 or 8 => $"Qualify {s - 4}",
    9 => "Warmup",
    10 or 11 or 12 or 13 => $"Race {s - 9}",
    _ => "?"
};

static string GamePhaseName(byte p) => p switch
{
    0 => "Before session",
    1 => "Recon laps",
    2 => "Grid walk",
    3 => "Formation lap",
    4 => "Countdown",
    5 => "Green flag",
    6 => "Full-course yellow",
    7 => "Session stopped",
    8 => "Session over",
    9 => "Paused",
    _ => "?"
};

static string PitStateName(byte p) => p switch
{
    0 => "None", 1 => "Requested", 2 => "Entering",
    3 => "Stopped", 4 => "Exiting", _ => "?"
};
