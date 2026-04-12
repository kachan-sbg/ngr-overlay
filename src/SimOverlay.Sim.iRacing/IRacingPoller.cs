using System.Collections.Immutable;
using IRSDKSharper;
using SimOverlay.Core;
using SimOverlay.Sim.Contracts;

namespace SimOverlay.Sim.iRacing;

/// <summary>
/// Wraps <see cref="IRacingSdk"/> and publishes data to the <see cref="ISimDataBus"/>:
/// <list type="bullet">
///   <item><see cref="DriverData"/> — 60 Hz</item>
///   <item><see cref="TelemetryData"/> — 60 Hz</item>
///   <item><see cref="RelativeData"/> — 10 Hz</item>
///   <item><see cref="PitData"/> — 10 Hz</item>
///   <item><see cref="TrackMapData"/> — 10 Hz</item>
///   <item><see cref="WeatherData"/> — 1 Hz</item>
///   <item><see cref="SessionData"/> — on session YAML change</item>
/// </list>
/// IRSDKSharper fires <c>OnTelemetryData</c> at ~60 Hz on its own background thread.
/// <c>OnSessionInfo</c> fires whenever the iRacing session YAML changes.
/// </summary>
internal sealed class IRacingPoller : IDisposable
{
    // Publish RelativeData / PitData / TrackMapData every N telemetry ticks → ~10 Hz at 60 Hz input.
    private const int RelativePublishInterval = 6;
    // Publish WeatherData every N telemetry ticks → ~1 Hz at 60 Hz input.
    private const int WeatherPublishInterval  = 60;

    // iRacing allocates exactly 64 car-index slots in every telemetry array.
    private const int MaxCars = 64;

    // iRacing EngineWarnings bitmask: pit speed limiter active.
    private const int EngineWarningPitLimiter = 0x40;

    private readonly IRacingSdk        _sdk;
    private readonly ISimDataBus      _bus;
    private readonly Action<SimState> _onStateChanged;

    // Cached driver list, updated each time OnSessionInfo fires.
    private ImmutableArray<DriverSnapshot> _cachedDrivers =
        ImmutableArray<DriverSnapshot>.Empty;

    // Cached track length (metres), parsed from session YAML.
    private float _trackLengthMeters;

    private readonly FuelConsumptionTracker _fuelTracker = new();
    private int  _telemetryFrameCount;
    private bool _disposed;

    // Signalled by OnStopped (v1.1.6+) so Dispose() can block until the async
    // Stop() task has fully completed and Win32 handles have been nullified.
    private readonly ManualResetEventSlim _stoppedGate = new(initialState: false);

    // ── Construction ─────────────────────────────────────────────────────────

    public IRacingPoller(ISimDataBus bus, Action<SimState> onStateChanged)
    {
        _bus            = bus;
        _onStateChanged = onStateChanged;

        _sdk = new IRacingSdk();
        _sdk.OnConnected     += HandleConnected;
        _sdk.OnDisconnected  += HandleDisconnected;
        _sdk.OnSessionInfo   += HandleSessionInfo;
        _sdk.OnTelemetryData += HandleTelemetryData;
        _sdk.OnException     += HandleException;
        _sdk.OnStopped       += () => _stoppedGate.Set();
    }

    /// <summary>Starts the IRSDKSharper background loop.</summary>
    public void Start() => _sdk.Start();

    /// <summary>Stops the IRSDKSharper background loop.</summary>
    public void Stop() => _sdk.Stop();

    // ── SDK event handlers ────────────────────────────────────────────────────

    private void HandleConnected()
    {
        AppLog.Info("iRacing SDK connected — session active.");
        _fuelTracker.Reset();
        _onStateChanged(SimState.InSession);
    }

    private void HandleDisconnected()
    {
        AppLog.Info("iRacing SDK disconnected — waiting for session.");
        _fuelTracker.Reset();
        _onStateChanged(SimState.Connected);
    }

    private void HandleSessionInfo()
    {
        try
        {
            var (drivers, session) = IRacingSessionDecoder.Decode(_sdk.Data);
            _cachedDrivers = drivers.ToImmutableArray();

            // Cache track length for TrackMapData (changes only on track change).
            _trackLengthMeters = ParseTrackLengthMeters(
                _sdk.Data.SessionInfo?.WeekendInfo?.TrackLength);

            _bus.Publish(session);
            AppLog.Info($"Session info updated: {session.TrackName} / {session.SessionType}");
        }
        catch (Exception ex)
        {
            AppLog.Exception("Error decoding iRacing session info", ex);
        }
    }

