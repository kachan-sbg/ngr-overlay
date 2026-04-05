using System.Runtime.InteropServices;

namespace SimOverlay.App;

/// <summary>
/// Ensures only one instance of SimOverlay runs at a time.
/// <para>
/// First instance: acquires <c>Global\SimOverlay_SingleInstance</c> mutex and creates
/// a hidden message-only HWND (class <c>SimOverlay_Cmd</c>) that listens for WM_APP.
/// </para>
/// <para>
/// Second instance: detects the mutex is held, finds the hidden HWND via
/// <see cref="FindWindow"/>, posts WM_APP to signal "show Settings", then exits.
/// </para>
/// Must be created on the STA (UI) thread so the hidden HWND is pumped by the same
/// Win32 message loop that runs <see cref="SimOverlay.Rendering.MessagePump"/>.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName    = @"Global\SimOverlay_SingleInstance";
    private const string WndClassName = "SimOverlay_Cmd";
    private const uint   WM_APP       = 0x8000;
    private static readonly nint HWND_MESSAGE = new(-3);

    private Mutex?         _mutex;
    private readonly nint  _hwnd;
    private readonly WndProcDelegate? _wndProcDelegate; // field keeps delegate alive (prevents GC)

    /// <summary>True when another instance was already running; this instance should exit.</summary>
    public bool IsAlreadyRunning { get; }

    /// <summary>
    /// Fired on the UI thread when a second instance signals this one to open Settings.
    /// Set this after creating the guard; it is safe to set at any time before the first signal arrives.
    /// </summary>
    public Action? OpenSettingsRequested { get; set; }

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            IsAlreadyRunning = true;
            _mutex.Dispose();
            _mutex = null;

            // Signal the existing instance to show its Settings window, then let caller exit.
            var existingHwnd = FindWindow(WndClassName, null);
            if (existingHwnd != nint.Zero)
            {
                AllowSetForegroundWindow(ASFW_ANY);
                PostMessage(existingHwnd, WM_APP, nint.Zero, nint.Zero);
            }
            return;
        }

        // First instance — register a hidden message-only window to receive inter-process signals.
        _wndProcDelegate = WndProc;
        var hInst = GetModuleHandle(null);

        var wc = new WNDCLASSEX
        {
            cbSize        = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc   = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance     = hInst,
            lpszClassName = WndClassName,
        };

        RegisterClassEx(ref wc);

        _hwnd = CreateWindowEx(
            dwExStyle:    0,
            lpClassName:  WndClassName,
            lpWindowName: "",
            dwStyle:      0,
            x: 0, y: 0, nWidth: 0, nHeight: 0,
            hWndParent:   HWND_MESSAGE,
            hMenu:        nint.Zero,
            hInstance:    hInst,
            lpParam:      nint.Zero);
    }

    private nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_APP)
        {
            OpenSettingsRequested?.Invoke();
            return nint.Zero;
        }
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hwnd != nint.Zero)
        {
            DestroyWindow(_hwnd);
            UnregisterClass(WndClassName, GetModuleHandle(null));
        }

        if (_mutex is not null)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { /* already released */ }
            _mutex.Dispose();
            _mutex = null;
        }
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint    cbSize;
        public uint    style;
        public nint    lpfnWndProc;
        public int     cbClsExtra;
        public int     cbWndExtra;
        public nint    hInstance;
        public nint    hIcon;
        public nint    hCursor;
        public nint    hbrBackground;
        public string? lpszMenuName;
        public string  lpszClassName;
        public nint    hIconSm;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClass(string lpClassName, nint hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(
        int dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint hwnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint hwnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(nint hWnd, uint Msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint dwProcessId);

    private const uint ASFW_ANY = 0xFFFF_FFFF;
}
