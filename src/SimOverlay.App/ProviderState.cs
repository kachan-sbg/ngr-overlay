namespace SimOverlay.App;

/// <summary>
/// The lifecycle state of a single <see cref="Sim.Contracts.ISimProvider"/> as tracked
/// by <see cref="SimDetector"/>.
/// </summary>
public enum ProviderState
{
    /// <summary>
    /// The sim is not running.  <see cref="Sim.Contracts.ISimProvider.IsRunning"/> returns
    /// <c>false</c>.  No connection has been made.
    /// </summary>
    Idle,

    /// <summary>
    /// The sim is running (<see cref="Sim.Contracts.ISimProvider.IsRunning"/> returns
    /// <c>true</c>) but is not yet the active data source — either another provider is
    /// already <see cref="Active"/>, or no provider has been chosen yet this tick.
    /// Transitions to <see cref="Active"/> as soon as no other provider is active.
    /// </summary>
    Available,

    /// <summary>
    /// This provider is the current data source.
    /// <see cref="Sim.Contracts.ISimProvider.Start"/> has been called and telemetry is
    /// flowing to the bus.  Remains active until the sim stops and the disconnect debounce
    /// threshold is reached — even if another sim starts in the meantime.
    /// </summary>
    Active,

    /// <summary>
    /// Was <see cref="Active"/> but <see cref="Sim.Contracts.ISimProvider.IsRunning"/>
    /// returned <c>false</c>.  Counting consecutive false results; if the sim comes back
    /// before the threshold is reached the state returns to <see cref="Active"/> without
    /// restarting.  Confirmed dead after <c>DisconnectThreshold</c> consecutive false
    /// results → transitions to <see cref="Idle"/>.
    /// </summary>
    Disconnecting,
}
