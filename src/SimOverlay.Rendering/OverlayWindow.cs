using System.ComponentModel;
using System.Runtime.InteropServices;
using SimOverlay.Core.Config;
using SimOverlay.Rendering.Win32;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectComposition;
using Vortice.DXGI;
using AlphaMode = Vortice.DXGI.AlphaMode;

namespace SimOverlay.Rendering;

/// <summary>
/// A borderless, always-on-top, click-through Win32 window for rendering overlays.
/// Hosts a DXGI swap chain + Direct2D device context via DirectComposition for
/// genuine per-pixel transparency without a chroma key.
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

    private nint _hInstance;
    private nint _hwnd;
    private bool _disposed;
    private bool _isLocked = true;

    // -------------------------------------------------------------------------
    // Graphics state
    // -------------------------------------------------------------------------

    private IDXGISwapChain1?      _swapChain;
    private ID2D1DeviceContext?   _d2dContext;
    private ID2D1Bitmap1?         _d2dTarget;
    private IDCompositionDevice?  _dcompDevice;
    private IDCompositionTarget?  _dcompTarget;
    private IDCompositionVisual?  _dcompVisual;

    // Tracked so RecoverDevice() can recreate at the current size.
    private int _currentWidth;
    private int _currentHeight;

    // DXGI error codes that indicate the GPU device has been lost.
    private const int DxgiErrorDeviceRemoved = unchecked((int)0x887A0005);
    private const int DxgiErrorDeviceReset   = unchecked((int)0x887A0007);

    /// <summary>
    /// Held by the render thread during each frame (BeginDraw → Present) and by
    /// the UI thread during ResizeSwapChain. Prevents a resize from tearing the
    /// render target out from under an in-progress frame.
    /// </summary>
    internal readonly object RenderLock = new();

    // -------------------------------------------------------------------------
    // Public surface
    // -------------------------------------------------------------------------

    /// <summary>HWND of the overlay window.</summary>
    public nint Handle => _hwnd;

    /// <summary>Display name used as the window title, e.g. "SimOverlay — Relative".</summary>
    public string DisplayName { get; }

    /// <summary>
    /// Direct2D device context for this window. Available after construction.
    /// Subclasses draw into this context inside <see cref="Render"/>.
    /// </summary>
    protected ID2D1DeviceContext D2DContext =>
        _d2dContext ?? throw new InvalidOperationException("Graphics not yet initialized.");

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
        _className = $"SimOverlay_{Guid.NewGuid():N}";
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
            hbrBackground = nint.Zero, // D2D / DComp owns the surface — no GDI background
        };

        if (NativeMethods.RegisterClassEx(ref wcex) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"RegisterClassEx failed for '{DisplayName}'");

        _hwnd = NativeMethods.CreateWindowEx(
            dwExStyle:   NativeMethods.WS_EX_TOPMOST
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
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"CreateWindowEx failed for '{DisplayName}'");
    }

    // -------------------------------------------------------------------------
    // Graphics initialization (D3D11 → DXGI → D2D → DComp)
    // -------------------------------------------------------------------------

    private void InitializeGraphics(int width, int height)
    {
        _currentWidth  = width;
        _currentHeight = height;
        // 1 — D3D11 device (BGRA support required for Direct2D interop)
        D3D11.D3D11CreateDevice(
            adapter:          null,
            driverType:       DriverType.Hardware,
            flags:            DeviceCreationFlags.BgraSupport,
            featureLevels:    [Vortice.Direct3D.FeatureLevel.Level_11_1, Vortice.Direct3D.FeatureLevel.Level_11_0],
            out var d3dDevice,
            out _,
            out _).CheckError();

        using var dxgiDevice  = d3dDevice.QueryInterface<IDXGIDevice1>();
        using var dxgiAdapter = dxgiDevice.GetAdapter();
        using var dxgiFactory = dxgiAdapter.GetParent<IDXGIFactory2>();

        // 2 — DXGI swap chain for DirectComposition
        //     CreateSwapChainForComposition is required when using
        //     WS_EX_NOREDIRECTIONBITMAP + DComp for per-pixel transparency.
        var swapChainDesc = new SwapChainDescription1
        {
            Width             = width,
            Height            = height,
            Format            = Format.B8G8R8A8_UNorm,
            Stereo            = false,
            SampleDescription = new SampleDescription(1, 0),
            BufferUsage       = Usage.RenderTargetOutput,
            BufferCount       = 2,
            Scaling           = Scaling.Stretch,
            SwapEffect        = SwapEffect.FlipSequential,
            AlphaMode         = AlphaMode.Premultiplied,
            Flags             = SwapChainFlags.None,
        };

        _swapChain = dxgiFactory.CreateSwapChainForComposition(dxgiDevice, swapChainDesc);

        // 3 — Direct2D device and device context
        //     Factory is multi-threaded: render thread and UI thread can both use D2D.
        using var d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory1>(FactoryType.MultiThreaded);
        using var d2dDevice  = d2dFactory.CreateDevice(dxgiDevice);
        _d2dContext = d2dDevice.CreateDeviceContext(DeviceContextOptions.None);

        // Bind the D2D context to the swap chain back-buffer.
        BindRenderTarget();

        // 4 — DirectComposition: bind the swap chain to the HWND
        DComp.DCompositionCreateDevice(dxgiDevice, out _dcompDevice).CheckError();
        _dcompDevice!.CreateTargetForHwnd(_hwnd, topmost: true, out _dcompTarget).CheckError();
        _dcompDevice.CreateVisual(out _dcompVisual).CheckError();
        _dcompVisual!.SetContent(_swapChain);
        _dcompTarget!.SetRoot(_dcompVisual);
        _dcompDevice.Commit().CheckError();

        d3dDevice.Dispose();
    }

    private void BindRenderTarget()
    {
        _d2dTarget?.Dispose();
        _d2dTarget = null;

        using var backBuffer = _swapChain!.GetBuffer<IDXGISurface>(0);

        var bitmapProps = new BitmapProperties1
        {
            BitmapOptions = BitmapOptions.Target | BitmapOptions.CannotDraw,
            PixelFormat = new Vortice.DCommon.PixelFormat(
                Vortice.DXGI.Format.B8G8R8A8_UNorm,
                Vortice.DCommon.AlphaMode.Premultiplied),
        };

        _d2dTarget = _d2dContext!.CreateBitmapFromDxgiSurface(backBuffer, bitmapProps);
        _d2dContext.Target = _d2dTarget;
    }

    // -------------------------------------------------------------------------
    // Device lost recovery
    // -------------------------------------------------------------------------

    /// <summary>
    /// Releases all GPU resources and recreates them from scratch.
    /// Called by the render loop in <see cref="BaseOverlay"/> after a
    /// <see cref="DeviceLostException"/>. Must be called on the render thread
    /// (or any thread — <see cref="RenderLock"/> is acquired internally).
    /// </summary>
    public void RecoverDevice()
    {
        lock (RenderLock)
        {
            ReleaseGraphicsResources();
            InitializeGraphics(_currentWidth, _currentHeight);
        }
    }

    private void ReleaseGraphicsResources()
    {
        // Must be called inside RenderLock.
        if (_d2dContext != null)
            _d2dContext.Target = null;
        _d2dTarget?.Dispose();   _d2dTarget   = null;
        _dcompVisual?.Dispose(); _dcompVisual = null;
        _dcompTarget?.Dispose(); _dcompTarget = null;
        _dcompDevice?.Dispose(); _dcompDevice = null;
        _swapChain?.Dispose();   _swapChain   = null;
        _d2dContext?.Dispose();  _d2dContext  = null;
    }

    private static bool IsDeviceLost(int hresult) =>
        hresult == DxgiErrorDeviceRemoved || hresult == DxgiErrorDeviceReset;

    // -------------------------------------------------------------------------
    // Rendering
    // -------------------------------------------------------------------------

    /// <summary>
    /// Clears the surface to fully transparent, calls <see cref="OnRender"/>,
    /// then presents the frame. Called by the render loop in <c>BaseOverlay</c>.
    /// </summary>
    public void Render()
    {
        lock (RenderLock)
        {
            try
            {
                _d2dContext!.BeginDraw();
                _d2dContext.Clear(new Vortice.Mathematics.Color4(0f, 0f, 0f, 0f));

                OnRender(_d2dContext);

                _d2dContext.EndDraw();

                // SyncInterval=0: don't block waiting for vsync. The render loop's
                // Stopwatch already caps to 60 fps, and for a CreateSwapChainForComposition
                // flip chain DWM composites at its own rate regardless.
                var presentResult = _swapChain!.Present(0, PresentFlags.None);
                if (IsDeviceLost(presentResult.Code))
                    throw new DeviceLostException();
                presentResult.CheckError();
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
    protected virtual void OnRender(ID2D1DeviceContext context) { }

    // -------------------------------------------------------------------------
    // Swap chain resize (called from OnSize override)
    // -------------------------------------------------------------------------

    protected void ResizeSwapChain(int width, int height)
    {
        if (_swapChain is null || _d2dContext is null)
            return;

        lock (RenderLock)
        {
            _d2dContext.Target = null;
            _d2dTarget?.Dispose();
            _d2dTarget = null;

            _swapChain.ResizeBuffers(0, width, height, Format.Unknown, SwapChainFlags.None).CheckError();

            _currentWidth  = width;
            _currentHeight = height;

            BindRenderTarget();

            // After ResizeBuffers the DComp visual's cached content extent still
            // reflects the old swap-chain dimensions.  Re-setting the content and
            // committing tells DWM to use the new buffer size for compositing.
            // Without this, pixels outside the original window dimensions are
            // composited at alpha=0 and therefore always click-through
            // (WS_EX_LAYERED alpha hit-testing).
            _dcompVisual?.SetContent(_swapChain);
            _dcompDevice?.Commit();
        }
    }

    // -------------------------------------------------------------------------
    // Window procedure
    // -------------------------------------------------------------------------

    private nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        // Top-level catch: an unhandled exception escaping a WndProc into native
        // code causes silent process termination on .NET. Log and swallow instead.
        try
        {
            switch (msg)
            {
                case NativeMethods.WM_NCHITTEST:
                    return _isLocked ? NativeMethods.HTTRANSPARENT : HandleNcHitTest(lParam);

                case NativeMethods.WM_DESTROY:
                    OnDestroy();
                    NativeMethods.PostQuitMessage(0);
                    return 0;

                case NativeMethods.WM_GETMINMAXINFO:
                    // Prevent the window from being dragged smaller than the resize
                    // grip zone so the grip is always reachable.
                    // MINMAXINFO layout (all ints, 4 bytes each):
                    //   ptReserved (8), ptMaxSize (8), ptMaxPosition (8),
                    //   ptMinTrackSize (8) ← offset 24, ptMaxTrackSize (8)
                    System.Runtime.InteropServices.Marshal.WriteInt32(lParam, 24, ResizeGripSize);
                    System.Runtime.InteropServices.Marshal.WriteInt32(lParam, 28, ResizeGripSize);
                    return 0;

                case NativeMethods.WM_EXITSIZEMOVE:
                    // Windows may reset extended styles during a drag/resize operation.
                    // Re-apply our lock state so hit-testing works correctly afterward.
                    ApplyTransparentStyle(_isLocked);
                    break;

                case NativeMethods.WM_SIZE:
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
            Core.AppLog.Exception($"WndProc exception (msg=0x{msg:X4}) in '{DisplayName}'", ex);
            return 0;
        }
    }

    // Size of the bottom-right resize grip hit zone in pixels.
    // Large enough to be reliably reachable on any window size.
    protected const int ResizeGripSize = 24;

    protected virtual nint HandleNcHitTest(nint lParam)
    {
        var cx = LoWord(lParam);
        var cy = HiWord(lParam);

        NativeMethods.GetWindowRect(_hwnd, out var rect);

        // Bottom-right ResizeGripSize×ResizeGripSize px hit zone → resize grip.
        if (cx >= rect.Right - ResizeGripSize && cy >= rect.Bottom - ResizeGripSize)
            return NativeMethods.HTBOTTOMRIGHT;

        // Everything else is draggable.
        return NativeMethods.HTCAPTION;
    }

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

        // Per MSDN: after SetWindowLongPtr you MUST call SetWindowPos with
        // SWP_FRAMECHANGED for the new extended style to be honoured by the
        // window manager (especially for hit-test / mouse routing changes).
        NativeMethods.SetWindowPos(
            _hwnd,
            nint.Zero,
            0, 0, 0, 0,
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
        }

        if (_hwnd != nint.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = nint.Zero;
        }

        if (_hInstance != nint.Zero)
            NativeMethods.UnregisterClass(_className, _hInstance);
    }

    ~OverlayWindow() => Dispose(false);
}
