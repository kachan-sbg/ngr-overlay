using System.Collections.Immutable;
using SimOverlay.Core;
using SimOverlay.Sim.Contracts;
using SimOverlay.Sim.LMU.SharedMemory;

namespace SimOverlay.Sim.LMU;

/// <summary>
/// Polls the <c>LMU_Data</c> shared memory at ~60 Hz and publishes data to the
/// <see cref="ISimDataBus"/>:
/// <list type="bullet">
///   <item><see cref="DriverData"/> — 60 Hz</item>
///   <item><see cref="TelemetryData"/> — 60 Hz</item>
///   <item><see cref="RelativeData"/> — 10 Hz</item>
///   <item><see cref="PitData"/> — 10 Hz</item>
///   <item><see cref="TrackMapData"/> — 10 Hz</item>
///   <item><see cref="WeatherData"/> — 1 Hz</item>
///   <item><see cref="SessionData"/> — on session change (track / vehicle count / session)</item>
/// </list>
/// </summary>
internal sealed class LmuPoller : IDisposable
{
    private const int RelativePublishInterval = 6;  // 60 Hz ÷ 6 = 10 Hz
    private const int WeatherPublishInterval  = 60; // 60 Hz ÷ 60 = 1 Hz
    // Periodically release/reacquire LMU_Data so this process does not keep a stale
    // mapping alive after LMU exits.
    private const int ReaderReopenIntervalTicks = 300; // ~4.8s at 16 ms

    private readonly ISimDataBus      _bus;
    private readonly Action<SimState> _onStateChanged;
    private readonly LmuMemoryReader  _reader;
    private readonly LmuFuelTracker   _fuelTracker = new();
    private int _pollInProgress;
    private int _readerReopenTicks;

    // Cached driver list, rebuilt when track or vehicle count changes.
    private ImmutableArray<LmuDriverSnapshot> _cachedDrivers =
        ImmutableArray<LmuDriverSnapshot>.Empty;

    // Used to detect session changes without full decode every tick.
    private string _lastTrackName     = "";
    private int    _lastNumVehicles   = -1;
    private int    _lastSession       = -1;
    private byte   _lastInRealtime    = 255; // 255 = sentinel so first read always fires

    // Track length cached from session data (metres).
    private double _trackLengthMeters;

    // Player slot ID (used for relative/standings/trackmap lookups).
    private int _playerSlotId = -1;

    private int  _frameCount;
    private bool _inSession;
    private bool _disposed;
    private float _lastFuelCapacityLiters;
    private float _lastFuelLevelLiters = -1f;
    private bool _loggedMissingFuelCapacity;

    private Timer? _timer;

    // ── Construction ─────────────────────────────────────────────────────────

    public LmuPoller(ISimDataBus bus, Action<SimState> onStateChanged)
    {
        _bus            = bus;
        _onStateChanged = onStateChanged;
        _reader         = new LmuMemoryReader();
    }

    public void Start()
    {
        // Always start the timer even if the MMF isn't open yet.
        // Poll() retries TryOpen() on every tick until it succeeds, so startup
        // order (overlay before or after LMU) does not matter.
        _timer = new Timer(Poll, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(16));
        AppLog.Info("LmuPoller started — will open LMU_Data on first successful poll.");
    }

    // ── Polling ───────────────────────────────────────────────────────────────

