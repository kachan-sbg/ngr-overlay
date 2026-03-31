using System.Diagnostics;
using SimOverlay.Core;
using SimOverlay.Core.Config;
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

    /// <summary>
    /// Called by the render loop each frame. Forwards to
    /// <see cref="OnRender(ID2D1DeviceContext, OverlayConfig)"/> with the current config.
    /// </summary>
    protected sealed override void OnRender(ID2D1DeviceContext context) =>
        OnRender(context, _config);

    /// <summary>
    /// Override to issue D2D draw calls for this overlay.
    /// Called at ~60 fps on the render thread; <paramref name="config"/> is a snapshot
    /// consistent for the entire frame.
    /// </summary>
    protected abstract void OnRender(ID2D1DeviceContext context, OverlayConfig config);

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

    private void RenderLoop()
    {
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
                catch (Exception)
                {
                    // Swallow individual frame errors.
                    // DXGI device-lost is handled separately in TASK-108.
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
        _running = false;
        _renderThread?.Join(TimeSpan.FromSeconds(2));

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
