using SimOverlay.Core;

namespace SimOverlay.Sim.Contracts;

public interface ISimProvider
{
    /// <summary>Unique identifier for this sim, e.g. "iRacing".</summary>
    string SimId { get; }

    /// <summary>
    /// Fast check for whether the sim is running. Called every ~2 s from the detection loop.
    /// Must not block or throw.
    /// </summary>
    bool IsRunning();

    /// <summary>Begin the polling loop and start publishing data to <see cref="Core.ISimDataBus"/>.</summary>
    void Start();

    /// <summary>Stop the polling loop and release sim resources.</summary>
    void Stop();

    /// <summary>Fires when the sim connection state changes.</summary>
    event Action<SimState> StateChanged;
}
