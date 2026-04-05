namespace SimOverlay.Core.Events;

/// <summary>
/// Published on <see cref="ISimDataBus"/> whenever the active sim provider's
/// connection state changes. Overlays subscribe to this to show placeholder
/// text when no session is active.
/// </summary>
public sealed record SimStateChangedEvent(SimState State);
