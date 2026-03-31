using System.Runtime.InteropServices;

namespace SimOverlay.Rendering.Win32;

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

    // WS_EX_TOOLWINDOW is intentionally NOT defined here.
    // Its presence would hide overlay windows from OBS's window picker.

    // -------------------------------------------------------------------------
    // SetWindowLong / SetWindowLongPtr indices
    // -------------------------------------------------------------------------
    internal const int GWL_EXSTYLE = -20;

    // -------------------------------------------------------------------------
    // WM_ messages
    // -------------------------------------------------------------------------
    internal const uint WM_DESTROY   = 0x0002;
    internal const uint WM_SIZE      = 0x0005;
    internal const uint WM_MOVE      = 0x0003;
    internal const uint WM_NCHITTEST = 0x0084;

    // -------------------------------------------------------------------------
    // Hit-test return values
    // -------------------------------------------------------------------------
    internal const nint HTTRANSPARENT = -1;
    internal const nint HTCAPTION     = 2;
    internal const nint HTBOTTOMRIGHT = 17;

    // -------------------------------------------------------------------------
    // ShowWindow commands
    // -------------------------------------------------------------------------
    internal const int SW_SHOW = 5;
    internal const int SW_HIDE = 0;

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
    // Window geometry
    // -------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hwnd, out RECT lpRect);
}
