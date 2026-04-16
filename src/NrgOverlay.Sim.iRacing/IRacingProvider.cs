using NrgOverlay.Core;
using NrgOverlay.Core.Config;
using NrgOverlay.Sim.Contracts;

namespace NrgOverlay.Sim.iRacing;

/// <summary>
/// <see cref="ISimProvider"/> implementation for iRacing.
/// <para>
/// Detection reads the iRacing SDK shared memory header directly - no process enumeration.
/// The polling machinery (<see cref="IRacingPoller"/> / IRSDKSharper) is only started when
/// <see cref="Start"/> is called.
/// </para>
/// </summary>
public sealed class IRacingProvider : ISimProvider, IDisposable
{
    private readonly ISimDataBus _bus;
    private readonly AppConfig _appConfig;
    private readonly ConfigStore _configStore;
    private readonly IIRacingConnectionProbe _connectionProbe;
    private IRacingPoller? _poller;
    private bool _started;

    /// <inheritdoc/>
    public string SimId => "iRacing";

    /// <inheritdoc/>
    public event Action<SimState>? StateChanged;

    public IRacingProvider(ISimDataBus bus, AppConfig appConfig, ConfigStore configStore)
        : this(bus, appConfig, configStore, new IRacingConnectionProbe())
    {
    }

    internal IRacingProvider(
        ISimDataBus bus,
        AppConfig appConfig,
        ConfigStore configStore,
        IIRacingConnectionProbe connectionProbe)
    {
        _bus = bus;
        _appConfig = appConfig;
        _configStore = configStore;
        _connectionProbe = connectionProbe;
    }

    /// <summary>
    /// Returns <c>true</c> when the iRacing SDK shared memory header reports the sim as
    /// connected (<c>irsdk_stConnected</c> bit set).
    /// </summary>
    public bool IsRunning()
    {
        try
        {
            return _connectionProbe.IsConnected();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Starts the IRSDKSharper polling loop and immediately fires
    /// <see cref="StateChanged"/> with <see cref="SimState.InSession"/>.
    /// <para>
    /// We skip the intermediate <c>Connected</c> state because <see cref="IsRunning"/>
    /// already confirmed the sim is live before this method is called. Waiting for
    /// IRSDKSharper's <c>OnConnected</c> event is unreliable when iRacing is already
    /// running at SDK init time - the event sometimes never fires, leaving overlays locked.
    /// Firing <c>InSession</c> immediately unblocks overlays; IRSDKSharper will connect
    /// internally and telemetry will start flowing within a second or two. If the SDK stalls,
    /// the watchdog inside <see cref="IRacingPoller"/> detects it and restarts the SDK.
    /// </para>
    /// </summary>
    public void Start()
    {
        if (_started) return;
        _started = true;

        AppLog.Info("IRacingProvider starting.");
        _poller = new IRacingPoller(_bus, _appConfig, _configStore, FireStateChanged);
        _poller.Start();

        // iRacing is confirmed running - fire InSession immediately so overlays unlock.
        // IRSDKSharper will also fire HandleConnected once its loop attaches, which
        // re-fires InSession (idempotent - SimDetector ignores repeated same-state transitions).
        FireStateChanged(SimState.InSession);
    }

    /// <summary>
    /// Stops the polling loop, disposes SDK resources, and fires
    /// <see cref="StateChanged"/> with <see cref="SimState.Disconnected"/>.
    /// </summary>
    public void Stop()
    {
        if (!_started) return;
        _started = false;

        AppLog.Info("IRacingProvider stopping.");
        _poller?.Dispose();
        _poller = null;

        FireStateChanged(SimState.Disconnected);
    }

    /// <summary>Stops the polling loop if still running. Safe to call multiple times.</summary>
    public void Dispose() => Stop();

    private void FireStateChanged(SimState state) => StateChanged?.Invoke(state);
}
