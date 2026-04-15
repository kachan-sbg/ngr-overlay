using NrgOverlay.Core;

namespace NrgOverlay.Sim.Contracts;

public interface ISimProvider
{
    /// <summary>Unique identifier for this sim, e.g. "iRacing".</summary>
    string SimId { get; }

    /// <summary>
    /// Fast check for whether the sim is running. Called on every detector poll tick
    /// (every ~5 s by default). Must not block or throw.
    /// </summary>
    bool IsRunning();

    /// <summary>Begin the polling loop and start publishing data to <see cref="Core.ISimDataBus"/>.</summary>
    void Start();

    /// <summary>Stop the polling loop and release sim resources.</summary>
    void Stop();

    /// <summary>Fires when the sim connection state changes.</summary>
    event Action<SimState> StateChanged;
}

