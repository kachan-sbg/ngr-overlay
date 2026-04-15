using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using IRSDKSharper;
using NrgOverlay.Core;
using NrgOverlay.Core.Config;
using NrgOverlay.Sim.Contracts;

namespace NrgOverlay.Sim.iRacing;

/// <summary>
/// Wraps <see cref="IRacingSdk"/> and publishes data to the <see cref="ISimDataBus"/>:
/// <list type="bullet">
///   <item><see cref="DriverData"/> вЂ” 60 Hz</item>
///   <item><see cref="TelemetryData"/> вЂ” 60 Hz</item>
///   <item><see cref="RelativeData"/> вЂ” 10 Hz</item>
///   <item><see cref="PitData"/> вЂ” 10 Hz</item>
///   <item><see cref="TrackMapData"/> вЂ” 10 Hz</item>
///   <item><see cref="WeatherData"/> вЂ” 1 Hz</item>
///   <item><see cref="SessionData"/> вЂ” on session YAML change</item>
/// </list>
/// IRSDKSharper fires <c>OnTelemetryData</c> at ~60 Hz on its own background thread.
/// <c>OnSessionInfo</c> fires whenever the iRacing session YAML changes.
///
/// <para>
/// <b>Connection resilience</b>: IRSDKSharper's <c>OnConnected</c> event sometimes does not
/// fire when iRacing is already running at SDK init time. A watchdog timer fires every
/// <see cref="WatchdogIntervalSec"/> seconds; if no telemetry frames have arrived but the
/// iRacing shared-memory header reports a live connection, the SDK is restarted from scratch.
/// </para>
/// </summary>
internal sealed class IRacingPoller : IDisposable
{
    // Publish RelativeData / PitData / TrackMapData every N telemetry ticks в†’ ~10 Hz at 60 Hz input.
    private const int RelativePublishInterval = 6;
    // Publish WeatherData every N telemetry ticks в†’ ~1 Hz at 60 Hz input.
    private const int WeatherPublishInterval  = 60;

    // iRacing allocates exactly 64 car-index slots in every telemetry array.
    private const int MaxCars = 64;

    // iRacing EngineWarnings bitmask: pit speed limiter active.
    private const int EngineWarningPitLimiter = 0x40;

    // Watchdog: how often to check telemetry flow, and how many consecutive stall
    // ticks (each WatchdogIntervalSec long) before triggering an SDK restart.
    private const int WatchdogIntervalSec = 5;
    private const int WatchdogStallTicks  = 2;   // 2 Г— 5 s = 10 s of stall в†’ restart

    // iRacing SDK shared-memory constants (duplicated here so the watchdog can check
    // the connection status without depending on IRacingProvider).
    private const string MmfName          = "Local\\IRSDKMemMapFileName";
    private const int    MmfStatusOffset  = 4;
    private const int    MmfConnectedBit  = 0x01;
    private const string EnvDisableWatchdogRestart = "NRGOVERLAY_DEBUG_DISABLE_IRACING_WATCHDOG_RESTART";
    private const string EnvDisableForcedGcRelease = "NRGOVERLAY_DEBUG_DISABLE_IRACING_FORCED_GC";
    private const string EnvTraceLifecycle          = "NRGOVERLAY_DEBUG_TRACE_IRACING_LIFECYCLE";
    private const string EnvTraceCountryResolution  = "NRGOVERLAY_DEBUG_TRACE_COUNTRY_RESOLUTION";

    private readonly ISimDataBus      _bus;
    private readonly AppConfig        _appConfig;
    private readonly ConfigStore      _configStore;
    private readonly Action<SimState> _onStateChanged;

    // IRacingSdk is recreated by the watchdog when stalled; not readonly.
    private IRacingSdk _sdk;

    // Cached driver list, updated each time OnSessionInfo fires.
    private ImmutableArray<DriverSnapshot> _cachedDrivers =
        ImmutableArray<DriverSnapshot>.Empty;

    // Cached track length (metres), parsed from session YAML.
    private float _trackLengthMeters;

    private readonly FuelConsumptionTracker    _fuelTracker    = new();
    private readonly CarStateTracker           _carStateTracker = new();
    private readonly IRacingRelativeCalculator _calculator      = new();

