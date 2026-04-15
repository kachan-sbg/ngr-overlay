using System.ComponentModel;
using System.Runtime.InteropServices;
using NrgOverlay.Core;
using NrgOverlay.Core.Config;
using NrgOverlay.Rendering.Win32;
using Vortice.Direct2D1;

namespace NrgOverlay.Rendering;

/// <summary>
/// A borderless, always-on-top, click-through Win32 window for rendering overlays.
///
/// <para>
/// Rendering pipeline: <c>ID2D1DCRenderTarget</c> (software/CPU D2D) renders directly
/// into a GDI memory DC that has a 32-bit premultiplied-alpha DIB section selected.
/// Each frame the DIB is presented via <c>UpdateLayeredWindow(ULW_ALPHA)</c>.
/// </para>
///
/// <para>
/// Using a DC render target (software path) instead of a GPU-backed D3D11/D2D device
/// context eliminates all GPU interaction from the presentation path.  With the previous
/// approach, iRacing's DXGI flip chain held a GPU context that interfered with the
/// staging-texture readback or DWM composition of the layered window, making overlays
/// invisible while the sim was running.  The software DC render target has no GPU
/// dependency and is immune to that interaction.
/// </para>
///
/// <para>
/// Per-pixel transparency is provided by the premultiplied-alpha DIB bitmap passed to
/// <c>UpdateLayeredWindow(ULW_ALPHA)</c>.  OBS Window Capture (WGC method) captures
/// layered window content correctly with transparency intact.
/// </para>
/// </summary>
public class OverlayWindow : IDisposable
{
    // -------------------------------------------------------------------------
    // Win32 window state
    // -------------------------------------------------------------------------

    private readonly string _className;

    // Held as a field to prevent the delegate from being GC'd while the
    // Win32 window class still references the function pointer.
    private readonly NativeMethods.WndProcDelegate _wndProcDelegate;

    // Counts live OverlayWindow instances so PostQuitMessage is only sent when
    // the last window is destroyed (not on every individual window close).
    private static int _windowCount;

    private nint _hInstance;
    private nint _hwnd;
    private nint _hOwner;   // hidden owner HWND that suppresses the taskbar button
    private bool _disposed;
    private bool _isLocked      = true;
    private bool _shouldBeVisible;

    // -------------------------------------------------------------------------
    // Graphics state
    // -------------------------------------------------------------------------

    // D2D software render target вЂ” renders directly to a GDI DC (no GPU required)
    private ID2D1Factory?        _d2dFactory;
    private ID2D1DCRenderTarget? _dcRenderTarget;

    // GDI objects for UpdateLayeredWindow
    private nint _hdcMemory;   // memory DC
    private nint _hBitmap;     // 32-bit premultiplied-alpha DIB section
    private nint _dibBits;     // unmanaged pointer into the DIB's pixel data (unused directly вЂ” DCRenderTarget writes here)

    // Tracked so RecoverDevice() and resize can recreate at the current dimensions.
    private int _currentWidth;
    private int _currentHeight;

    // D2D error code indicating the render target must be recreated.
    private const int D2DErrorRecreateTarget = unchecked((int)0x8899000C);

    /// <summary>
    /// Held by the render thread during each frame (BeginDraw в†’ UpdateLayeredWindow)
    /// and by the UI thread during ResizeRenderTarget. Prevents a resize from tearing
    /// the render target out from under an in-progress frame.
    /// </summary>
    internal readonly object RenderLock = new();

    // -------------------------------------------------------------------------
    // Public surface
    // -------------------------------------------------------------------------

    /// <summary>HWND of the overlay window.</summary>
    public nint Handle => _hwnd;

    /// <summary>Display name used as the window title, e.g. "NrgOverlay вЂ” Relative".</summary>
    public string DisplayName { get; }

    /// <summary>
    /// D2D render target for this window. Available after construction.
    /// Subclasses draw into this context inside <see cref="Render"/>.
    /// </summary>
    protected ID2D1RenderTarget D2DContext =>
        _dcRenderTarget ?? throw new InvalidOperationException("Graphics not yet initialized.");

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

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    public OverlayWindow(string displayName, OverlayConfig config)
        : this(displayName, config.X, config.Y, config.Width, config.Height) { }

    public OverlayWindow(string displayName, int x, int y, int width, int height)
    {
        DisplayName = displayName;
        _className = $"NrgOverlay_{Guid.NewGuid():N}";
        _wndProcDelegate = WndProc;

        InitializeWindow(x, y, width, height);
        InitializeGraphics(width, height);
    }

