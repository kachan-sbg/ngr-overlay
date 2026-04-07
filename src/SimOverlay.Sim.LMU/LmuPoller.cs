using System.Collections.Immutable;
using SimOverlay.Core;
using SimOverlay.Sim.Contracts;
using SimOverlay.Sim.LMU.SharedMemory;

namespace SimOverlay.Sim.LMU;

/// <summary>
/// Polls the rF2/LMU shared memory at ~60 Hz and publishes data to the
/// <see cref="ISimDataBus"/>:
/// <list type="bullet">
///   <item><see cref="DriverData"/> — 60 Hz</item>
///   <item><see cref="TelemetryData"/> — 60 Hz</item>
///   <item><see cref="RelativeData"/> — 10 Hz</item>
///   <item><see cref="PitData"/> — 10 Hz</item>
///   <item><see cref="TrackMapData"/> — 10 Hz</item>
///   <item><see cref="WeatherData"/> — 1 Hz</item>
///   <item><see cref="SessionData"/> — on session change (track / vehicle count)</item>
/// </list>
/// </summary>
internal sealed class LmuPoller : IDisposable
{
    private const int RelativePublishInterval = 6;  // 60 Hz ÷ 6 = 10 Hz
    private const int WeatherPublishInterval  = 60; // 60 Hz ÷ 60 = 1 Hz

    private readonly ISimDataBus      _bus;
    private readonly Action<SimState> _onStateChanged;
    private readonly Rf2MemoryReader  _reader;
    private readonly LmuFuelTracker   _fuelTracker = new();

    // Cached driver list, rebuilt when track or vehicle count changes.
    private ImmutableArray<LmuDriverSnapshot> _cachedDrivers =
        ImmutableArray<LmuDriverSnapshot>.Empty;

    // Used to detect session changes without full decode every tick.
    private string _lastTrackName     = "";
    private int    _lastNumVehicles   = -1;
    private int    _lastSession       = -1;

    // Track length cached from session data (metres).
    private double _trackLengthMeters;

    // Player's slot ID (from matching PlayerName against scoring entries).
    private int _playerSlotId = -1;

    private int  _frameCount;
    private bool _inSession;
    private bool _disposed;

    private Timer? _timer;

    // ── Construction ─────────────────────────────────────────────────────────

    public LmuPoller(ISimDataBus bus, Action<SimState> onStateChanged)
    {
        _bus            = bus;
        _onStateChanged = onStateChanged;
        _reader         = new Rf2MemoryReader();
    }

    public void Start()
    {
        // Open scoring first; telemetry is optional.
        if (!_reader.TryOpenScoring())
        {
            AppLog.Error("LmuPoller: failed to open scoring shared memory.");
            return;
        }
        _reader.TryOpenTelemetry(); // best-effort; silently ignored if unavailable

        // Poll at ~60 Hz (16 ms interval).
        _timer = new Timer(Poll, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(16));
        AppLog.Info("LmuPoller started.");
    }

    // ── Polling ───────────────────────────────────────────────────────────────

    private void Poll(object? state)
    {
        if (_disposed) return;

        try
        {
            var snapshot = _reader.ReadScoring();
            if (snapshot == null) return; // torn read — skip tick

            var info = snapshot.Info;

            // Detect session start / track change.
            bool sessionChanged = info.TrackName != _lastTrackName
                               || info.NumVehicles != _lastNumVehicles
                               || info.Session != _lastSession;

            if (sessionChanged)
                HandleSessionChange(snapshot);

            if (!_inSession) return;

            // Find player vehicle.
            var playerNullable = FindPlayer(snapshot.Vehicles, info.PlayerName);
            if (playerNullable == null) return;
            var player = playerNullable.Value;

            // Publish high-frequency data every tick.
            PublishDriverData(player, info);
            PublishTelemetryData(player, info);

            // Throttle lower-frequency data.
            if (++_frameCount % RelativePublishInterval == 0)
            {
                PublishRelativeData(snapshot, player);
                PublishPitData(player, info);
                PublishTrackMapData(snapshot, player);
            }

            if (_frameCount % WeatherPublishInterval == 0)
                PublishWeatherData(info);
        }
        catch (Exception ex)
        {
            AppLog.Exception("LmuPoller.Poll", ex);
        }
    }

    // ── Session handling ──────────────────────────────────────────────────────