    private void HandleTelemetryData()
    {
        try
        {
            PublishDriverData();
            PublishTelemetryData();

            if (++_telemetryFrameCount % RelativePublishInterval == 0)
            {
                PublishRelativeData();
                PublishPitData();
                PublishTrackMapData();
            }

            if (_telemetryFrameCount % WeatherPublishInterval == 0 || _telemetryFrameCount == 1)
                PublishWeatherData();
        }
        catch (Exception ex)
        {
            AppLog.Exception("Error processing iRacing telemetry data", ex);
        }
    }

    private static void HandleException(Exception ex) =>
        AppLog.Exception("IRSDKSharper internal exception", ex);

    // ── Data publication ──────────────────────────────────────────────────────

    private void PublishDriverData()
    {
        var data = _sdk.Data;

        var bestLapSec       = data.GetFloat("LapBestLapTime");
        var lastLapSec       = data.GetFloat("LapLastLapTime");
        var delta            = data.GetFloat("LapDeltaToBestLap");
        var deltaSessionBest = data.GetFloat("LapDeltaToSessionBestLap");
        var position         = data.GetInt("PlayerCarPosition");
        var lap              = data.GetInt("Lap");

        // Live session timing — read from telemetry so the display counts down in
        // real time rather than showing a stale YAML snapshot value.
        var sessionTimeSec       = data.GetFloat("SessionTime");
        var sessionTimeRemainSec = data.GetFloat("SessionTimeRemain");

        // In-game time of day (real-time, not YAML snapshot).
        var sessionTimeOfDaySec = data.GetFloat("SessionTimeOfDay");
        TimeOnly? gameTimeOfDay = sessionTimeOfDaySec > 0f
            ? TimeOnly.FromTimeSpan(TimeSpan.FromSeconds(sessionTimeOfDaySec % 86400.0))
            : null;

        // Session best = minimum valid lap time across all cars this session.
        var sessionBestSec = 0f;
        for (int i = 0; i < MaxCars; i++)
        {
            var t = data.GetFloat("CarIdxBestLapTime", i);
            if (t > 0f && (sessionBestSec <= 0f || t < sessionBestSec))
                sessionBestSec = t;
        }

        _bus.Publish(new DriverData
        {
            Position              = position,
            Lap                   = lap,
            BestLapTime           = bestLapSec > 0 ? TimeSpan.FromSeconds(bestLapSec) : TimeSpan.Zero,
            LastLapTime           = lastLapSec > 0 ? TimeSpan.FromSeconds(lastLapSec) : TimeSpan.Zero,
            SessionBestLapTime    = sessionBestSec > 0 ? TimeSpan.FromSeconds(sessionBestSec) : TimeSpan.Zero,
            LapDeltaVsBestLap     = delta,
            LapDeltaVsSessionBest = deltaSessionBest,
            SessionTimeElapsed    = sessionTimeSec > 0 ? TimeSpan.FromSeconds(sessionTimeSec) : TimeSpan.Zero,
            // iRacing returns -1 for SessionTimeRemain when the session has no time limit (laps-based).
            SessionTimeRemaining  = sessionTimeRemainSec >= 0f
                                        ? TimeSpan.FromSeconds(sessionTimeRemainSec)
                                        : (TimeSpan?)null,
            GameTimeOfDay         = gameTimeOfDay,
        });
    }

    private void PublishTelemetryData()
    {
        var data         = _sdk.Data;
        var lap          = data.GetInt("Lap");
        var fuelLevel    = data.GetFloat("FuelLevel");
        var sessionFlags = data.GetInt("SessionFlags");

        _fuelTracker.Update(lap, fuelLevel, sessionFlags);

        _bus.Publish(new TelemetryData(
            Throttle:              data.GetFloat("Throttle"),
            Brake:                 data.GetFloat("Brake"),
            Clutch:                data.GetFloat("Clutch"),
            SteeringAngle:         data.GetFloat("SteeringWheelAngle"),
            SpeedMps:              data.GetFloat("Speed"),
            Gear:                  data.GetInt("Gear"),
            Rpm:                   data.GetFloat("RPM"),
            FuelLevelLiters:       fuelLevel,
            FuelConsumptionPerLap: _fuelTracker.PerLapAverage,
            LastLapFuelLiters:     _fuelTracker.LastLapConsumption,
            IncidentCount:         data.GetInt("PlayerCarMyIncidentCount")
        ));
    }

