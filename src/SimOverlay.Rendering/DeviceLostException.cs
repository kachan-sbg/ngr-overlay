namespace SimOverlay.Rendering;

/// <summary>
/// Thrown by <see cref="OverlayWindow.Render"/> when the D3D/D2D device is lost
/// (<c>DXGI_ERROR_DEVICE_REMOVED</c> or <c>DXGI_ERROR_DEVICE_RESET</c>).
/// <see cref="BaseOverlay"/> catches this in the render loop and triggers recovery.
/// </summary>
public sealed class DeviceLostException : Exception
{
    public DeviceLostException()
        : base("DXGI device lost (DEVICE_REMOVED or DEVICE_RESET).") { }

    public DeviceLostException(Exception inner)
        : base("DXGI device lost (DEVICE_REMOVED or DEVICE_RESET).", inner) { }
}
