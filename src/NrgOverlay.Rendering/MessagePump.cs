using NrgOverlay.Rendering.Win32;
using System.Runtime.InteropServices;
using NrgOverlay.Core;

namespace NrgOverlay.Rendering;

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
    /// <param name="onMessage">
    /// Optional callback invoked for every message before dispatch.
    /// Receives the message id and wParam (sufficient for WM_HOTKEY handling).
    /// </param>
    public static void Run(Action<uint, nint>? onMessage = null)
    {
        while (NativeMethods.GetMessage(out var msg, nint.Zero, 0, 0))
        {
            onMessage?.Invoke(msg.message, msg.wParam);
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }
    }

    /// <summary>
    /// Posts WM_QUIT to terminate the message loop started by <see cref="Run"/>.
    /// Safe to call from any thread.
    /// </summary>
    public static void Quit() => NativeMethods.PostQuitMessage(0);

    /// <summary>
    /// Registers a global hotkey that delivers <c>WM_HOTKEY</c> to the message loop.
    /// Returns the hotkey id on success, or -1 if registration failed.
    /// </summary>
    /// <param name="modifiers">MOD_ALT=1, MOD_CONTROL=2, MOD_SHIFT=4, MOD_WIN=8, 0=none.</param>
    /// <param name="virtualKey">Virtual-key code (e.g. 0x78 = F9).</param>
    public static int RegisterHotKey(uint modifiers, uint virtualKey)
    {
        // Use the next available id (start at 1; 0 is reserved by Windows).
        var id = System.Threading.Interlocked.Increment(ref _nextHotkeyId);
        if (NativeMethods.RegisterHotKey(nint.Zero, id, modifiers, virtualKey))
            return id;

        var err = Marshal.GetLastWin32Error();
        AppLog.Warn($"RegisterHotKey failed (id={id}, modifiers={modifiers}, vk=0x{virtualKey:X2}, win32={err}).");
        return -1;
    }

    /// <summary>Unregisters a hotkey previously registered with <see cref="RegisterHotKey"/>.</summary>
    public static void UnregisterHotKey(int id) =>
        NativeMethods.UnregisterHotKey(nint.Zero, id);

    /// <summary>Message id for WM_HOTKEY вЂ” use in <see cref="Run"/>'s onMessage callback.</summary>
    public const uint WmHotKey = NativeMethods.WM_HOTKEY;

    private static int _nextHotkeyId = 0;
}

