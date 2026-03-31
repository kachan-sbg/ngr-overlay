using System.ComponentModel;
using System.Runtime.InteropServices;
using SimOverlay.Core.Config;
using SimOverlay.Rendering.Win32;

namespace SimOverlay.Rendering;

/// <summary>
/// A borderless, always-on-top, click-through Win32 window for rendering overlays.
/// Transparent to mouse events when locked; accepts input when unlocked (edit mode).
/// </summary>
public class OverlayWindow : IDisposable
{
    private readonly string _className;

    // Held as a field to prevent the delegate from being garbage-collected
    // while the Win32 window class still references it.
    private readonly NativeMethods.WndProcDelegate _wndProcDelegate;

    private nint _hInstance;
    private nint _hwnd;
    private bool _disposed;
    private bool _isLocked = true;

    /// <summary>HWND of the overlay window. Zero until <see cref="Initialize"/> is called.</summary>
    public nint Handle => _hwnd;

    /// <summary>Display name used as the window title, e.g. "SimOverlay — Relative".</summary>
    public string DisplayName { get; }

    /// <summary>
    /// When <c>true</c> (default): window is click-through (<c>WS_EX_TRANSPARENT</c>).
    /// When <c>false</c>: window accepts mouse input for drag/resize (edit mode).
    /// </summary>
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (_isLocked == value || _hwnd == nint.Zero)
                return;

            _isLocked = value;
            ApplyTransparentStyle(value);
        }
    }

    public OverlayWindow(string displayName, OverlayConfig config)
        : this(displayName, config.X, config.Y, config.Width, config.Height) { }

    public OverlayWindow(string displayName, int x, int y, int width, int height)
    {
        DisplayName = displayName;
        _className = $"SimOverlay_{Guid.NewGuid():N}";
        _wndProcDelegate = WndProc;
        Initialize(x, y, width, height);
    }

    // -------------------------------------------------------------------------
    // Initialization
    // -------------------------------------------------------------------------

    private void Initialize(int x, int y, int width, int height)
    {
        _hInstance = NativeMethods.GetModuleHandle(null);

        var wcex = new NativeMethods.WNDCLASSEX
        {
            cbSize        = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            lpfnWndProc   = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance     = _hInstance,
            lpszClassName = _className,
            hbrBackground = nint.Zero, // no background fill — D2D owns the surface
        };

        if (NativeMethods.RegisterClassEx(ref wcex) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"RegisterClassEx failed for '{DisplayName}'");

        _hwnd = NativeMethods.CreateWindowEx(
            dwExStyle:   NativeMethods.WS_EX_TOPMOST
                       | NativeMethods.WS_EX_LAYERED
                       | NativeMethods.WS_EX_TRANSPARENT
                       | NativeMethods.WS_EX_NOREDIRECTIONBITMAP,
            lpClassName:  _className,
            lpWindowName: DisplayName,
            dwStyle:      NativeMethods.WS_POPUP | NativeMethods.WS_VISIBLE,
            x:            x,
            y:            y,
            nWidth:       width,
            nHeight:      height,
            hWndParent:   nint.Zero,
            hMenu:        nint.Zero,
            hInstance:    _hInstance,
            lpParam:      nint.Zero);

        if (_hwnd == nint.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"CreateWindowEx failed for '{DisplayName}'");
    }

    // -------------------------------------------------------------------------
    // Window procedure
    // -------------------------------------------------------------------------

    private nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case NativeMethods.WM_NCHITTEST:
                return _isLocked ? NativeMethods.HTTRANSPARENT : HandleNcHitTest(lParam);

            case NativeMethods.WM_DESTROY:
                OnDestroy();
                return 0;

            case NativeMethods.WM_SIZE:
                OnSize(LoWord(lParam), HiWord(lParam));
                break;

            case NativeMethods.WM_MOVE:
                OnMove(LoWord(lParam), HiWord(lParam));
                break;
        }

        return NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Override to customize hit-testing in unlocked (edit) mode.
    /// Default implementation returns <c>HTCAPTION</c> everywhere (full-window drag).
    /// </summary>
    protected virtual nint HandleNcHitTest(nint lParam) => NativeMethods.HTCAPTION;

    protected virtual void OnDestroy() { }

    protected virtual void OnSize(int width, int height) { }

    protected virtual void OnMove(int x, int y) { }

    // -------------------------------------------------------------------------
    // Visibility
    // -------------------------------------------------------------------------

    public void Show() => NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOW);

    public void Hide() => NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_HIDE);

    // -------------------------------------------------------------------------
    // Edit-mode style toggle
    // -------------------------------------------------------------------------

    private void ApplyTransparentStyle(bool transparent)
    {
        var current = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();

        nint next = transparent
            ? new nint(current | NativeMethods.WS_EX_TRANSPARENT)
            : new nint(current & ~NativeMethods.WS_EX_TRANSPARENT);

        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, next);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static int LoWord(nint value) => (short)(value.ToInt64() & 0xFFFF);
    private static int HiWord(nint value) => (short)((value.ToInt64() >> 16) & 0xFFFF);

    // -------------------------------------------------------------------------
    // Disposal
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_hwnd != nint.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = nint.Zero;
        }

        if (_hInstance != nint.Zero)
        {
            NativeMethods.UnregisterClass(_className, _hInstance);
        }
    }

    ~OverlayWindow() => Dispose(false);
}
