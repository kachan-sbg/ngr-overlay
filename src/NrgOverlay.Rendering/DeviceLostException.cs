namespace NrgOverlay.Rendering;

/// <summary>
/// Thrown by <see cref="OverlayWindow.Render"/> when the D2D render target is lost
/// (<c>D2DERR_RECREATE_TARGET</c>).
/// <see cref="BaseOverlay"/> catches this in the render loop and triggers recovery.
/// </summary>
public sealed class DeviceLostException : Exception
{
    public DeviceLostException()
        : base("D2D render target lost (D2DERR_RECREATE_TARGET).") { }

    public DeviceLostException(Exception inner)
        : base("D2D render target lost (D2DERR_RECREATE_TARGET).", inner) { }
}