    // -------------------------------------------------------------------------
    // Win32 window initialization
    // -------------------------------------------------------------------------

    private void InitializeWindow(int x, int y, int width, int height)
    {
        _hInstance = NativeMethods.GetModuleHandle(null);

        var wcex = new NativeMethods.WNDCLASSEX
        {
            cbSize        = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            lpfnWndProc   = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance     = _hInstance,
            lpszClassName = _className,
            hbrBackground = nint.Zero,
        };

        if (NativeMethods.RegisterClassEx(ref wcex) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"RegisterClassEx failed for '{DisplayName}'");

        // Create a hidden owner window.  Win32 does not add taskbar buttons for owned
        // windows, so this suppresses the overlay's taskbar entry.  WS_EX_TOOLWINDOW
        // hides the owner itself (it's 0Г—0 and invisible anyway).  Crucially the flag
        // is NOT applied to the overlay вЂ” OBS's WGC enumerates all top-level HWNDs
        // returned by EnumWindows, including owned ones, so capture still works.
        _hOwner = NativeMethods.CreateWindowEx(
            dwExStyle:    NativeMethods.WS_EX_TOOLWINDOW,
            lpClassName:  "Static",
            lpWindowName: "",
            dwStyle:      NativeMethods.WS_POPUP,
            x: 0, y: 0, nWidth: 0, nHeight: 0,
            hWndParent:   nint.Zero,
            hMenu:        nint.Zero,
            hInstance:    _hInstance,
            lpParam:      nint.Zero);

        _hwnd = NativeMethods.CreateWindowEx(
            dwExStyle:   NativeMethods.WS_EX_TOPMOST
                       | NativeMethods.WS_EX_LAYERED       // ULW: DWM composites our bitmap above DXGI planes
                       | NativeMethods.WS_EX_TRANSPARENT,  // click-through; overridden by WM_NCHITTEST in edit mode
            // WS_EX_NOREDIRECTIONBITMAP is intentionally NOT used here.
            // It tells DWM "I'm providing content via DComp вЂ” skip normal compositing."
            // With UpdateLayeredWindow we need DWM to composite our bitmap; that flag
            // makes DWM ignore the ULW bitmap entirely, so the window is invisible.
            lpClassName:  _className,
            lpWindowName: DisplayName,
            dwStyle:      NativeMethods.WS_POPUP,
            x:            x,
            y:            y,
            nWidth:       width,
            nHeight:      height,
            hWndParent:   _hOwner,
            hMenu:        nint.Zero,
            hInstance:    _hInstance,
            lpParam:      nint.Zero);

        if (_hwnd == nint.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"CreateWindowEx failed for '{DisplayName}'");

        // Do NOT call SetLayeredWindowAttributes here.
        // SetLayeredWindowAttributes and UpdateLayeredWindow are mutually exclusive:
        // once SetLayeredWindowAttributes is called, UpdateLayeredWindow is silently
        // blocked until the WS_EX_LAYERED style is cleared and re-set.
        // Per-pixel transparency is handled by UpdateLayeredWindow(ULW_ALPHA) + the
        // premultiplied-alpha DIB. Click-through is handled by WS_EX_TRANSPARENT
        // and WM_NCHITTEST в†’ HTTRANSPARENT in locked mode.

        Interlocked.Increment(ref _windowCount);
    }

    // -------------------------------------------------------------------------
    // Graphics initialization (D2D DCRenderTarget в†’ GDI DIB)
    // -------------------------------------------------------------------------

    private void InitializeGraphics(int width, int height)
    {
        _currentWidth  = width;
        _currentHeight = height;

        // Multi-threaded factory: render loop runs on a dedicated thread.
        _d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory>(FactoryType.MultiThreaded);

        // DC render target вЂ” software/CPU path, renders to a GDI DC.
        // Using the software path eliminates any GPU interaction that could interfere
        // with iRacing's DXGI flip chain or DWM's MPO hardware planes.
        var rtProps = new RenderTargetProperties
        {
            Type        = RenderTargetType.Default,
            PixelFormat = new Vortice.DCommon.PixelFormat(
                Vortice.DXGI.Format.B8G8R8A8_UNorm,
                Vortice.DCommon.AlphaMode.Premultiplied),
            DpiX     = 0f,   // 0 = use system DPI
            DpiY     = 0f,
            Usage    = RenderTargetUsage.None,
            MinLevel = Vortice.Direct2D1.FeatureLevel.Default,
        };
        _dcRenderTarget = _d2dFactory.CreateDCRenderTarget(rtProps);

        // GDI memory DC + DIB section for UpdateLayeredWindow.
        CreateDib(width, height);

        AppLog.Info($"Graphics initialized for '{DisplayName}' ({width}x{height}) вЂ” software DCRenderTarget");
    }

