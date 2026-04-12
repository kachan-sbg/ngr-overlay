using System.IO.MemoryMappedFiles;
using SimOverlay.Core;
using SimOverlay.Sim.Contracts;
using SimOverlay.Sim.LMU.SharedMemory;

namespace SimOverlay.Sim.LMU;

/// <summary>
/// <see cref="ISimProvider"/> implementation for Le Mans Ultimate.
/// <para>
/// Detection checks for the existence of the <c>LMU_Data</c> shared memory file,
/// which LMU creates when the process starts.
/// </para>
/// </summary>
public sealed class LmuProvider : ISimProvider, IDisposable
{
    private const string DataFileName = LmuSharedMemoryLayout.DataFile;

    private readonly ISimDataBus _bus;
    private LmuPoller?           _poller;
    private bool                 _started;

    /// <inheritdoc/>
    public string SimId => "LMU";

    /// <inheritdoc/>
    public event Action<SimState>? StateChanged;

    public LmuProvider(ISimDataBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Returns <c>true</c> if the <c>LMU_Data</c> shared memory file exists,
    /// indicating LMU is running.
    /// This check is intentionally lightweight — no SDK state is touched.
    /// </summary>
    public bool IsRunning()
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(DataFileName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Starts the LMU polling loop and fires <see cref="StateChanged"/> with
    /// <see cref="SimState.Connected"/>.  Session state follows once scoring data is valid.
    /// </summary>
    public void Start()
    {
        if (_started) return;
        _started = true;

        AppLog.Info("LmuProvider starting.");
        _poller = new LmuPoller(_bus, FireStateChanged);
        _poller.Start();

        FireStateChanged(SimState.Connected);
    }

    /// <summary>
    /// Stops the polling loop and fires <see cref="StateChanged"/> with
    /// <see cref="SimState.Disconnected"/>.
    /// </summary>
    public void Stop()
    {
        if (!_started) return;
        _started = false;

        AppLog.Info("LmuProvider stopping.");
        _poller?.Dispose();
        _poller = null;

        FireStateChanged(SimState.Disconnected);
    }

    /// <summary>Stops the polling loop if still running. Safe to call multiple times.</summary>
    public void Dispose() => Stop();

    private void FireStateChanged(SimState state) => StateChanged?.Invoke(state);
}