    private void Poll(object? state)
    {
        if (_disposed) return;
        if (System.Threading.Interlocked.Exchange(ref _pollInProgress, 1) == 1)
            return;

        try
        {
            // If the reader isn't open yet (LMU_Data not created yet, or a transient
            // failure on startup), try again every tick until it succeeds.
            if (!_reader.IsOpen && !_reader.TryOpen())
                return;

            // Rebind periodically so dead-producer cases are detected even if a stale
            // mapping would otherwise stay alive only because this process still holds
            // a handle.
            if (_reader.IsOpen && ++_readerReopenTicks >= ReaderReopenIntervalTicks)
            {
                _readerReopenTicks = 0;
                if (!_reader.Reopen())
                    return;
            }

            var snapshot = _reader.ReadScoring();
            if (snapshot == null) return;

            var info = snapshot.Info;

            // Detect session / track changes that require a full re-decode.
            bool sessionChanged = info.TrackName    != _lastTrackName
                               || info.NumVehicles  != _lastNumVehicles
                               || info.Session      != _lastSession;

            if (sessionChanged)
                HandleSessionChange(snapshot);

            // Always evaluate in-session state each tick — InRealtime can flip
            // 0→1 when the race starts without any TrackName/Session/NumVehicles
            // change, so state transitions must happen here, not just on session change.
            UpdateSessionState(info);

            if (!_inSession) return;

            // Find player vehicle by IsPlayer flag.
            var playerNullable = FindPlayer(snapshot.Vehicles, out _);
            if (playerNullable == null) return;
            var player = playerNullable.Value;
            _playerSlotId = player.Id;

            // Read player telemetry; LMU writes the correct telemInfo array index
            // into the telemetry header — use that instead of the scoring array index.
            var telem = _reader.ReadPlayerTelemetry();

            // Publish high-frequency data every tick.
            PublishDriverData(player);
            PublishTelemetryData(player, telem);

            // Throttle lower-frequency data.
            if (++_frameCount % RelativePublishInterval == 0)
            {
                PublishRelativeData(snapshot, player);
                PublishPitData(player, telem);
                PublishTrackMapData(snapshot);
            }

            if (_frameCount % WeatherPublishInterval == 0)
                PublishWeatherData(info);
        }
        catch (Exception ex)
        {
            AppLog.Exception("LmuPoller.Poll", ex);
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _pollInProgress, 0);
        }
    }

    // ── Session handling ──────────────────────────────────────────────────────

    /// <summary>
    /// Called when TrackName / NumVehicles / Session changes.
    /// Decodes the session data and rebuilds the driver list.
    /// </summary>
    private void HandleSessionChange(LmuScoringSnapshot snapshot)
    {
        var info = snapshot.Info;

        _lastTrackName     = info.TrackName;
        _lastNumVehicles   = info.NumVehicles;
        _lastSession       = info.Session;
        _trackLengthMeters = info.LapDist > 0 ? info.LapDist : _trackLengthMeters;

        var (session, drivers) = LmuSessionDecoder.Decode(info, snapshot.Vehicles);
        _cachedDrivers = drivers.ToImmutableArray();

        _bus.Publish(session);

        AppLog.Info(
            $"LmuPoller: session info updated — track={info.TrackName}, " +
            $"vehicles={info.NumVehicles}, session={info.Session}, " +
            $"inRealtime={info.InRealtime}, " +
            $"trackLen={_trackLengthMeters:F0}m");
    }

    /// <summary>
    /// Evaluates whether we are in an active session on every poll tick.
    /// Must run outside <see cref="HandleSessionChange"/> because
    /// <c>InRealtime</c> can flip 0→1 (race start) without any change to
    /// TrackName, NumVehicles, or Session.
    /// </summary>
    private void UpdateSessionState(LmuScoringInfo info)
    {
        if (info.InRealtime == _lastInRealtime) return;
        _lastInRealtime = info.InRealtime;

        bool nowInSession = info.InRealtime != 0 && info.NumVehicles > 0;

        if (nowInSession == _inSession) return;
        _inSession = nowInSession;

        if (_inSession)
        {
            _fuelTracker.Reset();
            _onStateChanged(SimState.InSession);
            AppLog.Info($"LmuPoller: InRealtime→1 — session active ({info.TrackName})");
        }
        else
        {
            _onStateChanged(SimState.Connected);
            AppLog.Info("LmuPoller: InRealtime→0 — session paused/ended.");
        }
    }

    // ── Data publication ──────────────────────────────────────────────────────

    private void PublishDriverData(LmuVehicleScoring player)
    {
        int position = player.Place > 0 ? player.Place : 0;

        _bus.Publish(new DriverData
        {
            Position              = position,
            Lap                   = player.TotalLaps + 1,
            BestLapTime           = player.BestLapTime > 0
                                        ? TimeSpan.FromSeconds(player.BestLapTime)
                                        : TimeSpan.Zero,
            LastLapTime           = player.LastLapTime > 0
                                        ? TimeSpan.FromSeconds(player.LastLapTime)
                                        : TimeSpan.Zero,
            LapDeltaVsBestLap     = ComputeDelta(player),
            LapDeltaVsSessionBest = 0f, // not available from LMU scoring
        });
    }

    private void PublishTelemetryData(LmuVehicleScoring player, LmuPlayerInputs? telem)
    {
        float throttle, brake, clutch, steering, rpm, fuelLiters;
        int   gear;

        if (telem != null)
        {
            throttle  = telem.Throttle;
            brake     = telem.Brake;
            clutch    = telem.Clutch;
            steering  = telem.Steering;
            gear      = telem.Gear;
            rpm       = telem.EngineRpm;
            fuelLiters = telem.FuelLiters;
            if (telem.FuelCapacityLiters > 0f)
                _lastFuelCapacityLiters = telem.FuelCapacityLiters;
            _lastFuelLevelLiters = fuelLiters;
            _loggedMissingFuelCapacity = false;
        }
        else
        {
            throttle  = 0f;
            brake     = 0f;
            clutch    = 0f;
            steering  = 0f;
            gear      = 0;
            rpm       = 0f;
            // Fallback: derive fuel from FuelFraction scoring field using last known capacity.
            if (_lastFuelCapacityLiters > 0f)
            {
                fuelLiters = player.FuelFraction / 255f * _lastFuelCapacityLiters;
                _lastFuelLevelLiters = fuelLiters;
                _loggedMissingFuelCapacity = false;
            }
            else if (_lastFuelLevelLiters >= 0f)
            {
                fuelLiters = _lastFuelLevelLiters;
            }
            else
            {
                fuelLiters = 0f;
                if (!_loggedMissingFuelCapacity)
                {
                    AppLog.Warn("LmuPoller: player telemetry unavailable and no known fuel capacity; fuel falls back to 0 until telemetry appears.");
                    _loggedMissingFuelCapacity = true;
                }
            }
        }

        // Speed from LocalVel magnitude (always available in scoring).
        float speed = player.SpeedMps;

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
            LastLapFuelLiters:     _fuelTracker.LastLapConsumption,
            IncidentCount:         -1  // not available in LMU
        ));
    }

    private void PublishRelativeData(LmuScoringSnapshot snapshot, LmuVehicleScoring player)
    {
        if (_trackLengthMeters <= 0) return;

        var (relative, standings) = LmuRelativeCalculator.Compute(
            snapshot.Vehicles,
            _cachedDrivers,
            _playerSlotId,
            _trackLengthMeters,
            player.EstimatedLapTime > 0 ? player.EstimatedLapTime : 90.0);

        _bus.Publish(relative);
        _bus.Publish(standings);
    }

    private void PublishPitData(LmuVehicleScoring player, LmuPlayerInputs? telem)
    {
        bool isOnPitRoad  = player.PitState is 2 or 3 or 4; // entering, stopped, exiting
        bool isInPitStall = player.PitState == 3;
        bool pitLimiter   = telem?.SpeedLimiterActive
                            ?? (player.PitState is 2 or 4); // fallback: entering or exiting

        _bus.Publish(new PitData(
            IsOnPitRoad:        isOnPitRoad,
            IsInPitStall:       isInPitStall,
            PitLimiterSpeedMps: 0f,
            CurrentSpeedMps:    player.SpeedMps,
            PitLimiterActive:   pitLimiter,
            PitStopCount:       player.NumPitstops,
            RequestedService:   PitServiceFlags.None,
            FuelToAddLiters:    0f
        ));
    }

    private void PublishWeatherData(LmuScoringInfo info)
    {
        int wetness = info.MaxPathWetness <= 0.0
            ? 1
            : Math.Clamp((int)(info.MaxPathWetness * 6f) + 1, 1, 7);

        _bus.Publish(new WeatherData(
            AirTempC:         (float)info.AmbientTempC,
            TrackTempC:       (float)info.TrackTempC,
            WindSpeedMps:     info.WindSpeedMps,
            WindDirectionDeg: info.WindDirectionDeg,
            Humidity:         0f,
            SkyCoverage:      null, // not available from LMU
            TrackWetness:     (float)Math.Clamp(info.MaxPathWetness, 0.0, 1.0),
            IsPrecipitating:  info.Raining > 0.05
        ));
    }

    private void PublishTrackMapData(LmuScoringSnapshot snapshot)
    {
        if (_trackLengthMeters <= 0) return;

        var driverBySlot = new Dictionary<int, LmuDriverSnapshot>(_cachedDrivers.Length);
        foreach (var d in _cachedDrivers)
            driverBySlot[d.SlotId] = d;

        var cars = new List<TrackMapCarEntry>(snapshot.Vehicles.Length);
        foreach (ref readonly var v in snapshot.Vehicles.AsSpan())
        {
            if (!v.IsActive || v.InGarageStall != 0) continue;

            float pct = (float)(v.LapDist / _trackLengthMeters);
            if (pct < 0f || pct > 1.05f) continue;

            driverBySlot.TryGetValue(v.Id, out var driver);

            cars.Add(new TrackMapCarEntry(
                CarIndex:   v.Id,
                CarNumber:  driver?.CarNumber ?? v.Id.ToString(),
                Position:   v.Place > 0 ? v.Place : 0,
                LapDistPct: Math.Clamp(pct, 0f, 1f),
                CarClass:   driver?.VehicleClass ?? "",
                IsPlayer:   v.IsPlayer != 0,
                IsInPit:    v.PitState != 0
            ));
        }

        _bus.Publish(new TrackMapData(
            TrackLengthMeters: (float)_trackLengthMeters,
            Cars:              cars));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the player vehicle by the <see cref="LmuVehicleScoring.IsPlayer"/> flag.
    /// Returns <c>null</c> if not found; sets <paramref name="arrayIndex"/> to the index
    /// within <paramref name="vehicles"/>.
    /// </summary>
    private static LmuVehicleScoring? FindPlayer(LmuVehicleScoring[] vehicles, out int arrayIndex)
    {
        for (int i = 0; i < vehicles.Length; i++)
        {
            if (vehicles[i].IsPlayer != 0)
            {
                arrayIndex = i;
                return vehicles[i];
            }
        }
        arrayIndex = -1;
        return null;
    }

    private static float ComputeDelta(LmuVehicleScoring player)
    {
        if (player.BestLapTime <= 0 || player.LastLapTime <= 0)
            return 0f;

        float approx = (float)(player.LastLapTime - player.BestLapTime);
        return Math.Abs(approx) < 60f ? approx : 0f;
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Stop the timer and wait for any in-progress callback to finish before
        // releasing the memory reader.  Timer.Dispose(WaitHandle) signals the handle
        // when the timer is fully stopped (no pending callbacks remain), preventing a
        // race where Poll() accesses _reader after it has been disposed.
        if (_timer != null)
        {
            using var stopped = new ManualResetEvent(false);
            _timer.Dispose(stopped);
            stopped.WaitOne(TimeSpan.FromSeconds(2));
            _timer = null;
        }

        _reader.Dispose();
        AppLog.Info("LmuPoller disposed.");
    }
}