    private void CreateDib(int width, int height)
    {
        DestroyDib();

        _hdcMemory = NativeMethods.CreateCompatibleDC(nint.Zero);
        if (_hdcMemory == nint.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"CreateCompatibleDC failed for '{DisplayName}'");

        var bmi = new NativeMethods.BITMAPINFO
        {
            bmiHeader = new NativeMethods.BITMAPINFOHEADER
            {
                biSize        = Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                biWidth       = width,
                biHeight      = -height,   // negative = top-down, standard for premultiplied-alpha layered windows
                biPlanes      = 1,
                biBitCount    = 32,
                biCompression = NativeMethods.BI_RGB,
            }
        };

        _hBitmap = NativeMethods.CreateDIBSection(
            _hdcMemory, ref bmi, NativeMethods.DIB_RGB_COLORS, out _dibBits, nint.Zero, 0);
        if (_hBitmap == nint.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"CreateDIBSection failed for '{DisplayName}'");

        NativeMethods.SelectObject(_hdcMemory, _hBitmap);
    }

    private void DestroyDib()
    {
        if (_hBitmap   != nint.Zero) { NativeMethods.DeleteObject(_hBitmap);  _hBitmap   = nint.Zero; }
        if (_hdcMemory != nint.Zero) { NativeMethods.DeleteDC(_hdcMemory);    _hdcMemory = nint.Zero; }
        _dibBits = nint.Zero;
    }

    // -------------------------------------------------------------------------
    // Device recovery
    // -------------------------------------------------------------------------

    /// <summary>
    /// Releases all graphics resources and recreates them from scratch.
    /// Called by the render loop in <see cref="BaseOverlay"/> after a
    /// <see cref="DeviceLostException"/>. Safe to call from any thread;
    /// <see cref="RenderLock"/> is acquired internally.
    /// </summary>
    public void RecoverDevice()
    {
        lock (RenderLock)
        {
            ReleaseGraphicsResources();
            InitializeGraphics(_currentWidth, _currentHeight);
            OnDeviceRecreated();
        }
    }

    /// <summary>
    /// Called inside <see cref="RecoverDevice"/> while <see cref="RenderLock"/> is held,
    /// immediately after new graphics resources are created.
    /// Override to update context references (e.g. in <see cref="RenderResources"/>).
    /// </summary>
    protected virtual void OnDeviceRecreated() { }

    private void ReleaseGraphicsResources()
    {
        // Must be called inside RenderLock.
        _dcRenderTarget?.Dispose(); _dcRenderTarget = null;
        _d2dFactory?.Dispose();     _d2dFactory     = null;
        DestroyDib();
    }

    private static bool IsDeviceLost(int hresult) =>
        hresult == D2DErrorRecreateTarget;

    // -------------------------------------------------------------------------
    // Rendering
    // -------------------------------------------------------------------------

    /// <summary>
    /// Binds the DC render target to the memory DC, calls <see cref="OnRender"/>,
    /// then presents via <c>UpdateLayeredWindow</c>.
    /// Called by the render loop in <c>BaseOverlay</c>.
    /// </summary>
    public void Render()
    {
        lock (RenderLock)
        {
            try
            {
                // Bind the software render target to our memory DC each frame.
                // This is required before BeginDraw and after any resize.
                var bounds = new Vortice.RawRect(0, 0, _currentWidth, _currentHeight);
                _dcRenderTarget!.BindDC(_hdcMemory, bounds);

                _dcRenderTarget.BeginDraw();
                _dcRenderTarget.Clear(new Vortice.Mathematics.Color4(0f, 0f, 0f, 0f));

                OnRender(_dcRenderTarget);

                _dcRenderTarget.EndDraw();

                // --- Present via UpdateLayeredWindow ---
                var size  = new NativeMethods.SIZE_GDI { cx = _currentWidth,  cy = _currentHeight };
                var ptSrc = new NativeMethods.POINT_GDI { X = 0, Y = 0 };
                var blend = new NativeMethods.BLENDFUNCTION
                {
                    BlendOp             = NativeMethods.AC_SRC_OVER,
                    BlendFlags          = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat         = NativeMethods.AC_SRC_ALPHA,
                };

                var ulwOk = NativeMethods.UpdateLayeredWindow(
                    _hwnd, nint.Zero, nint.Zero,
                    ref size, _hdcMemory, ref ptSrc,
                    0, ref blend, NativeMethods.ULW_ALPHA);

                if (!ulwOk)
                {
                    var err = Marshal.GetLastWin32Error();
                    AppLog.Warn($"UpdateLayeredWindow failed for '{DisplayName}': Win32 error {err}");
                }
                else if (!_ulwOkLogged)
                {
                    _ulwOkLogged = true;
                    AppLog.Info($"UpdateLayeredWindow first success for '{DisplayName}'");
                }
            }
            catch (SharpGen.Runtime.SharpGenException ex) when (IsDeviceLost(ex.HResult))
            {
                throw new DeviceLostException(ex);
            }
        }
    }

