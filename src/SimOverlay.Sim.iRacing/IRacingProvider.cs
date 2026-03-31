using System.IO.MemoryMappedFiles;
using SimOverlay.Core;
using SimOverlay.Sim.Contracts;

namespace SimOverlay.Sim.iRacing;

/// <summary>
/// <see cref="ISimProvider"/> implementation for iRacing.
/// <para>
/// Detection uses a lightweight named MMF open attempt so the caller can check
/// <see cref="IsRunning"/> at any time without starting the full SDK stack.
/// The polling machinery (<see cref="IRacingPoller"/> / IRSDKSharper) is only
/// started when <see cref="Start"/> is called.
/// </para>
/// </summary>
public sealed class IRacingProvider : ISimProvider
{
    // iRacing creates this shared memory segment when the process starts.
    private const string IracingMmfName = "Local\\IRSDKMemMapFileName";

    private readonly ISimDataBus _bus;
    private IRacingPoller?       _poller;
    private bool                 _started;

    /// <inheritdoc/>
    public string SimId => "iRacing";

    /// <inheritdoc/>
    public event Action<SimState>? StateChanged;

    public IRacingProvider(ISimDataBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Returns <c>true</c> if the iRacing shared memory mapped file exists, which
    /// indicates the iRacing process is running (regardless of whether a session is active).
    /// This call is intentionally lightweight — it does not start or touch any SDK state.
    /// </summary>
    public bool IsRunning()
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(IracingMmfName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Starts the IRSDKSharper polling loop and immediately fires
    /// <see cref="StateChanged"/> with <see cref="SimState.Connected"/>
    /// (iRacing is known to be running when <c>Start</c> is called by the
    /// detection loop). <see cref="SimState.InSession"/> fires once IRSDKSharper
    /// confirms an active SDK connection.
    /// </summary>
    public void Start()
    {
        if (_started) return;
        _started = true;

        AppLog.Info("IRacingProvider starting.");
        _poller = new IRacingPoller(_bus, FireStateChanged);
        _poller.Start();

        // Signal that iRacing is running — session state will follow via OnConnected.
        FireStateChanged(SimState.Connected);
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

    private void FireStateChanged(SimState state) => StateChanged?.Invoke(state);
}
