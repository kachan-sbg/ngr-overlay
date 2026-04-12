using System.Diagnostics;
using SimOverlay.Core;
using SimOverlay.Sim.Contracts;

namespace SimOverlay.Sim.iRacing;

/// <summary>
/// <see cref="ISimProvider"/> implementation for iRacing.
/// <para>
/// Detection checks for the <c>iRacingSim64.exe</c> process so the caller can check
/// <see cref="IsRunning"/> at any time without starting the full SDK stack.
/// The polling machinery (<see cref="IRacingPoller"/> / IRSDKSharper) is only
/// started when <see cref="Start"/> is called.
/// </para>
/// </summary>
public sealed class IRacingProvider : ISimProvider
{
    // The iRacing sim executable name (without .exe).
    // iRacingSVC.exe (the background service) also creates the IRSDK MMF at Windows startup,
    // so an MMF open-check incorrectly returns true even when the sim is not running.
    // Process detection is the reliable way to distinguish "sim open" from "service running".
    private const string IracingProcessName = "iRacingSim64";

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
    /// Returns <c>true</c> if the iRacing sim process (<c>iRacingSim64.exe</c>) is running.
    /// <para>
    /// We intentionally do NOT check the <c>Local\IRSDKMemMapFileName</c> MMF here because
    /// the iRacing background service (<c>iRacingSVC.exe</c>) creates and holds that file
    /// permanently at Windows startup — making the MMF check return <c>true</c> even when
    /// the sim itself is not open, which would prevent other providers (e.g. LMU) from
    /// ever being detected.
    /// </para>
    /// </summary>
    public bool IsRunning()
    {
        var procs = Process.GetProcessesByName(IracingProcessName);
        bool running = procs.Length > 0;
        foreach (var p in procs) p.Dispose();
        return running;
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