    /// <summary>
    /// Override in subclasses to issue D2D draw calls.
    /// Called between <c>BeginDraw</c> and <c>EndDraw</c> on the render thread.
    /// </summary>
    protected virtual void OnRender(ID2D1RenderTarget context) { }

    // -------------------------------------------------------------------------
    // Render target resize (called from OnSize override in BaseOverlay)
    // -------------------------------------------------------------------------

    protected void ResizeRenderTarget(int width, int height)
    {
        if (_dcRenderTarget is null)
            return;

        lock (RenderLock)
        {
            _currentWidth  = width;
            _currentHeight = height;

            // Recreate the DIB at the new size.
            // BindDC is called at the start of each Render() frame, so the new
            // dimensions are automatically picked up on the next draw.
            CreateDib(width, height);
        }
    }

    // -------------------------------------------------------------------------
    // Window procedure
    // -------------------------------------------------------------------------

    private nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        try
        {
            switch (msg)
            {
                case NativeMethods.WM_NCHITTEST:
                    return _isLocked ? NativeMethods.HTTRANSPARENT : HandleNcHitTest(lParam);

                case NativeMethods.WM_DESTROY:
                    OnDestroy();
                    if (Interlocked.Decrement(ref _windowCount) == 0)
                        NativeMethods.PostQuitMessage(0);
                    return 0;

                case NativeMethods.WM_GETMINMAXINFO:
                    Marshal.WriteInt32(lParam, 24, ResizeGripSize);
                    Marshal.WriteInt32(lParam, 28, ResizeGripSize);
                    return 0;

                case NativeMethods.WM_SYSCOMMAND:
                    if ((wParam & 0xFFF0) == NativeMethods.SC_MINIMIZE)
                    {
                        AppLog.Info($"WM_SYSCOMMAND SC_MINIMIZE swallowed for '{DisplayName}'");
                        return 0;
                    }
                    AppLog.Info($"WM_SYSCOMMAND wParam=0x{wParam:X} for '{DisplayName}'");
                    break;

                case NativeMethods.WM_EXITSIZEMOVE:
                    ApplyTransparentStyle(_isLocked);
                    break;

                case NativeMethods.WM_SIZE:
                    if (wParam == NativeMethods.SIZE_MINIMIZED || wParam == 0)
                        AppLog.Info($"WM_SIZE '{DisplayName}': wParam={wParam} ({LoWord(lParam)}x{HiWord(lParam)})");
                    if (wParam == NativeMethods.SIZE_MINIMIZED && _shouldBeVisible)
                    {
                        AppLog.Info($"WM_SIZE SIZE_MINIMIZED вЂ” restoring '{DisplayName}'");
                        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_RESTORE);
                        return 0;
                    }
                    OnSize(LoWord(lParam), HiWord(lParam));
                    break;

                case NativeMethods.WM_MOVE:
                    OnMove(LoWord(lParam), HiWord(lParam));
                    break;
            }

