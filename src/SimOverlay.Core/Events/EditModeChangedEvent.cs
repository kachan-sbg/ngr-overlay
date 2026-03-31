namespace SimOverlay.Core.Events;

/// <summary>
/// Published (typically by the tray icon controller) to lock or unlock all
/// overlay windows globally. When <see cref="IsLocked"/> is <c>false</c> the
/// overlays enter edit mode: borders appear and the windows accept mouse input
/// for drag and resize. When <c>true</c> the windows revert to click-through.
/// </summary>
public sealed record EditModeChangedEvent(bool IsLocked);