    private void HandleSessionChange(Rf2ScoringSnapshot snapshot)
    {
        var info = snapshot.Info;

        _lastTrackName   = info.TrackName;
        _lastNumVehicles = info.NumVehicles;
        _lastSession     = info.Session;
        _trackLengthMeters = info.LapDist > 0 ? info.LapDist : _trackLengthMeters;

        var (session, drivers) = LmuSessionDecoder.Decode(info, snapshot.Vehicles);
        _cachedDrivers = drivers.ToImmutableArray();

        // Resolve player slot ID.
        _playerSlotId = -1;
        foreach (var v in snapshot.Vehicles)
        {
            if (v.DriverName == info.PlayerName)
            {
                _playerSlotId = v.Id;
                break;
            }
        }

        _bus.Publish(session);

        bool wasInSession = _inSession;
        _inSession = info.InRealtime != 0 && info.NumVehicles > 0;

        if (!wasInSession && _inSession)
        {
            _fuelTracker.Reset();
            _onStateChanged(SimState.InSession);
            AppLog.Info($"LmuPoller: session started — {info.TrackName} / session {info.Session}");
        }
        else if (wasInSession && !_inSession)
        {
            _onStateChanged(SimState.Connected);
            AppLog.Info("LmuPoller: session ended — waiting for new session.");
        }
        else if (!_inSession && info.NumVehicles > 0)
        {
            _inSession = true;
            _fuelTracker.Reset();
            _onStateChanged(SimState.InSession);
            AppLog.Info($"LmuPoller: session active — {info.TrackName}");
        }

        AppLog.Info(
            $"LmuPoller: session info updated — track={info.TrackName}, " +
            $"vehicles={info.NumVehicles}, session={info.Session}, " +
            $"trackLen={_trackLengthMeters:F0}m, playerSlot={_playerSlotId}");
    }

    // ── Data publication ──────────────────────────────────────────────────────

    private void PublishDriverData(Rf2VehicleScoring player, Rf2ScoringInfo info)
    {
        // Compute position from the player's cached snapshot (V02 Place field, else 0).
        var playerDriver = _cachedDrivers.FirstOrDefault(d => d.SlotId == _playerSlotId);
        int position = 0;
        if (playerDriver != null)
        {
            // Resolve from the live scoring entry's Place expansion field.
            foreach (var v in _cachedDrivers)
            {
                if (v.SlotId == _playerSlotId)
                    break;
            }
        }

        // Try Place from scoring expansion first, fall back to counting.
        position = FindPlayerPosition(player);

        _bus.Publish(new DriverData
        {
            Position              = position,
            Lap                   = player.TotalLaps + 1,
            BestLapTime           = player.BestLapTime > 0 ? TimeSpan.FromSeconds(player.BestLapTime) : TimeSpan.Zero,
            LastLapTime           = player.LastLapTime > 0 ? TimeSpan.FromSeconds(player.LastLapTime) : TimeSpan.Zero,
            // rF2 does not expose a direct delta-to-best; approximate as (LastLap - BestLap).
            LapDeltaVsBestLap     = ComputeDelta(player),
            LapDeltaVsSessionBest = 0f, // not available from rF2 scoring
        });
    }

    private void PublishTelemetryData(Rf2VehicleScoring player, Rf2ScoringInfo info)
    {
        // Try to read driver inputs from the telemetry MMF.
        var telem = _reader.ReadPlayerTelemetry(_playerSlotId);

        float throttle = telem.HasValue ? (float)telem.Value.UnfilteredThrottle : 0f;
        float brake    = telem.HasValue ? (float)telem.Value.UnfilteredBrake    : 0f;
        float clutch   = telem.HasValue ? (float)telem.Value.UnfilteredClutch   : 0f;
        float steering = telem.HasValue ? (float)telem.Value.UnfilteredSteering : 0f;
        int   gear     = telem.HasValue ? telem.Value.Gear                      : 0;
        float rpm      = telem.HasValue ? (float)telem.Value.EngineRPM          : 0f;

        // Speed from scoring velocity vector (available regardless of telemetry).
        float speed = player.SpeedMps;

        // Fuel from scoring (absolute litres = FuelLevel × FuelCapacity).
        float fuelLiters = player.FuelLiters;
        _fuelTracker.Update(player.TotalLaps, fuelLiters, player.UnderYellow);

        _bus.Publish(new TelemetryData(
            Throttle:              throttle,
            Brake:                 brake,
            Clutch:                clutch,
            SteeringAngle:         steering,
            SpeedMps:              speed,
            Gear:                  gear,
            Rpm:                   rpm,
            FuelLevelLiters:       fuelLiters,
            FuelConsumptionPerLap: _fuelTracker.PerLapAverage,
            IncidentCount:         -1  // not available in LMU
        ));
    }