    // Telemetry frame counter вЂ” incremented on every HandleTelemetryData call.
    // The watchdog reads this on a different thread; volatile ensures visibility.
    private volatile int _telemetryFrameCount;

    // Watchdog state.
    private readonly Timer _watchdogTimer;
    private int  _watchdogPrevFrame  = -1;  // -1 = baseline not yet established
    private int  _watchdogStallCount;
    private volatile bool _restarting;       // true while SDK restart is in progress

    // Set to true the first time OnConnected fires. The watchdog never restarts the SDK
    // until this is true вЂ” iRacing may still be loading (MMF connected bit set, but no
    // telemetry yet). Restarting during loading is what causes the iRacing/VS Code crash.
    private bool _hasConnected;

    private bool _disposed;

    // Signalled by OnStopped (v1.1.6+) so Stop/restart can block until the async
    // Stop() task has fully completed and Win32 handles have been nullified.
    private readonly ManualResetEventSlim _stoppedGate = new(initialState: false);

    // Keep a stable reference to the lambda so we can detach it before SDK recreation.
    private readonly Action _stoppedHandler;
    private static readonly bool DebugDisableWatchdogRestart = GetEnvFlag(EnvDisableWatchdogRestart);
    private static readonly bool DebugDisableForcedGcRelease = GetEnvFlag(EnvDisableForcedGcRelease);
    private static readonly bool DebugTraceLifecycle         = GetEnvFlag(EnvTraceLifecycle);
    private static readonly bool DebugTraceCountryResolution = GetEnvFlag(EnvTraceCountryResolution);

