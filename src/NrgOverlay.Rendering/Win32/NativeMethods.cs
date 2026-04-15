using System.Runtime.InteropServices;

namespace NrgOverlay.Rendering.Win32;

internal static class NativeMethods
{
    // -------------------------------------------------------------------------
    // Window style flags
    // -------------------------------------------------------------------------
    internal const uint WS_POPUP    = 0x80000000;
    internal const uint WS_VISIBLE  = 0x10000000;

    internal const int WS_EX_TOPMOST             = 0x00000008;
    internal const int WS_EX_TRANSPARENT         = 0x00000020;
    internal const int WS_EX_LAYERED             = 0x00080000;
    internal const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;
    // WS_EX_LAYERED: required so DWM composites our window in the overlay tier above
    // MPO (Multi-Plane Overlay) hardware planes used by games' DXGI flip chains.
    // Without it, game frames rendered via DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL sit above
    // DWM composition, making our overlays invisible even though Windows reports them
    // as visible. WS_EX_NOREDIRECTIONBITMAP: DWM skips creating a redundant redirection
    // surface; content comes from UpdateLayeredWindow's DIB bitmap instead.

    // SetLayeredWindowAttributes dwFlags
    internal const uint LWA_ALPHA = 0x00000002;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetLayeredWindowAttributes(
        nint hwnd, uint crKey, byte bAlpha, uint dwFlags);

    // WS_EX_TOOLWINDOW intentionally NOT used on overlay windows вЂ” it hides them from OBS's window picker.
    // It IS used on hidden owner windows (see OverlayWindow) where visibility is irrelevant.
    internal const int WS_EX_TOOLWINDOW = 0x00000080;

    // -------------------------------------------------------------------------
    // SetWindowLong / SetWindowLongPtr indices
    // -------------------------------------------------------------------------
    internal const int GWL_EXSTYLE = -20;

    // -------------------------------------------------------------------------
    // WM_ messages
    // -------------------------------------------------------------------------
    internal const uint WM_DESTROY      = 0x0002;
    internal const uint WM_SIZE         = 0x0005;
    internal const uint WM_MOVE         = 0x0003;
    internal const uint WM_NCHITTEST    = 0x0084;
    internal const uint WM_SYSCOMMAND   = 0x0112;

    // WM_SYSCOMMAND wParam values (mask off low 4 bits per MSDN)
    internal const nint SC_MINIMIZE     = 0xF020;

    // -------------------------------------------------------------------------
    // Hit-test return values
    // -------------------------------------------------------------------------
    internal const nint HTTRANSPARENT = -1;
    internal const nint HTCAPTION     = 2;
    internal const nint HTBOTTOMRIGHT = 17;

    // -------------------------------------------------------------------------
    // ShowWindow commands
    // -------------------------------------------------------------------------
    internal const int SW_HIDE    = 0;
    internal const int SW_SHOW    = 5;
    internal const int SW_RESTORE = 9;   // restores a minimized window to its normal size/position

    // WM_SIZE wParam values
    internal const nint SIZE_MINIMIZED = 1;

    // -------------------------------------------------------------------------
    // WNDCLASSEX
    // -------------------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WNDCLASSEX
    {
        public uint   cbSize;
        public uint   style;
        public nint   lpfnWndProc;
        public int    cbClsExtra;
        public int    cbWndExtra;
        public nint   hInstance;
        public nint   hIcon;
        public nint   hCursor;
        public nint   hbrBackground;
        public string? lpszMenuName;
        public string  lpszClassName;
        public nint   hIconSm;
    }

