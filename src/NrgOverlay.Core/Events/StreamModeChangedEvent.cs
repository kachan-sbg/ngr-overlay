namespace NrgOverlay.Core.Events;

/// <summary>
/// Published on <see cref="ISimDataBus"/> when the user toggles stream mode.
/// Overlays subscribe to this to invalidate their cached render resources so
/// the effective config (base vs. stream override) is re-resolved on the next frame.
/// </summary>
public sealed record StreamModeChangedEvent(bool IsActive);