    // в”Ђв”Ђ Construction в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public IRacingPoller(
        ISimDataBus bus,
        AppConfig appConfig,
        ConfigStore configStore,
        Action<SimState> onStateChanged)
    {
        _bus            = bus;
        _appConfig      = appConfig;
        _configStore    = configStore;
        _onStateChanged = onStateChanged;
        _appConfig.GlobalSettings ??= new GlobalSettings();
        _appConfig.GlobalSettings.DriverCountryOverrides ??= [];
        _appConfig.GlobalSettings.DriverCountryCache ??= [];
        _appConfig.GlobalSettings.DriverCountryByFlairId ??= [];
        _appConfig.GlobalSettings.DriverCountryIso2ByFlairId ??= [];

        SeedFlairCountryMappingsFromDefaults();

        _stoppedHandler = () => _stoppedGate.Set();

        _sdk = new IRacingSdk();
        AttachSdkEvents();

        _watchdogTimer = new Timer(Watchdog, null,
            TimeSpan.FromSeconds(WatchdogIntervalSec),
            TimeSpan.FromSeconds(WatchdogIntervalSec));

        AppLog.Info(
            $"IRacingPoller debug toggles: restartDisabled={DebugDisableWatchdogRestart}, " +
            $"forcedGcDisabled={DebugDisableForcedGcRelease}, lifecycleTrace={DebugTraceLifecycle}.");
    }

    /// <summary>Starts the IRSDKSharper background loop.</summary>
    public void Start() => _sdk.Start();

    /// <summary>Stops the IRSDKSharper background loop.</summary>
    public void Stop() => _sdk.Stop();

    // в”Ђв”Ђ SDK event attachment в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void AttachSdkEvents()
    {
        _sdk.OnConnected     += HandleConnected;
        _sdk.OnDisconnected  += HandleDisconnected;
        _sdk.OnSessionInfo   += HandleSessionInfo;
        _sdk.OnTelemetryData += HandleTelemetryData;
        _sdk.OnException     += HandleException;
        _sdk.OnStopped       += _stoppedHandler;
    }

    private void DetachSdkEvents()
    {
        _sdk.OnConnected     -= HandleConnected;
        _sdk.OnDisconnected  -= HandleDisconnected;
        _sdk.OnSessionInfo   -= HandleSessionInfo;
        _sdk.OnTelemetryData -= HandleTelemetryData;
        _sdk.OnException     -= HandleException;
        _sdk.OnStopped       -= _stoppedHandler;
    }

    // в”Ђв”Ђ SDK event handlers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void HandleConnected()
    {
        AppLog.Info("iRacing SDK connected вЂ” session active.");
        _hasConnected = true;
        _fuelTracker.Reset();
        _carStateTracker.Reset();
        _calculator.Reset();
        _onStateChanged(SimState.InSession);
    }

    private void HandleDisconnected()
    {
        AppLog.Info("iRacing SDK disconnected вЂ” waiting for session.");
        _fuelTracker.Reset();
        _carStateTracker.Reset();
        _calculator.Reset();
        _onStateChanged(SimState.Connected);
    }

    private void HandleSessionInfo()
    {
        try
        {
            var (drivers, session) = IRacingSessionDecoder.Decode(_sdk.Data);
            _cachedDrivers = ResolveDriverCountries(drivers);
            TraceCountryResolution(_cachedDrivers);

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

    private void SeedFlairCountryMappingsFromDefaults()
    {
        try
        {
            var iso3ByFlair = _appConfig.GlobalSettings.DriverCountryByFlairId;
            var iso2ByFlair = _appConfig.GlobalSettings.DriverCountryIso2ByFlairId;
            bool changed = false;

            foreach (var kv in DriverCountryDefaults.Iso3ByFlairId)
            {
                var iso3 = CountryCodeResolver.NormalizeIso3Code(kv.Value);
                if (iso3.Length == 3
                    && (!iso3ByFlair.TryGetValue(kv.Key, out var currentIso3)
                        || !string.Equals(currentIso3, iso3, StringComparison.Ordinal)))
                {
                    iso3ByFlair[kv.Key] = iso3;
                    changed = true;
                }
            }

            foreach (var kv in DriverCountryDefaults.Iso2ByFlairId)
            {
                var iso2 = CountryCodeResolver.NormalizeIso2Code(kv.Value);
                if (iso2.Length == 2
                    && (!iso2ByFlair.TryGetValue(kv.Key, out var currentIso2)
                        || !string.Equals(currentIso2, iso2, StringComparison.Ordinal)))
                {
                    iso2ByFlair[kv.Key] = iso2;
                    changed = true;
                }
            }

            if (changed)
                _configStore.Save(_appConfig);

            AppLog.Info(
                $"Loaded flair-country mapping: iso2={iso2ByFlair.Count}, " +
                $"iso3={iso3ByFlair.Count}, source='built-in defaults'.");
        }
        catch (Exception ex)
        {
            AppLog.Exception("Failed to seed built-in flair-country mapping", ex);
        }
    }

    private ImmutableArray<DriverSnapshot> ResolveDriverCountries(
        IReadOnlyList<DriverSnapshot> drivers)
    {
        _appConfig.GlobalSettings ??= new GlobalSettings();
        _appConfig.GlobalSettings.DriverCountryOverrides ??= [];
        _appConfig.GlobalSettings.DriverCountryCache ??= [];
        _appConfig.GlobalSettings.DriverCountryByFlairId ??= [];
        _appConfig.GlobalSettings.DriverCountryIso2ByFlairId ??= [];

        var overrides = _appConfig.GlobalSettings.DriverCountryOverrides;
        var cache = _appConfig.GlobalSettings.DriverCountryCache;
        var byFlairIso3 = _appConfig.GlobalSettings.DriverCountryByFlairId;
        var byFlairIso2 = _appConfig.GlobalSettings.DriverCountryIso2ByFlairId;

        var resolved = new DriverSnapshot[drivers.Count];

        for (int i = 0; i < drivers.Count; i++)
        {
            var driver = drivers[i];
            var countryCode = CountryCodeResolver.ResolveCountryCode(
                driver.UserId,
                driver.FlairId,
                overrides,
                cache,
                byFlairIso2,
                byFlairIso3);

            resolved[i] = driver with { CountryCode = countryCode };
        }

        return resolved.ToImmutableArray();
    }

    private static void TraceCountryResolution(ImmutableArray<DriverSnapshot> drivers)
    {
        if (!DebugTraceCountryResolution) return;
        if (drivers.Length == 0)
        {
            AppLog.Info("CountryTrace: no drivers decoded from session info.");
            return;
        }

        var take = Math.Min(drivers.Length, 12);
        for (int i = 0; i < take; i++)
        {
            var d = drivers[i];
            AppLog.Info(
                $"CountryTrace[{i}]: carIdx={d.CarIdx} userId={d.UserId} " +
                $"user='{d.UserName}' flairId={d.FlairId} " +
                $"club='{d.ClubName}' clubId={d.ClubId} country='{d.CountryCode}'");
        }
    }

    private void HandleTelemetryData()
    {
        if (_restarting) return;

        _telemetryFrameCount++;

        // Each publisher is isolated: one failure cannot block the others.
        try { PublishDriverData(); }
        catch (Exception ex) { AppLog.Exception("IRacingPoller: error in PublishDriverData", ex); }

        try { PublishTelemetryData(); }
        catch (Exception ex) { AppLog.Exception("IRacingPoller: error in PublishTelemetryData", ex); }

        if (_telemetryFrameCount % RelativePublishInterval == 0)
        {
            try { PublishRelativeData(); }
            catch (Exception ex) { AppLog.Exception("IRacingPoller: error in PublishRelativeData", ex); }

            try { PublishPitData(); }
            catch (Exception ex) { AppLog.Exception("IRacingPoller: error in PublishPitData", ex); }

            try { PublishTrackMapData(); }
            catch (Exception ex) { AppLog.Exception("IRacingPoller: error in PublishTrackMapData", ex); }
        }

        if (_telemetryFrameCount % WeatherPublishInterval == 0 || _telemetryFrameCount == 1)
        {
            try { PublishWeatherData(); }
            catch (Exception ex) { AppLog.Exception("IRacingPoller: error in PublishWeatherData", ex); }
        }
    }

    private static void HandleException(Exception ex) =>
        AppLog.Exception("IRSDKSharper internal exception", ex);

    // в”Ђв”Ђ Watchdog в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>
    /// Fires every <see cref="WatchdogIntervalSec"/> seconds on a ThreadPool thread.
    /// Detects when telemetry has stalled while iRacing is connected and restarts the SDK.
    /// This recovers from IRSDKSharper's <c>OnConnected</c> event not firing when the
    /// sim was already running at SDK init time.
    /// </summary>
    private void Watchdog(object? _)
    {
        if (_disposed || _restarting) return;

        var current = _telemetryFrameCount;

        // First tick: just capture baseline, don't decide anything yet.
        if (_watchdogPrevFrame < 0)
        {
            _watchdogPrevFrame  = current;
            _watchdogStallCount = 0;
            return;
        }

        if (current != _watchdogPrevFrame)
        {
            // Telemetry is flowing вЂ” reset stall counter.
            _watchdogPrevFrame  = current;
            _watchdogStallCount = 0;
            return;
        }

        // Frames have not advanced since last check.
        // If OnConnected has never fired, iRacing is still loading вЂ” the connected bit in
        // the MMF gets set before telemetry starts. Never restart during loading; just
        // keep the baseline fresh so the stall count doesn't accumulate.
        if (!_hasConnected)
        {
            _watchdogPrevFrame = current;
            return;
        }

        if (!IsMmfConnected())
        {
            // iRacing itself has gone away вЂ” SimDetector handles this via IsRunning().
            _watchdogStallCount = 0;
            return;
        }

        _watchdogStallCount++;
        AppLog.Info(
            $"IRacingPoller: telemetry stalled for " +
            $"{_watchdogStallCount * WatchdogIntervalSec}s while iRacing MMF is connected " +
            $"(stall {_watchdogStallCount}/{WatchdogStallTicks}).");

        if (_watchdogStallCount >= WatchdogStallTicks)
        {
            if (DebugDisableWatchdogRestart)
            {
                AppLog.Warn(
                    "IRacingPoller watchdog restart suppressed by debug toggle " +
                    $"({EnvDisableWatchdogRestart}=1).");
                _watchdogStallCount = 0;
                _watchdogPrevFrame = current;
                return;
            }
            RestartSdk();
        }
    }

    /// <summary>
    /// Tears down the current <see cref="IRacingSdk"/> instance and starts a fresh one.
    /// Called from the watchdog when telemetry stalls while iRacing is connected.
    /// </summary>
    private void RestartSdk()
    {
        if (_disposed) return;

        AppLog.Info("IRacingPoller: restarting SDK to recover stalled connection.");
        var restartSw = Stopwatch.StartNew();
        _restarting = true;
        try
        {
            // Detach everything except OnStopped so _stoppedGate still fires after Stop().
            _sdk.OnConnected     -= HandleConnected;
            _sdk.OnDisconnected  -= HandleDisconnected;
            _sdk.OnSessionInfo   -= HandleSessionInfo;
            _sdk.OnTelemetryData -= HandleTelemetryData;
            _sdk.OnException     -= HandleException;

            _stoppedGate.Reset();
            try
            {
                var stopSw = Stopwatch.StartNew();
                _sdk.Stop();
                // Block until the async Stop() task has completed and released Win32 handles.
                var stopCompleted = _stoppedGate.Wait(TimeSpan.FromSeconds(3));
                stopSw.Stop();
                TraceLifecycle($"RestartSdk stop wait: completed={stopCompleted}, elapsedMs={stopSw.ElapsedMilliseconds}");
            }
            catch (Exception ex)
            {
                AppLog.Exception("IRacingPoller: error stopping SDK during restart", ex);
            }
            _sdk.OnStopped -= _stoppedHandler;

            // IRacingSdk has no IDisposable вЂ” its Win32 handles (MMF, named events)
            // are only released via GC finalizers on the SafeHandle wrappers inside it.
            // Null the field first so the instance becomes GC-eligible, then force a
            // full blocking collect so handles are closed before we open new ones.
            _sdk = null!;
            ReleaseSdkHandles("RestartSdk");

            _sdk = new IRacingSdk();
            AttachSdkEvents();

            // Reset frame counters and connection state so the watchdog re-establishes
            // its baseline and doesn't restart again before the new SDK connects.
            _telemetryFrameCount = 0;
            _watchdogPrevFrame   = -1;
            _watchdogStallCount  = 0;
            _hasConnected        = false;

            _sdk.Start();
            AppLog.Info("IRacingPoller: SDK restarted successfully.");
            TraceLifecycle($"RestartSdk total elapsedMs={restartSw.ElapsedMilliseconds}");
        }
        catch (Exception ex)
        {
            AppLog.Exception("IRacingPoller: fatal error during SDK restart", ex);
        }
        finally
        {
            _restarting = false;
        }
    }

    /// <summary>
    /// Reads the iRacing SDK shared-memory status bit directly.
    /// Returns <c>true</c> when the sim is live, <c>false</c> if the file is absent
    /// or the connected bit is not set.
    /// </summary>
    private static bool IsMmfConnected()
    {
        try
        {
            using var mmf  = MemoryMappedFile.OpenExisting(MmfName, MemoryMappedFileRights.Read);
            using var view = mmf.CreateViewAccessor(0, 8, MemoryMappedFileAccess.Read);
            return (view.ReadInt32(MmfStatusOffset) & MmfConnectedBit) != 0;
        }
        catch
        {
            return false;
        }
    }

    // в”Ђв”Ђ Data publication в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void PublishDriverData()
    {
        var data = _sdk.Data;

        var bestLapSec       = SafeGetFloat(data, "LapBestLapTime");
        var lastLapSec       = SafeGetFloat(data, "LapLastLapTime");
        var delta            = SafeGetFloat(data, "LapDeltaToBestLap");
        var deltaSessionBest = SafeGetFloat(data, "LapDeltaToSessionBestLap");
        var position         = SafeGetInt(data, "PlayerCarPosition");
        var lap              = SafeGetInt(data, "Lap");

        // Live session timing вЂ” read from telemetry so the display counts down in
        // real time rather than showing a stale YAML snapshot value.
        var sessionTimeSec       = SafeGetFloat(data, "SessionTime");
        var sessionTimeRemainSec = SafeGetFloat(data, "SessionTimeRemain");

        // In-game time of day (real-time, not YAML snapshot).
        var sessionTimeOfDaySec = SafeGetFloat(data, "SessionTimeOfDay");
        TimeOnly? gameTimeOfDay = sessionTimeOfDaySec > 0f
            ? TimeOnly.FromTimeSpan(TimeSpan.FromSeconds(sessionTimeOfDaySec % 86400.0))
            : null;

        // Session best = minimum valid lap time across all cars this session.
        var sessionBestSec = 0f;
        for (int i = 0; i < MaxCars; i++)
        {
            var t = SafeGetFloat(data, "CarIdxBestLapTime", i);
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
            SessionTimeElapsed    = sessionTimeSec > 0f ? ClampedTimeSpan(sessionTimeSec) : TimeSpan.Zero,
            // iRacing returns -1 for SessionTimeRemain when the session has no time limit (laps-based).
            // It also returns a huge sentinel float (~3.4e38) in some session types вЂ” treat those as null too.
            SessionTimeRemaining  = sessionTimeRemainSec >= 0f && sessionTimeRemainSec < 1e10f
                                        ? ClampedTimeSpan(sessionTimeRemainSec)
                                        : (TimeSpan?)null,
            GameTimeOfDay         = gameTimeOfDay,
        });
    }

    private void PublishTelemetryData()
    {
        var data         = _sdk.Data;
        var lap          = SafeGetInt(data, "Lap");
        var fuelLevel    = SafeGetFloat(data, "FuelLevel");
        var sessionFlags = SafeGetInt(data, "SessionFlags");

        _fuelTracker.Update(lap, fuelLevel, sessionFlags);

        _bus.Publish(new TelemetryData(
            Throttle:              SafeGetFloat(data, "Throttle"),
            Brake:                 SafeGetFloat(data, "Brake"),
            Clutch:                SafeGetFloat(data, "Clutch"),
            SteeringAngle:         SafeGetFloat(data, "SteeringWheelAngle"),
            SpeedMps:              SafeGetFloat(data, "Speed"),
            Gear:                  SafeGetInt(data, "Gear"),
            Rpm:                   SafeGetFloat(data, "RPM"),
            FuelLevelLiters:       fuelLevel,
            FuelConsumptionPerLap: _fuelTracker.PerLapAverage,
            LastLapFuelLiters:     _fuelTracker.LastLapConsumption,
            IncidentCount:         SafeGetInt(data, "PlayerCarMyIncidentCount")
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
        var onPitRoad     = new bool[MaxCars];
        var f2Times       = new float[MaxCars];
        var pitStopCounts = new int[MaxCars];
        var pitLaneTimes  = new float[MaxCars];
        var tireCompounds = new int[MaxCars];

        for (var i = 0; i < MaxCars; i++)
        {
            lapDistPcts[i]   = SafeGetFloat(data, "CarIdxLapDistPct",              i);
            positions[i]     = SafeGetInt  (data, "CarIdxPosition",                i);
            laps[i]          = SafeGetInt  (data, "CarIdxLap",                     i);
            bestLapTimes[i]  = SafeGetFloat(data, "CarIdxBestLapTime",             i);
            lastLapTimes[i]  = SafeGetFloat(data, "CarIdxLastLapTime",             i);
            trackSurfaces[i] = SafeGetInt  (data, "CarIdxTrackSurface",            i);
            onPitRoad[i]     = SafeGetInt  (data, "CarIdxOnPitRoad",               i) != 0;
            f2Times[i]       = SafeGetFloat(data, "CarIdxF2Time",                  i);
            pitStopCounts[i] = SafeGetInt  (data, "CarIdxNumPitStops",             i);
            pitLaneTimes[i]  = SafeGetFloat(data, "CarIdxLastPitLaneTimeAppro",    i);
            tireCompounds[i] = SafeGetInt  (data, "CarIdxTireCompound",            i);
        }

        var snapshot = new TelemetrySnapshot(
            PlayerCarIdx:     playerCarIdx,
            LapDistPcts:      lapDistPcts,
            Positions:        positions,
            Laps:             laps,
            EstimatedLapTime: estLapTimeSec,
            BestLapTimes:     bestLapTimes,
            LastLapTimes:     lastLapTimes,
            TrackSurfaces:    trackSurfaces,
            OnPitRoad:        onPitRoad,
            F2Times:          f2Times,
            PitStopCounts:    pitStopCounts,
            PitLaneTimes:     pitLaneTimes,
            TireCompounds:    tireCompounds);

        _carStateTracker.Update(snapshot);

        var (relativeData, standingsData) = _calculator.Compute(snapshot, _cachedDrivers, _carStateTracker);
        _bus.Publish(relativeData);
        _bus.Publish(standingsData);
    }

    private void PublishPitData()
    {
        var data         = _sdk.Data;
        var playerCarIdx = data.SessionInfo?.DriverInfo?.DriverCarIdx ?? 0;

        var isOnPitRoad    = SafeGetInt(data, "OnPitRoad") != 0;
        var trackSurface   = SafeGetInt(data, "PlayerTrackSurface");
        var isInPitStall   = trackSurface == 1; // irsdk_InPitStall = 1
        var engineWarnings = SafeGetInt(data, "EngineWarnings");
        var pitLimiterOn   = (engineWarnings & EngineWarningPitLimiter) != 0;

        // Map iRacing PitSvFlags bitmask to our PitServiceFlags enum.
        var iracingPitFlags = SafeGetInt(data, "PitSvFlags");
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
            PitLimiterSpeedMps: SafeGetFloat(data, "PitSpeedLimit"),
            CurrentSpeedMps:    SafeGetFloat(data, "Speed"),
            PitLimiterActive:   pitLimiterOn,
            PitStopCount:       SafeGetInt(data, "CarIdxNumPitStops", playerCarIdx),
            RequestedService:   serviceFlags,
            FuelToAddLiters:    SafeGetFloat(data, "dpFuelFill")
        ));
    }

    private void PublishWeatherData()
    {
        var data = _sdk.Data;

        var windDirRad = SafeGetFloat(data, "WindDir");
        var windDirDeg = windDirRad * (180f / MathF.PI);
        if (windDirDeg < 0f) windDirDeg += 360f;

        // iRacing TrackWetness: 0=unknown, 1=dry, ..., 7=extremely wet.
        // Normalise 1вЂ“7 to 0.0вЂ“1.0; treat 0 (unknown) as 0.
        var trackWetnessRaw  = SafeGetInt(data, "TrackWetness");
        var trackWetnessNorm = trackWetnessRaw <= 0 ? 0f : (trackWetnessRaw - 1) / 6f;

        _bus.Publish(new WeatherData(
            AirTempC:         SafeGetFloat(data, "AirTemp"),
            TrackTempC:       SafeGetFloat(data, "TrackTempCrew"),
            WindSpeedMps:     SafeGetFloat(data, "WindVel"),
            WindDirectionDeg: windDirDeg,
            Humidity:         SafeGetFloat(data, "RelativeHumidity"),
            SkyCoverage:      SafeGetInt(data, "Skies"),
            TrackWetness:     trackWetnessNorm,
            IsPrecipitating:  SafeGetInt(data, "WeatherDeclaredWet") != 0
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
            var trackSurface = SafeGetInt(data, "CarIdxTrackSurface", i);
            if (trackSurface < 0) continue;  // NotInWorld

            var pct = SafeGetFloat(data, "CarIdxLapDistPct", i);

            driverByIdx.TryGetValue(i, out var driver);
            if (driver is { IsSpectator: true } or { IsPaceCar: true }) continue;

            cars.Add(new TrackMapCarEntry(
                CarIndex:   i,
                CarNumber:  driver?.CarNumber ?? i.ToString(),
                Position:   SafeGetInt(data, "CarIdxPosition", i),
                LapDistPct: pct,
                CarClass:   driver?.CarClass ?? "",
                IsPlayer:   i == playerCarIdx,
                IsInPit:    SafeGetInt(data, "CarIdxOnPitRoad", i) != 0
            ));
        }

        _bus.Publish(new TrackMapData(
            TrackLengthMeters: _trackLengthMeters,
            Cars:              cars));
    }

    // в”Ђв”Ђ Helpers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>
    /// Reads a float telemetry variable by name (scalar, no index).
    /// Returns 0 if the variable is not present in this iRacing build/session
    /// (avoids KeyNotFoundException crashing the telemetry handler).
    /// iRacing does not expose every variable in every session type вЂ” e.g.
    /// <c>PitSpeedLimit</c> is absent in off-line test sessions.
    /// </summary>
    private static float SafeGetFloat(IRacingSdkData data, string name)
    {
        if (!data.TelemetryDataProperties.ContainsKey(name)) return 0f;
        return data.GetFloat(name);
    }

    /// <summary>Indexed variant of <see cref="SafeGetFloat(IRacingSdkData,string)"/>.</summary>
    private static float SafeGetFloat(IRacingSdkData data, string name, int index)
    {
        if (!data.TelemetryDataProperties.ContainsKey(name)) return 0f;
        return data.GetFloat(name, index);
    }

    /// <summary>
    /// Reads an int telemetry variable by name (scalar, no index).
    /// Returns 0 if the variable is not present in this iRacing build/session.
    /// </summary>
    private static int SafeGetInt(IRacingSdkData data, string name)
    {
        if (!data.TelemetryDataProperties.ContainsKey(name)) return 0;
        return data.GetInt(name);
    }

    /// <summary>Indexed variant of <see cref="SafeGetInt(IRacingSdkData,string)"/>.</summary>
    private static int SafeGetInt(IRacingSdkData data, string name, int index)
    {
        if (!data.TelemetryDataProperties.ContainsKey(name)) return 0;
        return data.GetInt(name, index);
    }

    /// <summary>
    /// Converts seconds to TimeSpan, clamped to [0, 30 days] to prevent overflow.
    /// iRacing returns a very large sentinel float (~3.4e38) for unlimited-time
    /// sessions instead of -1, which overflows TimeSpan.FromSeconds().
    /// </summary>
    private static TimeSpan ClampedTimeSpan(float seconds)
    {
        const float MaxSec = 86400f * 30; // 30 days вЂ” no racing session is longer
        return TimeSpan.FromSeconds(Math.Clamp(seconds, 0f, MaxSec));
    }

    /// <summary>
    /// Parses iRacing's track length string, e.g. "5.78 km" в†’ 5780f.
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

    // в”Ђв”Ђ Disposal в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _watchdogTimer.Dispose();
        // Detach everything except OnStopped so _stoppedGate fires after Stop() completes.
        _sdk.OnConnected     -= HandleConnected;
        _sdk.OnDisconnected  -= HandleDisconnected;
        _sdk.OnSessionInfo   -= HandleSessionInfo;
        _sdk.OnTelemetryData -= HandleTelemetryData;
        _sdk.OnException     -= HandleException;

        _stoppedGate.Reset();
        var disposeStopSw = Stopwatch.StartNew();

        try
        {
            _sdk.Stop();
        }
        catch (Exception ex)
        {
            AppLog.Exception("IRacingPoller.Dispose: sdk Stop failed", ex);
        }

        // IRSDKSharper has no IDisposable вЂ” Win32 handles (MMF, named events) are
        // only released via GC finalizers on the SafeHandle wrappers inside IRacingSdk.
        // We must null the field before collecting so the instance becomes GC-eligible;
        // without this, GC.Collect() cannot see the object and handles stay open,
        // conflicting with iOverlay or iRacing re-init on the next app launch.
        var disposeStopCompleted = _stoppedGate.Wait(TimeSpan.FromSeconds(3));
        disposeStopSw.Stop();
        TraceLifecycle($"Dispose stop wait: completed={disposeStopCompleted}, elapsedMs={disposeStopSw.ElapsedMilliseconds}");
        _sdk.OnStopped -= _stoppedHandler;
        _sdk = null!;
        ReleaseSdkHandles("Dispose");

        AppLog.Info("IRacingPoller disposed вЂ” SDK handles released.");
    }

    /// <summary>
    /// Forces a full, blocking GC cycle to flush SafeHandle finalizers inside the
    /// now-unreferenced <see cref="IRacingSdk"/> instance.  Must be called after
    /// <c>_sdk</c> has been set to <c>null</c>.
    /// <para>
    /// Two passes are required: the first GC pass discovers the unreachable objects and
    /// enqueues their finalizers; <see cref="GC.WaitForPendingFinalizers"/> runs them
    /// (closing the Win32 handles); the second pass reclaims the memory.
    /// </para>
    /// </summary>
    private static void ReleaseSdkHandles(string context)
    {
        if (DebugDisableForcedGcRelease)
        {
            AppLog.Warn(
                $"IRacingPoller {context}: forced GC handle release skipped by debug toggle " +
                $"({EnvDisableForcedGcRelease}=1).");
            return;
        }

        var gcSw = Stopwatch.StartNew();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        gcSw.Stop();
        TraceLifecycle($"{context} forced GC handle release elapsedMs={gcSw.ElapsedMilliseconds}");
    }

    private static bool GetEnvFlag(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static void TraceLifecycle(string message)
    {
        if (DebugTraceLifecycle)
            AppLog.Info($"IRacingPoller lifecycle: {message}");
    }
}