    // -------------------------------------------------------------------------
    // WndProc delegate
    // -------------------------------------------------------------------------
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    // -------------------------------------------------------------------------
    // P/Invoke
    // -------------------------------------------------------------------------
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterClass(string lpClassName, nint hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateWindowEx(
        int     dwExStyle,
        string  lpClassName,
        string  lpWindowName,
        uint    dwStyle,
        int     x,
        int     y,
        int     nWidth,
        int     nHeight,
        nint    hWndParent,
        nint    hMenu,
        nint    hInstance,
        nint    lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint DefWindowProc(nint hwnd, uint msg, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint hwnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWindowLongPtr(nint hwnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint GetWindowLongPtr(nint hwnd, int nIndex);

    // -------------------------------------------------------------------------
    // Message loop
    // -------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        public nint   hwnd;
        public uint   message;
        public nint   wParam;
        public nint   lParam;
        public uint   time;
        public int    ptX;
        public int    ptY;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    internal static extern nint DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    internal static extern void PostQuitMessage(int nExitCode);

    // -------------------------------------------------------------------------
    // WM_ messages (additional)
    // -------------------------------------------------------------------------
    internal const uint WM_GETMINMAXINFO = 0x0024;
    internal const uint WM_EXITSIZEMOVE  = 0x0232;
    internal const uint WM_HOTKEY        = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint hwnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint hwnd, int id);

    // -------------------------------------------------------------------------
    // Z-order diagnostics
    // -------------------------------------------------------------------------

    internal const int GW_HWNDPREV = 3;  // window above in z-order

    [DllImport("user32.dll")]
    internal static extern nint GetWindow(nint hwnd, int uCmd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(nint hwnd, System.Text.StringBuilder text, int maxLength);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(nint hwnd, System.Text.StringBuilder className, int maxLength);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint lpdwProcessId);

    // -------------------------------------------------------------------------
    // GDI вЂ” UpdateLayeredWindow support
    // -------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT_GDI { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SIZE_GDI { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFOHEADER
    {
        public int   biSize;
        public int   biWidth;
        public int   biHeight;   // negative = top-down bitmap (matches D3D texture coords)
        public short biPlanes;
        public short biBitCount;
        public int   biCompression;
        public int   biSizeImage;
        public int   biXPelsPerMeter;
        public int   biYPelsPerMeter;
        public int   biClrUsed;
        public int   biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public int              bmiColors;  // single RGBQUAD entry (unused for 32-bit BI_RGB)
    }

    internal const int  BI_RGB         = 0;
    internal const uint DIB_RGB_COLORS = 0;
    internal const byte AC_SRC_OVER    = 0;
    internal const byte AC_SRC_ALPHA   = 1;
    internal const uint ULW_ALPHA      = 2;

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern nint CreateDIBSection(
        nint hdc, ref BITMAPINFO pbmi, uint iUsage,
        out nint ppvBits, nint hSection, uint dwOffset);

    [DllImport("gdi32.dll")]
    internal static extern nint SelectObject(nint hdc, nint hgdiobj);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(nint hObject);

    /// <summary>
    /// Updates the position, size, shape, content, and translucency of a layered window.
    /// Pass <c>nint.Zero</c> for <paramref name="hdcDst"/> (screen DC) and
    /// <paramref name="pptDst"/> (keep current position).
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UpdateLayeredWindow(
        nint           hwnd,
        nint           hdcDst,    // Zero = screen DC
        nint           pptDst,    // Zero = keep current window position
        ref SIZE_GDI   psize,
        nint           hdcSrc,
        ref POINT_GDI  pptSrc,
        uint           crKey,
        ref BLENDFUNCTION pblend,
        uint           dwFlags);

    // -------------------------------------------------------------------------
    // WinEvent hook
    // -------------------------------------------------------------------------

    /// <summary>Fires when a window's z-order changes (via SetWindowPos etc.).</summary>
    internal const uint EVENT_OBJECT_REORDER = 0x8004;

    /// <summary>idObject value indicating the event is for the window itself (not a child object).</summary>
    internal const int OBJID_WINDOW = 0;

    /// <summary>
    /// WINEVENT_OUTOFCONTEXT: callback runs on the calling thread's message pump,
    /// not injected into the target process. Safe and no DLL injection required.
    /// </summary>
    internal const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void WinEventDelegate(
        nint hWinEventHook, uint eventType, nint hwnd,
        int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    internal static extern nint SetWinEventHook(
        uint eventMin, uint eventMax,
        nint hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess, uint idThread,
        uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(nint hWinEventHook);

    // -------------------------------------------------------------------------
    // SetWindowPos hWndInsertAfter special values
    internal static readonly nint HWND_TOPMOST = new(-1);

    // SetWindowPos flags
    // -------------------------------------------------------------------------
    internal const uint SWP_NOSIZE      = 0x0001;
    internal const uint SWP_NOMOVE      = 0x0002;
    internal const uint SWP_NOZORDER    = 0x0004;
    internal const uint SWP_NOACTIVATE  = 0x0010;
    internal const uint SWP_FRAMECHANGED = 0x0020;

    // -------------------------------------------------------------------------
    // Window geometry
    // -------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(nint hwnd);  // true if the window is minimized

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(nint hwnd);  // true if the window is visible

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hwnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(
        nint   hwnd,
        nint   hwndInsertAfter,
        int    x,
        int    y,
        int    cx,
        int    cy,
        uint   uFlags);
}