    private void PublishRelativeData()
    {
        var data         = _sdk.Data;
        var playerCarIdx = data.SessionInfo?.DriverInfo?.DriverCarIdx ?? 0;

        // Prefer the SDK's pre-computed estimated lap time for the player's car;
        // fall back to session lap times if it's not available yet.
        var estLapTimeSec = data.SessionInfo?.DriverInfo?.DriverCarEstLapTime ?? 0f;
        if (estLapTimeSec <= 0f) estLapTimeSec = data.GetFloat("LapBestLapTime");
        if (estLapTimeSec <= 0f) estLapTimeSec = data.GetFloat("LapLastLapTime");
        if (estLapTimeSec <= 0f) estLapTimeSec = 90f; // safe fallback before any lap is complete

        // Build per-car arrays from the indexed telemetry variables.
        var lapDistPcts   = new float[MaxCars];
        var positions     = new int[MaxCars];
        var laps          = new int[MaxCars];
        var bestLapTimes  = new float[MaxCars];
        var lastLapTimes  = new float[MaxCars];
        var trackSurfaces = new int[MaxCars];

        for (var i = 0; i < MaxCars; i++)
        {
            lapDistPcts[i]   = data.GetFloat("CarIdxLapDistPct",    i);
            positions[i]     = data.GetInt("CarIdxPosition",        i);
            laps[i]          = data.GetInt("CarIdxLap",             i);
            bestLapTimes[i]  = data.GetFloat("CarIdxBestLapTime",   i);
            lastLapTimes[i]  = data.GetFloat("CarIdxLastLapTime",   i);
            trackSurfaces[i] = data.GetInt("CarIdxTrackSurface",    i);
        }

        var snapshot = new TelemetrySnapshot(
            PlayerCarIdx:     playerCarIdx,
            LapDistPcts:      lapDistPcts,
            Positions:        positions,
            Laps:             laps,
            EstimatedLapTime: estLapTimeSec,
            BestLapTimes:     bestLapTimes,
            LastLapTimes:     lastLapTimes,
            TrackSurfaces:    trackSurfaces);

        var (relativeData, standingsData) = IRacingRelativeCalculator.Compute(snapshot, _cachedDrivers);
        _bus.Publish(relativeData);
        _bus.Publish(standingsData);
    }

    private void PublishPitData()
    {
        var data         = _sdk.Data;
        var playerCarIdx = data.SessionInfo?.DriverInfo?.DriverCarIdx ?? 0;

        var isOnPitRoad    = data.GetInt("OnPitRoad") != 0;
        var trackSurface   = data.GetInt("PlayerTrackSurface");
        var isInPitStall   = trackSurface == 1; // irsdk_InPitStall = 1
        var engineWarnings = data.GetInt("EngineWarnings");
        var pitLimiterOn   = (engineWarnings & EngineWarningPitLimiter) != 0;

        // Map iRacing PitSvFlags bitmask to our PitServiceFlags enum.
        var iracingPitFlags = data.GetInt("PitSvFlags");
        var serviceFlags    = PitServiceFlags.None;
        if ((iracingPitFlags & 0x10) != 0) serviceFlags |= PitServiceFlags.Fuel;
        if ((iracingPitFlags & 0x01) != 0) serviceFlags |= PitServiceFlags.LeftFrontTire;
        if ((iracingPitFlags & 0x02) != 0) serviceFlags |= PitServiceFlags.RightFrontTire;
        if ((iracingPitFlags & 0x04) != 0) serviceFlags |= PitServiceFlags.LeftRearTire;
        if ((iracingPitFlags & 0x08) != 0) serviceFlags |= PitServiceFlags.RightRearTire;
        if ((iracingPitFlags & 0x20) != 0) serviceFlags |= PitServiceFlags.WindshieldTearoff;
        if ((iracingPitFlags & 0x40) != 0) serviceFlags |= PitServiceFlags.FastRepair;

        _bus.Publish(new PitData(
            IsOnPitRoad:        isOnPitRoad,
            IsInPitStall:       isInPitStall,
            PitLimiterSpeedMps: data.GetFloat("PitSpeedLimit"),
            CurrentSpeedMps:    data.GetFloat("Speed"),
            PitLimiterActive:   pitLimiterOn,
            PitStopCount:       data.GetInt("CarIdxNumPitStops", playerCarIdx),
            RequestedService:   serviceFlags,
            FuelToAddLiters:    data.GetFloat("dpFuelFill")
        ));
    }

