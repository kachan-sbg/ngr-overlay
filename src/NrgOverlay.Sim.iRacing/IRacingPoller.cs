using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
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
    private readonly IIRacingConnectionProbe _connectionProbe;

    // IRacingSdk is recreated by the watchdog when stalled; not readonly.
    private IRacingSdk _sdk;

    // Cached driver list, updated each time OnSessionInfo fires.
    private ImmutableArray<DriverSnapshot> _cachedDrivers =
        ImmutableArray<DriverSnapshot>.Empty;

    // Cached track length (metres), parsed from session YAML.
    private float _trackLengthMeters;
    // Cached configured session duration (time-limited sessions only).
    private TimeSpan? _sessionTimeLimit;
    // Cached session-best lap parsed from session YAML (valid lap table).
    private TimeSpan _sessionBestLapFromYaml = TimeSpan.Zero;

    private readonly FuelConsumptionTracker    _fuelTracker    = new();
    private readonly CarStateTracker           _carStateTracker = new();
    private readonly IRacingRelativeCalculator _calculator      = new();
    private readonly Queue<float>[] _recentLapTimesByCarIdx = Enumerable.Range(0, MaxCars)
        .Select(_ => new Queue<float>(5))
        .ToArray();
    private readonly int[] _lastLapSampledByCarIdx = new int[MaxCars];
    private int _cachedPlayerCarIdx = -1;

    // Telemetry frame counter вЂ” incremented on every HandleTelemetryData call.
    // The watchdog reads this on a different thread; volatile ensures visibility.
    private volatile int _telemetryFrameCount;

    // Watchdog state.
    private readonly Timer _watchdogTimer;
    private readonly IRacingWatchdogController _watchdog;
    private volatile bool _restarting;       // true while SDK restart is in progress

    // Set to true the first time OnConnected fires. The watchdog never restarts the SDK
    // until this is true вЂ” iRacing may still be loading (MMF connected bit set, but no
    // telemetry yet). Restarting during loading is what causes the iRacing/VS Code crash.
    private bool _hasConnected;
    private bool _sessionFieldDumpLogged;

    private bool _disposed;
    private readonly object _stateLock = new();
    private long _stateVersion;
    private SessionData? _stateSession;
    private DriverData? _stateDriver;
    private TelemetryData? _stateTelemetry;
    private RelativeData? _stateRelative;
    private StandingsData? _stateStandings;
    private PitData? _statePit;
    private TrackMapData? _stateTrackMap;
    private WeatherData? _stateWeather;

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
        _connectionProbe = new IRacingConnectionProbe(MmfName, MmfStatusOffset, MmfConnectedBit);
        _appConfig.GlobalSettings ??= new GlobalSettings();
        _appConfig.GlobalSettings.DriverCountryOverrides ??= [];
        _appConfig.GlobalSettings.DriverCountryCache ??= [];
        _appConfig.GlobalSettings.DriverCountryByFlairId ??= [];
        _appConfig.GlobalSettings.DriverCountryIso2ByFlairId ??= [];

        SeedFlairCountryMappingsFromDefaults();

        _stoppedHandler = () => _stoppedGate.Set();
        _watchdog = new IRacingWatchdogController(WatchdogStallTicks);

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
        _sessionFieldDumpLogged = false;
        _fuelTracker.Reset();
        _carStateTracker.Reset();
        _calculator.Reset();
        ResetLeaderLapAverages();
        _sessionBestLapFromYaml = TimeSpan.Zero;
        _cachedPlayerCarIdx = -1;
        ClearRaceStateSnapshot();
        _onStateChanged(SimState.InSession);
    }

    private void HandleDisconnected()
    {
        AppLog.Info("iRacing SDK disconnected вЂ” waiting for session.");
        _fuelTracker.Reset();
        _carStateTracker.Reset();
        _calculator.Reset();
        ResetLeaderLapAverages();
        _sessionBestLapFromYaml = TimeSpan.Zero;
        _cachedPlayerCarIdx = -1;
        ClearRaceStateSnapshot();
        _onStateChanged(SimState.Connected);
    }

    private void HandleSessionInfo()
    {
        try
        {
            var (drivers, session) = IRacingSessionDecoder.Decode(_sdk.Data);
            _cachedDrivers = ResolveDriverCountries(drivers);
            TraceCountryResolution(_cachedDrivers);
            TryUpdateCachedPlayerCarIdx(_sdk.Data);

            // Cache track length for TrackMapData (changes only on track change).
            _trackLengthMeters = ParseTrackLengthMeters(
                _sdk.Data.SessionInfo?.WeekendInfo?.TrackLength);
            _sessionTimeLimit = session.SessionTimeLimit > TimeSpan.Zero
                ? session.SessionTimeLimit
                : null;
            _sessionBestLapFromYaml = session.SessionBestLapTime;

            _bus.Publish(session);
            PublishRaceStateSnapshot(() => _stateSession = session);
            AppLog.Info($"Session info updated: {session.TrackName} / {session.SessionType}");
            DumpSessionSdkFieldsOnce();
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
        DumpSessionSdkFieldsOnce();

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
        var decision = _watchdog.Evaluate(
            currentFrame: current,
            hasConnected: _hasConnected,
            simConnected: _connectionProbe.IsConnected(),
            restartDisabled: DebugDisableWatchdogRestart);

        if (decision.ShouldLogStall)
        {
            AppLog.Info(
                $"IRacingPoller: telemetry stalled for " +
                $"{decision.StallCount * WatchdogIntervalSec}s while iRacing MMF is connected " +
                $"(stall {decision.StallCount}/{WatchdogStallTicks}).");
        }

        if (decision.RestartSuppressed)
        {
            AppLog.Warn(
                "IRacingPoller watchdog restart suppressed by debug toggle " +
                $"({EnvDisableWatchdogRestart}=1).");
            return;
        }

        if (decision.ShouldRestart)
            RestartSdk();
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
            _watchdog.CompleteRestartCycle();
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
        var hasSessionBestRef = SafeGetBool(data, "LapDeltaToSessionBestLap_OK");
        var position         = SafeGetInt(data, "PlayerCarPosition");
        var lap              = SafeGetInt(data, "Lap");

        // Live session timing вЂ” read from telemetry so the display counts down in
        // real time rather than showing a stale YAML snapshot value.
        var sessionTimeSec       = SafeGetTelemetrySeconds(data, "SessionTime");
        var sessionTimeRemainSec = SafeGetTelemetrySeconds(data, "SessionTimeRemain");
        var sessionLapsRemainRaw = SafeGetInt(data, "SessionLapsRemain");

        // In-game time of day (real-time, not YAML snapshot).
        var sessionTimeOfDaySec = SafeGetTelemetrySeconds(data, "SessionTimeOfDay");
        TimeOnly? gameTimeOfDay = sessionTimeOfDaySec > 0f
            ? TimeOnly.FromTimeSpan(TimeSpan.FromSeconds(sessionTimeOfDaySec % 86400.0))
            : null;

        // Player-scoped session best only.
        // Using field-wide minima (CarIdxBestLapTime / session tables) can pick unrelated cars/classes.
        // Keep SessionBestLapTime empty until iRacing signals a valid session-best reference.
        var sessionBestSec = hasSessionBestRef && bestLapSec > 0f
            ? bestLapSec
            : 0f;

        var sessionTimeElapsedValid = sessionTimeSec >= 0f && sessionTimeSec < 1e10f;
        var sessionTimeRemainValid = sessionTimeRemainSec >= 0f && sessionTimeRemainSec < 1e10f;
        TimeSpan? sessionRemaining = sessionTimeRemainValid
            ? ClampedTimeSpan(sessionTimeRemainSec)
            : (TimeSpan?)null;

        var sessionElapsed = TimeSpan.Zero;
        if (sessionRemaining.HasValue && _sessionTimeLimit is { } limit && limit > TimeSpan.Zero)
        {
            // Prefer elapsed = configured-session-duration - live-remaining.
            // This path avoids bogus SessionTime telemetry values seen in some sessions.
            var boundedRemaining = sessionRemaining.Value > limit ? limit : sessionRemaining.Value;
            sessionElapsed = limit - boundedRemaining;
        }
        else if (sessionTimeElapsedValid)
        {
            // Fallback only when we cannot derive elapsed from the session limit.
            sessionElapsed = ClampedTimeSpan(sessionTimeSec);
        }

        var sessionLapsRemaining =
            sessionLapsRemainRaw >= 0 && sessionLapsRemainRaw < short.MaxValue
                ? sessionLapsRemainRaw
                : (int?)null;

        var driverData = new DriverData
        {
            Position                = position,
            Lap                     = lap,
            BestLapTime             = bestLapSec > 0 ? TimeSpan.FromSeconds(bestLapSec) : TimeSpan.Zero,
            LastLapTime             = lastLapSec > 0 ? TimeSpan.FromSeconds(lastLapSec) : TimeSpan.Zero,
            SessionBestLapTime      = sessionBestSec > 0 ? TimeSpan.FromSeconds(sessionBestSec) : TimeSpan.Zero,
            LapDeltaVsBestLap       = delta,
            LapDeltaVsSessionBest   = deltaSessionBest,
            HasSessionBestReference = hasSessionBestRef || sessionBestSec > 0f,
            SessionTimeElapsed      = sessionElapsed,
            // iRacing returns -1 for SessionTimeRemain when the session has no time limit (laps-based).
            // It also returns a huge sentinel float (~3.4e38) in some session types вЂ” treat those as null too.
            SessionTimeRemaining    = sessionRemaining,
            GameTimeOfDay           = gameTimeOfDay,
            EstimatedLapsRemaining  = EstimateLapsRemaining(data, sessionRemaining),
            SessionLapsRemaining    = sessionLapsRemaining,
        };

        _bus.Publish(driverData);
        PublishRaceStateSnapshot(() => _stateDriver = driverData);
    }

    private void PublishTelemetryData()
    {
        var data         = _sdk.Data;
        var lap          = SafeGetInt(data, "Lap");
        var fuelLevel    = SafeGetFloat(data, "FuelLevel");
        var sessionFlags = SafeGetInt(data, "SessionFlags");

        _fuelTracker.Update(lap, fuelLevel, sessionFlags);

        var telemetryData = new TelemetryData(
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
        );

        _bus.Publish(telemetryData);
        PublishRaceStateSnapshot(() => _stateTelemetry = telemetryData);
    }

    private void PublishRelativeData()
    {
        var data         = _sdk.Data;
        var playerCarIdx = ResolvePlayerCarIdx(data);
        TracePlayerCarIdxResolution(data, playerCarIdx);

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
        PublishRaceStateSnapshot(() =>
        {
            _stateRelative = relativeData;
            _stateStandings = standingsData;
        });
    }

    private void PublishPitData()
    {
        var data         = _sdk.Data;
        var playerCarIdx = ResolvePlayerCarIdx(data);

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

        var pitData = new PitData(
            IsOnPitRoad:        isOnPitRoad,
            IsInPitStall:       isInPitStall,
            PitLimiterSpeedMps: SafeGetFloat(data, "PitSpeedLimit"),
            CurrentSpeedMps:    SafeGetFloat(data, "Speed"),
            PitLimiterActive:   pitLimiterOn,
            PitStopCount:       SafeGetInt(data, "CarIdxNumPitStops", playerCarIdx),
            RequestedService:   serviceFlags,
            FuelToAddLiters:    SafeGetFloat(data, "dpFuelFill")
        );
        _bus.Publish(pitData);
        PublishRaceStateSnapshot(() => _statePit = pitData);
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

        var weatherData = new WeatherData(
            AirTempC:         SafeGetFloat(data, "AirTemp"),
            TrackTempC:       SafeGetFloat(data, "TrackTempCrew"),
            WindSpeedMps:     SafeGetFloat(data, "WindVel"),
            WindDirectionDeg: windDirDeg,
            Humidity:         SafeGetFloat(data, "RelativeHumidity"),
            SkyCoverage:      SafeGetInt(data, "Skies"),
            TrackWetness:     trackWetnessNorm,
            IsPrecipitating:  SafeGetInt(data, "WeatherDeclaredWet") != 0
        );
        _bus.Publish(weatherData);
        PublishRaceStateSnapshot(() => _stateWeather = weatherData);
    }

    private void PublishTrackMapData()
    {
        var data         = _sdk.Data;
        var playerCarIdx = ResolvePlayerCarIdx(data);

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

        var trackMapData = new TrackMapData(
            TrackLengthMeters: _trackLengthMeters,
            Cars:              cars);
        _bus.Publish(trackMapData);
        PublishRaceStateSnapshot(() => _stateTrackMap = trackMapData);
    }

    private void ClearRaceStateSnapshot()
    {
        PublishRaceStateSnapshot(() =>
        {
            _stateSession   = null;
            _stateDriver    = null;
            _stateTelemetry = null;
            _stateRelative  = null;
            _stateStandings = null;
            _statePit       = null;
            _stateTrackMap  = null;
            _stateWeather   = null;
        });
    }

    private void PublishRaceStateSnapshot(Action update)
    {
        RaceStateSnapshot snapshot;
        lock (_stateLock)
        {
            update();
            snapshot = new RaceStateSnapshot
            {
                Version   = ++_stateVersion,
                Session   = _stateSession,
                Driver    = _stateDriver,
                Telemetry = _stateTelemetry,
                Relative  = _stateRelative,
                Standings = _stateStandings,
                Pit       = _statePit,
                TrackMap  = _stateTrackMap,
                Weather   = _stateWeather,
            };
        }

        _bus.Publish(snapshot);
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

    private static bool SafeGetBool(IRacingSdkData data, string name)
    {
        if (!data.TelemetryDataProperties.ContainsKey(name)) return false;
        return data.GetBool(name);
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

    private static float SafeGetTelemetrySeconds(IRacingSdkData data, string name)
    {
        if (!data.TelemetryDataProperties.ContainsKey(name))
            return 0f;

        var meta = data.TelemetryDataProperties[name];
        var varType = TryReadProperty(meta, "VarType")?.ToString() ?? string.Empty;
        if (varType.Contains("Double", StringComparison.OrdinalIgnoreCase))
            return (float)data.GetDouble(name);

        return data.GetFloat(name);
    }

    private int? EstimateLapsRemaining(IRacingSdkData data, TimeSpan? sessionRemaining)
    {
        if (!sessionRemaining.HasValue || sessionRemaining.Value <= TimeSpan.Zero)
            return null;

        var leaderCarIdx = FindLeaderCarIdx(data);
        if (leaderCarIdx < 0)
            return null;

        CaptureLeaderLapHistory(data, leaderCarIdx);

        var recent = _recentLapTimesByCarIdx[leaderCarIdx];
        if (recent.Count == 0)
            return null;

        var avgLapSec = recent.Average();
        if (!(avgLapSec > 0f))
            return null;

        var laps = (int)Math.Ceiling(sessionRemaining.Value.TotalSeconds / avgLapSec);
        return Math.Max(0, laps);
    }

    private static int FindLeaderCarIdx(IRacingSdkData data)
    {
        for (var i = 0; i < MaxCars; i++)
        {
            if (SafeGetInt(data, "CarIdxPosition", i) == 1)
                return i;
        }
        return -1;
    }

    private void CaptureLeaderLapHistory(IRacingSdkData data, int leaderCarIdx)
    {
        var currentLap = SafeGetInt(data, "CarIdxLap", leaderCarIdx);
        if (currentLap <= 0)
            return;

        if (currentLap <= _lastLapSampledByCarIdx[leaderCarIdx])
            return;

        var lastLapSec = SafeGetFloat(data, "CarIdxLastLapTime", leaderCarIdx);
        if (!(lastLapSec > 0f))
            return;

        _lastLapSampledByCarIdx[leaderCarIdx] = currentLap;
        var queue = _recentLapTimesByCarIdx[leaderCarIdx];
        queue.Enqueue(lastLapSec);
        while (queue.Count > 5)
            queue.Dequeue();
    }

    private void ResetLeaderLapAverages()
    {
        for (var i = 0; i < MaxCars; i++)
        {
            _recentLapTimesByCarIdx[i].Clear();
            _lastLapSampledByCarIdx[i] = 0;
        }
    }

    private int ResolvePlayerCarIdx(IRacingSdkData data)
    {
        if (TryUpdateCachedPlayerCarIdx(data))
            return _cachedPlayerCarIdx;

        // Last known good value (prevents temporary SessionInfo null/zero glitches from re-anchoring).
        if (_cachedPlayerCarIdx >= 0 && _cachedPlayerCarIdx < MaxCars)
            return _cachedPlayerCarIdx;

        return 0;
    }

    private bool TryUpdateCachedPlayerCarIdx(IRacingSdkData data)
    {
        var sessionIdx = data.SessionInfo?.DriverInfo?.DriverCarIdx ?? -1;
        if (sessionIdx >= 0 && sessionIdx < MaxCars)
        {
            _cachedPlayerCarIdx = sessionIdx;
            return true;
        }

        var telemetryIdx = SafeGetInt(data, "PlayerCarIdx");
        if (telemetryIdx >= 0 && telemetryIdx < MaxCars)
        {
            _cachedPlayerCarIdx = telemetryIdx;
            return true;
        }

        return false;
    }

    private void TracePlayerCarIdxResolution(IRacingSdkData data, int resolvedIdx)
    {
        if (!DebugTraceLifecycle) return;

        var sessionIdx = data.SessionInfo?.DriverInfo?.DriverCarIdx ?? -1;
        var telemetryIdx = SafeGetInt(data, "PlayerCarIdx");
        AppLog.Info(
            $"IRacingPoller player-car-idx: resolved={resolvedIdx}, session={sessionIdx}, " +
            $"telemetry={telemetryIdx}, cached={_cachedPlayerCarIdx}");
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

    private void DumpSessionSdkFieldsOnce()
    {
        if (_sessionFieldDumpLogged)
            return;

        try
        {
            var data = _sdk.Data;
            if (data?.SessionInfo == null)
                return;

            AppLog.Info("IRSDK Session Dump BEGIN | field | value");
            DumpYamlSessionSection(data);
            DumpTelemetrySessionFields(data);
            AppLog.Info("IRSDK Session Dump END");
            _sessionFieldDumpLogged = true;
        }
        catch (Exception ex)
        {
            AppLog.Exception("IRSDK Session Dump failed", ex);
        }
    }

    private static void DumpYamlSessionSection(IRacingSdkData data)
    {
        var info = data.SessionInfo;
        if (info == null)
            return;

        var weekend = info.WeekendInfo;
        if (weekend != null)
        {
            foreach (var prop in weekend.GetType().GetProperties(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (prop.Name.Contains("Driver", StringComparison.OrdinalIgnoreCase))
                    continue;

                object? value;
                try { value = prop.GetValue(weekend); }
                catch { value = null; }

                AppLog.Info($"IRSDK Session Dump | WeekendInfo.{prop.Name} | {FormatDumpValue(value)}");
            }
        }

        var sessions = info.SessionInfo?.Sessions;
        var sessionNum = data.GetInt("SessionNum");
        if (sessions == null)
            return;

        AppLog.Info($"IRSDK Session Dump | SessionInfo.SessionNum | {sessionNum}");
        AppLog.Info($"IRSDK Session Dump | SessionInfo.Sessions.Count | {sessions.Count}");

        if (sessionNum < 0 || sessionNum >= sessions.Count)
            return;

        var current = sessions[sessionNum];
        if (current == null)
            return;

        foreach (var prop in current.GetType().GetProperties(
                     System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (prop.Name.Contains("Driver", StringComparison.OrdinalIgnoreCase))
                continue;

            object? value;
            try { value = prop.GetValue(current); }
            catch { value = null; }

            AppLog.Info($"IRSDK Session Dump | SessionInfo.Current.{prop.Name} | {FormatDumpValue(value)}");
        }
    }

    private static void DumpTelemetrySessionFields(IRacingSdkData data)
    {
        var keys = data.TelemetryDataProperties.Keys
            .Where(IsSessionTelemetryField)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys)
        {
            if (!data.TelemetryDataProperties.TryGetValue(key, out var meta) || meta == null)
                continue;

            var count = TryReadIntProperty(meta, "Count");
            var varType = TryReadProperty(meta, "VarType")?.ToString() ?? "?";

            if (count > 1)
            {
                AppLog.Info($"IRSDK Session Dump | Telemetry.{key} | [array count={count}, type={varType}]");
                continue;
            }

            var value = ReadTelemetryScalarValue(data, key, varType);
            AppLog.Info($"IRSDK Session Dump | Telemetry.{key} | {value} (type={varType})");
        }
    }

    private static bool IsSessionTelemetryField(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (name.Contains("CarIdx", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Driver", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Player", StringComparison.OrdinalIgnoreCase))
            return false;

        return name.Contains("Session", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Weekend", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Lap", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "LapBestLapTime", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "LapLastLapTime", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "SessionFlags", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadTelemetryScalarValue(IRacingSdkData data, string key, string varType)
    {
        try
        {
            var lowered = varType.ToLowerInvariant();
            if (lowered.Contains("double"))
            {
                var value = data.GetDouble(key);
                return value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            }

            if (lowered.Contains("float"))
            {
                var value = data.GetFloat(key);
                return value.ToString("G9", System.Globalization.CultureInfo.InvariantCulture);
            }

            if (lowered.Contains("bitfield"))
                return data.GetBitField(key).ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (lowered.Contains("bool"))
                return data.GetBool(key) ? "true" : "false";

            if (lowered.Contains("char"))
            {
                var ch = data.GetChar(key);
                return char.IsControl(ch) ? ((int)ch).ToString(System.Globalization.CultureInfo.InvariantCulture) : ch.ToString();
            }

            return data.GetInt(key).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            return $"<read-error:{ex.GetType().Name}>";
        }
    }

    private static object? TryReadProperty(object source, string propertyName)
    {
        var prop = source.GetType().GetProperty(
            propertyName,
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.IgnoreCase);
        return prop?.GetValue(source);
    }

    private static int TryReadIntProperty(object source, string propertyName)
    {
        var raw = TryReadProperty(source, propertyName);
        if (raw == null)
            return 0;

        return raw switch
        {
            int i => i,
            long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
            _ => int.TryParse(raw.ToString(), out var parsed) ? parsed : 0,
        };
    }

    private static string FormatDumpValue(object? value)
    {
        if (value == null)
            return "<null>";

        return value switch
        {
            string s when string.IsNullOrEmpty(s) => "<empty>",
            string s => s,
            _ => value.ToString() ?? "<null>",
        };
    }
}


