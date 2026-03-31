using System.Diagnostics;
using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Core.Events;
using Vortice.Direct2D1;

namespace SimOverlay.Rendering;

/// <summary>
/// Abstract base for all overlay windows. Manages the 60 Hz render loop,
/// <see cref="ISimDataBus"/> subscriptions, and the <see cref="RenderResources"/> cache.
/// </summary>
public abstract class BaseOverlay : OverlayWindow
{
    private const double TargetFrameMs = 1000.0 / 60.0; // ~16.667 ms

    private readonly ISimDataBus _bus;
    private readonly List<Action> _unsubscribeActions = new();

    private Thread? _renderThread;
    private volatile bool _running;

    // -------------------------------------------------------------------------
    // Config and resources
    // -------------------------------------------------------------------------

    private OverlayConfig _config;
    private RenderResources? _resources;

    /// <summary>Current effective overlay configuration.</summary>
    public OverlayConfig Config => _config;

    /// <summary>Cached D2D brushes and DirectWrite text formats for this overlay.</summary>
    protected RenderResources Resources =>
        _resources ?? throw new InvalidOperationException("Resources not yet initialized.");

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    protected BaseOverlay(string displayName, OverlayConfig config, ISimDataBus bus)
        : base(displayName, config)
    {
        _config = config;
        _bus = bus;
        _resources = new RenderResources(D2DContext);

        Subscribe<EditModeChangedEvent>(e => IsLocked = e.IsLocked);

        StartRenderLoop();
    }

    // -------------------------------------------------------------------------
    // Config update — invalidates cached resources
    // -------------------------------------------------------------------------

    /// <summary>
    /// Applies a new config. Cached brushes and text formats are invalidated
    /// and will be lazily recreated on the next render frame.
    /// </summary>
    public void UpdateConfig(OverlayConfig config)
    {
        _config = config;
        _resources?.Invalidate();
    }

    // -------------------------------------------------------------------------
    // ISimDataBus subscription helper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Subscribes to data bus messages of type <typeparamref name="T"/>.
    /// The subscription is automatically removed when this overlay is disposed.
    /// </summary>
    protected void Subscribe<T>(Action<T> handler)
    {
        _bus.Subscribe(handler);
        _unsubscribeActions.Add(() => _bus.Unsubscribe(handler));
    }

    // -------------------------------------------------------------------------
    // Render entry points
    // -------------------------------------------------------------------------

    // Edit-mode border: Windows accent blue, 2 px, drawn on top of overlay content.
    private static readonly (float R, float G, float B) EditBorderColor = (0f, 0.47f, 1f);

    // Size of each resize-grip dot (px) and the step between dot centres.
    private const float GripDotSize = 3f;
    private const float GripDotStep = 6f;

    /// <summary>
    /// Called by the render loop each frame. Forwards to
    /// <see cref="OnRender(ID2D1DeviceContext, OverlayConfig)"/> with the current config,
    /// then overlays the edit-mode border and resize grip when unlocked.
    /// </summary>
    protected sealed override void OnRender(ID2D1DeviceContext context)
    {
        OnRender(context, _config);

        if (!IsLocked)
        {
            var w = (float)_config.Width;
            var h = (float)_config.Height;
            var brush = Resources.GetBrush(EditBorderColor.R, EditBorderColor.G, EditBorderColor.B);

            // 2 px accent-blue border around the overlay.
            context.DrawRectangle(new Vortice.RawRectF(1, 1, w - 1, h - 1), brush, 2f);

            // Three diagonal dots in the bottom-right corner — indicate resize grip.
            // Laid out like a standard size-box: bottom-right, then one step up-left,
            // then two steps up-left.
            DrawGripDot(context, brush, w - 4f,                    h - 4f);
            DrawGripDot(context, brush, w - 4f - GripDotStep,      h - 4f - GripDotStep);
            DrawGripDot(context, brush, w - 4f - GripDotStep * 2f, h - 4f - GripDotStep * 2f);
        }
    }