            return NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);
        }
        catch (Exception ex)
        {
            AppLog.Exception($"WndProc exception (msg=0x{msg:X4}) in '{DisplayName}'", ex);
            return 0;
        }
    }

    protected const int ResizeGripSize = 24;

    protected virtual nint HandleNcHitTest(nint lParam)
    {
        var cx = LoWord(lParam);
        var cy = HiWord(lParam);
        NativeMethods.GetWindowRect(_hwnd, out var rect);
        if (cx >= rect.Right - ResizeGripSize && cy >= rect.Bottom - ResizeGripSize)
            return NativeMethods.HTBOTTOMRIGHT;
        return NativeMethods.HTCAPTION;
    }

    protected virtual void OnDestroy() { }
    protected virtual void OnSize(int width, int height) { }
    protected virtual void OnMove(int x, int y) { }

    // -------------------------------------------------------------------------
    // Visibility
    // -------------------------------------------------------------------------

    public void Show()
    {
        AppLog.Info($"Show '{DisplayName}'");
        _shouldBeVisible = true;
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOW);
    }

    public void Hide()
    {
        AppLog.Info($"Hide '{DisplayName}'");
        _shouldBeVisible = false;
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_HIDE);
    }

    /// <summary>
    /// Moves the overlay to <paramref name="x"/>, <paramref name="y"/> (screen coords).
    /// Triggers <c>WM_MOVE</c> в†’ <see cref="OnMove"/> в†’ config update + debounced save.
    /// Safe to call from any thread.
    /// </summary>
    public void SetPosition(int x, int y) =>
        NativeMethods.SetWindowPos(
            _hwnd, nint.Zero, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

    /// <summary>
    /// Resizes the overlay to <paramref name="width"/> Г— <paramref name="height"/>.
    /// Triggers <c>WM_SIZE</c> в†’ <see cref="OnSize"/> в†’ render target resize + debounced save.
    /// Safe to call from any thread.
    /// </summary>
    public void SetSize(int width, int height) =>
        NativeMethods.SetWindowPos(
            _hwnd, nint.Zero, 0, 0, width, height,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

    // Log the first successful UpdateLayeredWindow call per window.
    private bool _ulwOkLogged;

    // Throttle BringToFront diagnostic logs to once per 5 seconds.
    private DateTime _lastBtfLog = DateTime.MinValue;

    /// <summary>
    /// Re-asserts this window's position at the top of the TOPMOST z-order band.
    /// Safe to call from any thread.
    /// </summary>
    public void BringToFront()
    {
        if (_hwnd == nint.Zero || !_shouldBeVisible) return;

        var iconic  = NativeMethods.IsIconic(_hwnd);
        var visible = NativeMethods.IsWindowVisible(_hwnd);

        if (iconic)
            NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_RESTORE);
        else if (!visible)
        {
            AppLog.Info($"BringToFront '{DisplayName}': window not visible вЂ” calling SW_SHOW");
            NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOW);
        }

        var swpOk = NativeMethods.SetWindowPos(
            _hwnd,
            NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

        // Log every 5 s: SetWindowPos result + what's immediately above us in z-order.
        var now = DateTime.UtcNow;
        if ((now - _lastBtfLog).TotalSeconds >= 5)
        {
            _lastBtfLog = now;
            NativeMethods.GetWindowRect(_hwnd, out var r);
            var swpErr = swpOk ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();

            // Walk up z-order to find what window is directly above ours.
            var above = NativeMethods.GetWindow(_hwnd, NativeMethods.GW_HWNDPREV);
            string aboveInfo = "none";
            if (above != nint.Zero)
            {
                var sb  = new System.Text.StringBuilder(256);
                var cls = new System.Text.StringBuilder(256);
                NativeMethods.GetWindowText(above, sb, sb.Capacity);
                NativeMethods.GetClassName(above, cls, cls.Capacity);
                NativeMethods.GetWindowThreadProcessId(above, out var pid);
                NativeMethods.GetWindowRect(above, out var ar);
                var exStyle = NativeMethods.GetWindowLongPtr(above, NativeMethods.GWL_EXSTYLE).ToInt64();
                var aboveTopmost = (exStyle & NativeMethods.WS_EX_TOPMOST) != 0;
                aboveInfo = $"'{sb}' class='{cls}' pid={pid} topmost={aboveTopmost} hwnd=0x{above:X} rect=({ar.Left},{ar.Top},{ar.Right},{ar.Bottom})";
            }

            AppLog.Info(
                $"BringToFront '{DisplayName}': iconic={iconic} visible={visible} " +
                $"rect=({r.Left},{r.Top},{r.Right},{r.Bottom}) " +
                $"swpOk={swpOk} swpErr={swpErr} | above={aboveInfo}");
        }
    }

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

        NativeMethods.SetWindowPos(
            _hwnd, nint.Zero, 0, 0, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE |
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE |
            NativeMethods.SWP_FRAMECHANGED);
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

        if (disposing)
        {
            lock (RenderLock)
                ReleaseGraphicsResources();

            if (_hwnd != nint.Zero)
            {
                NativeMethods.DestroyWindow(_hwnd);
                _hwnd = nint.Zero;
            }

            // Destroy the hidden owner after the overlay so WM_DESTROY fires on
            // the overlay window (not cascade-destroyed by the owner).
            if (_hOwner != nint.Zero)
            {
                NativeMethods.DestroyWindow(_hOwner);
                _hOwner = nint.Zero;
            }

            if (_hInstance != nint.Zero)
                NativeMethods.UnregisterClass(_className, _hInstance);
        }
    }

    ~OverlayWindow() => Dispose(false);
}

