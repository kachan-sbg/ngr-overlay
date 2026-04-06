using System.Collections.Immutable;
using IRSDKSharper;
using SimOverlay.Core;
using SimOverlay.Sim.Contracts;

namespace SimOverlay.Sim.iRacing;

/// <summary>
/// Wraps <see cref="IRacingSdk"/> and publishes <see cref="DriverData"/>,
/// <see cref="RelativeData"/>, and <see cref="SessionData"/> to the <see cref="ISimDataBus"/>.
/// <para>
/// IRSDKSharper fires <c>OnTelemetryData</c> at ~60 Hz on its own background thread.
/// <c>OnSessionInfo</c> fires whenever the iRacing session YAML changes.
/// </para>
/// </summary>
internal sealed class IRacingPoller : IDisposable
{
    // Publish RelativeData every N telemetry ticks → ~10 Hz at 60 Hz input.
    private const int RelativePublishInterval = 6;

    // iRacing allocates exactly 64 car-index slots in every telemetry array.
    private const int MaxCars = 64;

    private readonly IRacingSdk        _sdk;
    private readonly ISimDataBus      _bus;
    private readonly Action<SimState> _onStateChanged;

    // Cached driver list, updated each time OnSessionInfo fires.
    private ImmutableArray<DriverSnapshot> _cachedDrivers =
        ImmutableArray<DriverSnapshot>.Empty;

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
        _onStateChanged(SimState.InSession);
    }

    private void HandleDisconnected()
    {
        AppLog.Info("iRacing SDK disconnected — waiting for session.");
        _onStateChanged(SimState.Connected);
    }

    private void HandleSessionInfo()
    {
        try
        {
            var (drivers, session) = IRacingSessionDecoder.Decode(_sdk.Data);
            _cachedDrivers = drivers.ToImmutableArray();
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

            if (++_telemetryFrameCount % RelativePublishInterval == 0)
                PublishRelativeData();
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
        var data       = _sdk.Data;
        var bestLapSec = data.GetFloat("LapBestLapTime");
        var lastLapSec = data.GetFloat("LapLastLapTime");
        var delta             = data.GetFloat("LapDeltaToBestLap");
        var deltaSessionBest  = data.GetFloat("LapDeltaToSessionBestLap");
        var position          = data.GetInt("PlayerCarPosition");
        var lap               = data.GetInt("Lap");

        _bus.Publish(new DriverData
        {
            Position              = position,
            Lap                   = lap,
            BestLapTime           = bestLapSec > 0 ? TimeSpan.FromSeconds(bestLapSec) : TimeSpan.Zero,
            LastLapTime           = lastLapSec > 0 ? TimeSpan.FromSeconds(lastLapSec) : TimeSpan.Zero,
            LapDeltaVsBestLap     = delta,
            LapDeltaVsSessionBest = deltaSessionBest,
        });
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
        var lapDistPcts = new float[MaxCars];
        var positions   = new int[MaxCars];
        var laps        = new int[MaxCars];

        for (var i = 0; i < MaxCars; i++)
        {
            lapDistPcts[i] = data.GetFloat("CarIdxLapDistPct", i);
            positions[i]   = data.GetInt("CarIdxPosition",    i);
            laps[i]        = data.GetInt("CarIdxLap",         i);
        }

        var snapshot = new TelemetrySnapshot(
            PlayerCarIdx:    playerCarIdx,
            LapDistPcts:     lapDistPcts,
            Positions:       positions,
            Laps:            laps,
            EstimatedLapTime: estLapTimeSec);

        var relativeData = IRacingRelativeCalculator.Compute(snapshot, _cachedDrivers);
        _bus.Publish(relativeData);
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