    private void PublishRelativeData(Rf2ScoringSnapshot snapshot, Rf2VehicleScoring player)
    {
        if (_trackLengthMeters <= 0) return;

        var relative = LmuRelativeCalculator.Compute(
            snapshot.Vehicles,
            _cachedDrivers,
            _playerSlotId,
            _trackLengthMeters,
            player.EstimatedLapTime > 0 ? player.EstimatedLapTime : 90.0);

        _bus.Publish(relative);
    }

    private void PublishPitData(Rf2VehicleScoring player, Rf2ScoringInfo info)
    {
        bool isOnPitRoad  = player.PitState is 2 or 3 or 4; // entering, stopped, exiting
        bool isInPitStall = player.PitState == 3;            // stopped
        bool pitLimiter   = player.PitState is 2 or 4;      // entering or exiting (best proxy)

        _bus.Publish(new PitData(
            IsOnPitRoad:        isOnPitRoad,
            IsInPitStall:       isInPitStall,
            PitLimiterSpeedMps: 0f,   // rF2 scoring does not expose the pit speed limit directly
            CurrentSpeedMps:    player.SpeedMps,
            PitLimiterActive:   pitLimiter,
            PitStopCount:       player.PitStopCount,
            RequestedService:   PitServiceFlags.None, // rF2 pit menu not exposed via scoring
            FuelToAddLiters:    0f
        ));
    }

    private void PublishWeatherData(Rf2ScoringInfo info)
    {
        // rF2 MaxPathWetness (0–1) maps to our 1–7 wetness scale.
        // 0.0 = dry (1), 1.0 = extremely wet (7).
        int wetness = info.MaxPathWetness <= 0.0
            ? 1
            : Math.Clamp((int)(info.MaxPathWetness * 6f) + 1, 1, 7);

        float airTemp   = (float)info.Temperature;
        float trackTemp = info.TrackTempC is float t and not float.NaN ? t : airTemp;

        _bus.Publish(new WeatherData(
            AirTempC:         airTemp,
            TrackTempC:       trackTemp,
            WindSpeedMps:     0f,   // rF2 scoring does not expose wind speed
            WindDirectionDeg: 0f,
            Humidity:         0f,   // not in base scoring struct (expansion varies)
            SkyCoverage:      0,
            TrackWetness:     (float)Math.Clamp(info.MaxPathWetness, 0.0, 1.0),
            IsPrecipitating:  info.Raining > 0.05
        ));
    }

    private void PublishTrackMapData(Rf2ScoringSnapshot snapshot, Rf2VehicleScoring player)
    {
        if (_trackLengthMeters <= 0) return;

        var driverBySlot = new Dictionary<int, LmuDriverSnapshot>(_cachedDrivers.Length);
        foreach (var d in _cachedDrivers)
            driverBySlot[d.SlotId] = d;

        var cars = new List<TrackMapCarEntry>(snapshot.Vehicles.Length);
        foreach (var v in snapshot.Vehicles)
        {
            if (!v.IsActive || v.InGarageStall != 0) continue;

            float pct = (float)(v.LapDist / _trackLengthMeters);
            if (pct < 0f || pct > 1.05f) continue; // sanity guard

            driverBySlot.TryGetValue(v.Id, out var driver);

            cars.Add(new TrackMapCarEntry(
                CarIndex:   v.Id,
                CarNumber:  driver?.CarNumber ?? v.Id.ToString(),
                Position:   v.Place > 0 ? v.Place : 0,
                LapDistPct: Math.Clamp(pct, 0f, 1f),
                CarClass:   driver?.VehicleClass ?? "",
                IsPlayer:   v.Id == _playerSlotId,
                IsInPit:    v.PitState != 0
            ));
        }

        _bus.Publish(new TrackMapData(
            TrackLengthMeters: (float)_trackLengthMeters,
            Cars:              cars));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Rf2VehicleScoring? FindPlayer(
        Rf2VehicleScoring[] vehicles, string playerName)
    {
        if (string.IsNullOrEmpty(playerName)) return null;

        foreach (ref readonly var v in vehicles.AsSpan())
        {
            if (v.DriverName == playerName)
                return v;
        }
        return null;
    }

    private static int FindPlayerPosition(Rf2VehicleScoring player)
    {
        int place = player.Place;
        return place > 0 ? place : 0;
    }

    private static float ComputeDelta(Rf2VehicleScoring player)
    {
        if (player.BestLapTime <= 0 || player.LastLapTime <= 0)
            return 0f;

        // Approximate: current lap time compared to personal best.
        // rF2 doesn't provide a real-time delta like iRacing does.
        float approx = (float)(player.LastLapTime - player.BestLapTime);
        return Math.Abs(approx) < 60f ? approx : 0f; // guard against stale values
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer?.Dispose();
        _timer = null;
        _reader.Dispose();

        AppLog.Info("LmuPoller disposed.");
    }
}
