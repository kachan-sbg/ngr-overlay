using SimOverlay.Rendering.Win32;

namespace SimOverlay.Rendering;

/// <summary>
/// Runs a standard Win32 message pump on the calling thread.
/// Blocks until <c>PostQuitMessage</c> is called, then returns.
/// </summary>
public static class MessagePump
{
    /// <summary>
    /// Enters the message loop. Returns when a WM_QUIT is received.
    /// Must be called on the thread that owns the overlay windows (the STA thread).
    /// </summary>
    public static void Run()
    {
        while (NativeMethods.GetMessage(out var msg, nint.Zero, 0, 0))
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }
    }

    /// <summary>
    /// Posts WM_QUIT to terminate the message loop started by <see cref="Run"/>.
    /// Safe to call from any thread.
    /// </summary>
    public static void Quit() => NativeMethods.PostQuitMessage(0);
}
