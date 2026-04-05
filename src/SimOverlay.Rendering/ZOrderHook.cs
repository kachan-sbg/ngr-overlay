using SimOverlay.Core;
using SimOverlay.Rendering.Win32;

namespace SimOverlay.Rendering;

/// <summary>
/// Installs a <c>WinEvent EVENT_OBJECT_REORDER</c> hook that fires on the UI
/// message pump whenever any window's z-order changes.  When the window that
/// changed is a TOPMOST window not owned by us, <paramref name="onTopmostReorder"/>
/// is invoked so callers can immediately re-assert their own topmost position.
///
/// <para>
/// This counters games (e.g. iRacing in borderless mode) that call
/// <c>SetWindowPos(HWND_TOPMOST)</c> on every render frame.  Because the
/// callback runs <em>after</em> the game's call, we always go on top last
/// and therefore win the z-order race without polling.
/// </para>
///
/// <para>
/// Must be created on a thread that has a Win32 message pump (UI thread).
/// Dispose to unregister the hook.
/// </para>
/// </summary>
public sealed class ZOrderHook : IDisposable
{
    // Held as a field to prevent the delegate from being GC'd while the
    // native hook still references the function pointer.
    private readonly NativeMethods.WinEventDelegate _proc;
    private readonly nint _hook;
    private bool _disposed;

    /// <param name="onTopmostReorder">
    ///   Called on the UI thread when a TOPMOST window not in
    ///   <paramref name="ownedHandles"/> changes z-order.
    /// </param>
    /// <param name="ownedHandles">
    ///   HWNDs of our own overlay windows.  Z-order changes caused by our own
    ///   <c>BringToFront</c> calls are filtered out to prevent a feedback loop.
    /// </param>
    public ZOrderHook(Action onTopmostReorder, IReadOnlyList<nint> ownedHandles)
    {
        _proc = (_, _, hwnd, idObject, _, _, _) =>
        {
            // Only react to window-level reorders (not menus, scrollbars, etc.)
            if (idObject != NativeMethods.OBJID_WINDOW) return;

            // Ignore z-order changes we caused ourselves.
            for (var i = 0; i < ownedHandles.Count; i++)
                if (hwnd == ownedHandles[i]) return;

            // Only react when the window that changed is also TOPMOST — avoids
            // spurious re-asserts for every tooltip, notification, etc.
            var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
            if ((exStyle & NativeMethods.WS_EX_TOPMOST) == 0) return;

            var sb  = new System.Text.StringBuilder(256);
            var cls = new System.Text.StringBuilder(256);
            NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
            NativeMethods.GetClassName(hwnd, cls, cls.Capacity);
            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            AppLog.Info($"ZOrderHook fired: hwnd=0x{hwnd:X} title='{sb}' class='{cls}' pid={pid} — calling BringAllToFront");

            onTopmostReorder();
        };

        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_REORDER, NativeMethods.EVENT_OBJECT_REORDER,
            nint.Zero, _proc, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

        if (_hook == nint.Zero)
            AppLog.Warn("ZOrderHook: SetWinEventHook returned null — z-order re-assertion disabled.");
        else
            AppLog.Info($"ZOrderHook installed (handle=0x{_hook:X})");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hook != nint.Zero)
            NativeMethods.UnhookWinEvent(_hook);
    }
}