    private void PublishWeatherData()
    {
        var data = _sdk.Data;

        var windDirRad = data.GetFloat("WindDir");
        var windDirDeg = windDirRad * (180f / MathF.PI);
        if (windDirDeg < 0f) windDirDeg += 360f;

        // iRacing TrackWetness: 0=unknown, 1=dry, ..., 7=extremely wet.
        // Normalise 1–7 to 0.0–1.0; treat 0 (unknown) as 0.
        var trackWetnessRaw  = data.GetInt("TrackWetness");
        var trackWetnessNorm = trackWetnessRaw <= 0 ? 0f : (trackWetnessRaw - 1) / 6f;

        _bus.Publish(new WeatherData(
            AirTempC:         data.GetFloat("AirTemp"),
            TrackTempC:       data.GetFloat("TrackTempCrew"),
            WindSpeedMps:     data.GetFloat("WindVel"),
            WindDirectionDeg: windDirDeg,
            Humidity:         data.GetFloat("RelativeHumidity"),
            SkyCoverage:      data.GetInt("Skies"),
            TrackWetness:     trackWetnessNorm,
            IsPrecipitating:  data.GetInt("WeatherDeclaredWet") != 0
        ));
    }

    private void PublishTrackMapData()
    {
        var data         = _sdk.Data;
        var playerCarIdx = data.SessionInfo?.DriverInfo?.DriverCarIdx ?? 0;

        var drivers     = _cachedDrivers;
        var driverByIdx = new Dictionary<int, DriverSnapshot>(drivers.Length);
        foreach (var d in drivers)
            driverByIdx[d.CarIdx] = d;

        var cars = new List<TrackMapCarEntry>(MaxCars);
        for (var i = 0; i < MaxCars; i++)
        {
            // CarIdxTrackSurface == -1 means the slot is unused / car not spawned in world.
            // This filters out registered-but-not-connected drivers that iRacing still
            // populates in the car array (they have pct=0 which passes a pct<0 check).
            var trackSurface = data.GetInt("CarIdxTrackSurface", i);
            if (trackSurface < 0) continue;  // NotInWorld

            var pct = data.GetFloat("CarIdxLapDistPct", i);

            driverByIdx.TryGetValue(i, out var driver);
            if (driver is { IsSpectator: true } or { IsPaceCar: true }) continue;

            cars.Add(new TrackMapCarEntry(
                CarIndex:   i,
                CarNumber:  driver?.CarNumber ?? i.ToString(),
                Position:   data.GetInt("CarIdxPosition", i),
                LapDistPct: pct,
                CarClass:   driver?.CarClass ?? "",
                IsPlayer:   i == playerCarIdx,
                IsInPit:    data.GetInt("CarIdxOnPitRoad", i) != 0
            ));
        }

        _bus.Publish(new TrackMapData(
            TrackLengthMeters: _trackLengthMeters,
            Cars:              cars));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses iRacing's track length string, e.g. "5.78 km" → 5780f.
    /// Returns 0 if the string is absent or unparseable.
    /// </summary>
    private static float ParseTrackLengthMeters(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0f;

        // Format: "N.NN km"
        var spaceIdx = raw.IndexOf(' ');
        var numStr   = spaceIdx >= 0 ? raw[..spaceIdx] : raw;

        return float.TryParse(numStr,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var km) ? km * 1000f : 0f;
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _sdk.Stop();
        }
        catch (Exception ex)
        {
            AppLog.Exception("IRacingPoller.Dispose: sdk Stop failed", ex);
        }

        // IRSDKSharper.Stop() is async (Task.Run) and does not Dispose() its
        // Win32 handles — it only nullifies them, leaving finalizers to close them.
        // If iRacing is restarted before GC runs, CreateEvent("Local\\IRSDKDataValidEvent")
        // returns the still-open event in whatever state it was, causing iRacing's
        // SDK init to stall ("pending").  Block on the OnStopped event (v1.1.6+) so
        // we know the async task completed, then force finalizer collection to
        // deterministically release the handles.
        _stoppedGate.Wait(TimeSpan.FromSeconds(3));

        GC.Collect();
        GC.WaitForPendingFinalizers();

        AppLog.Info("IRacingPoller disposed — SDK handles released.");
    }
}
