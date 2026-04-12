using System.IO.MemoryMappedFiles;
using SimOverlay.Core;
using SimOverlay.Sim.Contracts;

namespace SimOverlay.Sim.iRacing;

/// <summary>
/// <see cref="ISimProvider"/> implementation for iRacing.
/// <para>
/// Detection reads the iRacing SDK shared memory header directly — no process enumeration.
/// The polling machinery (<see cref="IRacingPoller"/> / IRSDKSharper) is only started when
/// <see cref="Start"/> is called.
/// </para>
/// </summary>
public sealed class IRacingProvider : ISimProvider, IDisposable
{
    // The iRacing SDK shared memory file name.
    // iRacingSVC.exe (the background service) holds this file open permanently but does NOT
    // set the irsdk_stConnected bit.  Reading the status field avoids the false positive
    // that a plain "can the file be opened?" check would produce.
    private const string IracingMmfName = "Local\\IRSDKMemMapFileName";

    // Byte offset of `int status` within the irsdk_header struct (after `int ver`).
    // Bit 0 = irsdk_stConnected: set by the sim when it is running, cleared on exit.
    private const int StatusOffset        = 4;
    private const int StatusConnectedBit  = 0x01;

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
    /// Returns <c>true</c> when the iRacing SDK shared memory header reports the sim as
    /// connected (<c>irsdk_stConnected</c> bit set).
    /// <para>
    /// Checking the status field — rather than file existence — correctly handles the
    /// <c>iRacingSVC.exe</c> background service, which keeps the MMF open at all times
    /// but never sets the connected bit when the sim itself is not running.
    /// </para>
    /// </summary>
    public bool IsRunning()
    {
        try
        {
            using var mmf  = MemoryMappedFile.OpenExisting(IracingMmfName, MemoryMappedFileRights.Read);
            using var view = mmf.CreateViewAccessor(0, 8, MemoryMappedFileAccess.Read);
            return (view.ReadInt32(StatusOffset) & StatusConnectedBit) != 0;
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

    /// <summary>Stops the polling loop if still running. Safe to call multiple times.</summary>
    public void Dispose() => Stop();

    private void FireStateChanged(SimState state) => StateChanged?.Invoke(state);
}