    private static void DrawGripDot(ID2D1DeviceContext context, ID2D1Brush brush, float cx, float cy)
    {
        var half = GripDotSize / 2f;
        context.FillRectangle(new Vortice.RawRectF(cx - half, cy - half, cx + half, cy + half), brush);
    }

    /// <summary>
    /// Override to issue D2D draw calls for this overlay.
    /// Called at ~60 fps on the render thread; <paramref name="config"/> is a snapshot
    /// consistent for the entire frame.
    /// </summary>
    protected abstract void OnRender(ID2D1DeviceContext context, OverlayConfig config);

    // -------------------------------------------------------------------------
    // Window move / resize — keep config in sync
    // -------------------------------------------------------------------------

    protected override void OnMove(int x, int y)
    {
        // _resources is set after OverlayWindow's constructor (which fires WM_MOVE
        // synchronously during CreateWindowEx). Guard until fully initialized.
        if (_resources is null)
            return;

        _config.X = x;
        _config.Y = y;
    }

    protected override void OnSize(int width, int height)
    {
        // WM_SIZE arrives during CreateWindowEx, before BaseOverlay constructor body
        // has run and assigned _config / _resources.  Guard until fully initialized.
        if (_resources is null || width <= 0 || height <= 0)
            return;

        _config.Width  = width;
        _config.Height = height;

        ResizeSwapChain(width, height);
        _resources.Invalidate();
    }

    // -------------------------------------------------------------------------
    // 60 Hz render loop
    // -------------------------------------------------------------------------

    private void StartRenderLoop()
    {
        _running = true;
        _renderThread = new Thread(RenderLoop)
        {
            IsBackground = true,
            Name = $"Render_{DisplayName}",
        };
        _renderThread.Start();
    }

    // Timestamp of the last render-error log entry — used to rate-limit logging
    // so a persistent D2D error doesn't flood the log at 60 fps.
    private DateTime _lastRenderErrorLog = DateTime.MinValue;

    private void RenderLoop()
    {
        AppLog.Info($"Render loop started for '{DisplayName}'");
        var sw = Stopwatch.StartNew();
        var nextFrameMs = sw.Elapsed.TotalMilliseconds;

        while (_running)
        {
            var now = sw.Elapsed.TotalMilliseconds;

            if (now >= nextFrameMs)
            {
                try
                {
                    Render();
                }
                catch (Exception ex)
                {
                    // Rate-limit to one log entry per 5 seconds so a persistent
                    // D2D error doesn't flood the file. Full recovery in TASK-108.
                    var ts = DateTime.UtcNow;
                    if ((ts - _lastRenderErrorLog).TotalSeconds >= 5)
                    {
                        AppLog.Exception($"Render error in '{DisplayName}'", ex);
                        _lastRenderErrorLog = ts;
                    }
                }

                nextFrameMs += TargetFrameMs;

                // If we fell significantly behind, snap forward rather than
                // spinning to catch up (e.g., after a long GC pause).
                if (sw.Elapsed.TotalMilliseconds > nextFrameMs + TargetFrameMs)
                    nextFrameMs = sw.Elapsed.TotalMilliseconds + TargetFrameMs;
            }
            else
            {
                // Sleep for roughly 1 ms when there is time to spare.
                // This keeps CPU usage low without busy-waiting the full interval.
                var sleepMs = nextFrameMs - now;
                if (sleepMs >= 1.5)
                    Thread.Sleep(1);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Disposal
    // -------------------------------------------------------------------------

    protected override void Dispose(bool disposing)
    {
        // Signal the render thread to stop, then wait for it to exit cleanly.
        AppLog.Info($"Stopping render loop for '{DisplayName}'");
        _running = false;
        _renderThread?.Join(TimeSpan.FromSeconds(2));
        AppLog.Info($"Render loop stopped for '{DisplayName}'");

        if (disposing)
        {
            foreach (var unsubscribe in _unsubscribeActions)
                unsubscribe();
            _unsubscribeActions.Clear();

            _resources?.Dispose();
            _resources = null;
        }

        base.Dispose(disposing);
    }
}
