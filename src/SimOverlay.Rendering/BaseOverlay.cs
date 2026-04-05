using System.Diagnostics;
using System.Threading;
using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Core.Events;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

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

    // Optional: when provided, position/size changes are persisted via a 500 ms debounce.
    private readonly ConfigStore? _configStore;
    private readonly AppConfig?   _appConfig;
    private Timer?  _saveTimer;
    private const int SaveDebounceMs = 500;

    private Thread? _renderThread;
    private volatile bool _running;

    // -------------------------------------------------------------------------
    // Config and resources
    // -------------------------------------------------------------------------

    // _config holds the *backing* (non-resolved) OverlayConfig that maps to the
    // entry in AppConfig.Overlays.  OnRender() resolves it per-frame so stream
    // mode changes are always reflected without a restart.
    private OverlayConfig _config;

    // Set by UpdateConfig(); the render thread picks it up and calls Invalidate()
    // before the next draw to avoid disposing D2D objects from the wrong thread.
    private volatile bool _pendingInvalidate;

    private RenderResources? _resources;

    /// <summary>Current backing overlay configuration (not stream-mode resolved).</summary>
    public OverlayConfig Config => _config;

    /// <summary>Cached D2D brushes and DirectWrite text formats for this overlay.</summary>
    protected RenderResources Resources =>
        _resources ?? throw new InvalidOperationException("Resources not yet initialized.");

    // -------------------------------------------------------------------------
    // Sim state (TASK-304)
    // -------------------------------------------------------------------------

    // Stored as int so we can mark the field volatile (enums are not permitted).
    private volatile int _simStateRaw = (int)SimState.Disconnected;

    /// <summary>Last known sim state; checked each frame to show placeholder text.</summary>
    protected SimState SimState => (SimState)_simStateRaw;

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    /// <param name="displayName">Win32 window title.</param>
    /// <param name="config">
    ///   The <em>backing</em> (non-resolved) <see cref="OverlayConfig"/> entry from
    ///   <see cref="AppConfig.Overlays"/>.  The window is positioned/sized using the
    ///   stream-mode-resolved dimensions so the correct profile is used at startup.
    /// </param>
    /// <param name="bus">Shared data bus.</param>
    /// <param name="configStore">When provided, move/resize events are persisted.</param>
    /// <param name="appConfig">Required when <paramref name="configStore"/> is supplied.</param>
    protected BaseOverlay(
        string displayName,
        OverlayConfig config,
        ISimDataBus bus,
        ConfigStore? configStore = null,
        AppConfig?   appConfig   = null)
        : base(displayName,
               appConfig is not null
                   ? config.Resolve(appConfig.GlobalSettings.StreamModeActive)
                   : config)
    {
        _config      = config;
        _bus         = bus;
        _configStore = configStore;
        _appConfig   = appConfig;
        _resources   = new RenderResources(D2DContext);

        if (_configStore is not null && _appConfig is not null)
            _saveTimer = new Timer(_ => _configStore.Save(_appConfig),
                state: null, Timeout.Infinite, Timeout.Infinite);

        Subscribe<EditModeChangedEvent>(e => IsLocked = e.IsLocked);
        Subscribe<SimStateChangedEvent>(e =>
        {
            AppLog.Info($"SimStateChangedEvent → {e.State} (overlay='{DisplayName}')");
            _simStateRaw = (int)e.State;
            // Re-assert our topmost z-order position whenever the sim state changes.
            // iRacing (and most sims) call SetWindowPos(HWND_TOPMOST) on startup, which
            // pushes our windows beneath theirs even though both are WS_EX_TOPMOST.
            // Calling BringToFront() here restores the correct stacking order.
            // Also called on disconnect so overlays re-appear after the sim closes.
            BringToFront();
        });
        Subscribe<StreamModeChangedEvent>(_ => _pendingInvalidate = true);

        StartRenderLoop();
    }

    // -------------------------------------------------------------------------
    // Config update — deferred invalidation (TASK-303)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Applies a new backing config.  Resource invalidation is deferred to the
    /// next render tick to avoid disposing D2D objects from a non-render thread.
    /// </summary>
    public void UpdateConfig(OverlayConfig config)
    {
        _config          = config;   // atomic reference write on 64-bit
        _pendingInvalidate = true;   // render thread will call Invalidate() next frame
    }

    /// <summary>
    /// Called after <see cref="OverlayWindow.RecoverDevice"/> when triggering
    /// recovery from outside the render loop (e.g. a dev test shortcut).
    /// <see cref="OnDeviceRecreated"/> already ran inside RecoverDevice under
    /// the lock, so this just fires the overlay-level recovered callback.
    /// </summary>
    public void InvalidateResources() => OnDeviceRecovered();

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
    /// Called by the render loop each frame. Applies any pending config
    /// invalidation, resolves the effective config for the active mode,
    /// handles sim-state placeholder rendering, then delegates to
    /// <see cref="OnRender(ID2D1RenderTarget, OverlayConfig)"/> and paints
    /// the edit-mode border when unlocked.
    /// </summary>
    protected sealed override void OnRender(ID2D1RenderTarget context)
    {
        // --- TASK-303: deferred resource invalidation (render thread only) ---
        if (_pendingInvalidate)
        {
            _resources?.Invalidate();
            _pendingInvalidate = false;
        }

        // Resolve stream-mode config per frame so toggling stream mode takes
        // effect immediately without requiring a config push.
        var streamModeActive = _appConfig?.GlobalSettings.StreamModeActive ?? false;
        var effectiveConfig  = _config.Resolve(streamModeActive);

        var w = (float)effectiveConfig.Width;
        var h = (float)effectiveConfig.Height;

        // Always draw the background so the overlay is visible even when
        // the concrete OnRender() is a stub or draws nothing (Phase 3).
        var bgBrush = Resources.GetBrush(effectiveConfig.BackgroundColor);
        context.FillRectangle(new Vortice.RawRectF(0, 0, w, h), bgBrush);

        // --- TASK-304: sim state placeholder ---
        var simState = (SimState)_simStateRaw;
        if (simState != Core.SimState.InSession)
        {
            RenderSimStatePlaceholder(context, effectiveConfig, simState, w, h);
        }
        else
        {
            OnRender(context, effectiveConfig);
        }

        // Edit-mode border is drawn on top regardless of sim state.
        if (!IsLocked)
            DrawEditDecoration(context, w, h);
    }

    // --- TASK-304 placeholder renderer ---
    private void RenderSimStatePlaceholder(
        ID2D1RenderTarget context,
        OverlayConfig config,
        SimState state,
        float w,
        float h)
    {
        // Dim background so the user can see overlay bounds.
        var bgBrush = Resources.GetBrush(config.BackgroundColor);
        context.FillRectangle(new Vortice.RawRectF(0, 0, w, h), bgBrush);

        var text = state == Core.SimState.Disconnected
            ? "Sim not detected"
            : "Waiting for session\u2026";

        var textBrush  = Resources.GetBrush(1f, 1f, 1f, 0.55f);
        var textFormat = Resources.GetTextFormat("Segoe UI", 12f);

        // Centre the text in the overlay.
        using var layout = Resources.WriteFactory.CreateTextLayout(
            text, textFormat, w - 20f, h - 10f);
        layout.TextAlignment      = TextAlignment.Center;
        layout.ParagraphAlignment = ParagraphAlignment.Center;

        context.DrawTextLayout(new System.Numerics.Vector2(10f, 5f), layout, textBrush);
    }

    private void DrawEditDecoration(ID2D1RenderTarget context, float w, float h)
    {
        var brush = Resources.GetBrush(EditBorderColor.R, EditBorderColor.G, EditBorderColor.B);

        // 2 px accent-blue border around the overlay.
        context.DrawRectangle(new Vortice.RawRectF(1, 1, w - 1, h - 1), brush, 2f);

        // Three diagonal dots in the bottom-right corner — indicate resize grip.
        DrawGripDot(context, brush, w - 4f,                    h - 4f);
        DrawGripDot(context, brush, w - 4f - GripDotStep,      h - 4f - GripDotStep);
        DrawGripDot(context, brush, w - 4f - GripDotStep * 2f, h - 4f - GripDotStep * 2f);
    }

    private static void DrawGripDot(ID2D1RenderTarget context, ID2D1Brush brush, float cx, float cy)
    {
        var half = GripDotSize / 2f;
        context.FillRectangle(new Vortice.RawRectF(cx - half, cy - half, cx + half, cy + half), brush);
    }

    /// <summary>
    /// Override to issue D2D draw calls for this overlay.
    /// Called at ~60 fps on the render thread; <paramref name="config"/> is the
    /// stream-mode-resolved snapshot consistent for the entire frame.
    /// </summary>
    protected abstract void OnRender(ID2D1RenderTarget context, OverlayConfig config);

    /// <summary>
    /// Called inside <see cref="OverlayWindow.RecoverDevice"/> while RenderLock is held,
    /// after new D2D resources are created. Updates <see cref="RenderResources"/> with
    /// the new context so the next frame renders correctly.
    /// </summary>
    protected override void OnDeviceRecreated()
    {
        _resources?.UpdateContext(D2DContext);
    }

    /// <summary>
    /// Called on the render thread immediately after a successful device recovery.
    /// Override to reset any overlay-level state that depended on GPU resources.
    /// </summary>
    protected virtual void OnDeviceRecovered() { }

    // -------------------------------------------------------------------------
    // Window move / resize — keep config in sync (TASK-302)
    // -------------------------------------------------------------------------

    protected override void OnMove(int x, int y)
    {
        // _resources is set after OverlayWindow's constructor (which fires WM_MOVE
        // synchronously during CreateWindowEx). Guard until fully initialized.
        if (_resources is null)
            return;

        // Position is always written to the base (backing) config regardless of
        // stream mode — position is never part of the stream override profile.
        lock (RenderLock)
        {
            _config.X = x;
            _config.Y = y;
        }

        ScheduleSave();
    }

    protected override void OnSize(int width, int height)
    {
        if (_resources is null || width <= 0 || height <= 0)
            return;

        // TASK-302: size is written to the stream override when stream mode is
        // active and the override is enabled; otherwise to the base config.
        lock (RenderLock)
        {
            var streamModeActive = _appConfig?.GlobalSettings.StreamModeActive ?? false;
            if (streamModeActive && _config.StreamOverride is { Enabled: true })
            {
                _config.StreamOverride.Width  = width;
                _config.StreamOverride.Height = height;
            }
            else
            {
                _config.Width  = width;
                _config.Height = height;
            }
        }

        ResizeRenderTarget(width, height);
        _resources.Invalidate();
        ScheduleSave();
    }

    private void ScheduleSave()
    {
        // Restart the 500 ms countdown; the timer fires once and saves.
        _saveTimer?.Change(SaveDebounceMs, Timeout.Infinite);
    }

    // -------------------------------------------------------------------------
    // 60 Hz render loop
    // -------------------------------------------------------------------------

    private void StartRenderLoop()
    {
        _running      = true;
        _renderThread = new Thread(RenderLoop)
        {
            IsBackground = true,
            Name         = $"Render_{DisplayName}",
        };
        _renderThread.Start();
    }

    private DateTime _lastRenderErrorLog = DateTime.MinValue;

    private void RenderLoop()
    {
        AppLog.Info($"Render loop started for '{DisplayName}'");
        var sw          = Stopwatch.StartNew();
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
                catch (DeviceLostException)
                {
                    AppLog.Warn($"Device lost in '{DisplayName}' — recovering");
                    try
                    {
                        RecoverDevice();
                        OnDeviceRecovered();
                        AppLog.Info($"Device recovered for '{DisplayName}'");
                    }
                    catch (Exception recoveryEx)
                    {
                        AppLog.Exception($"Device recovery failed for '{DisplayName}'", recoveryEx);
                        Thread.Sleep(1000);
                    }
                }
                catch (Exception ex)
                {
                    var ts = DateTime.UtcNow;
                    if ((ts - _lastRenderErrorLog).TotalSeconds >= 5)
                    {
                        AppLog.Exception($"Render error in '{DisplayName}'", ex);
                        _lastRenderErrorLog = ts;
                    }
                }

                nextFrameMs += TargetFrameMs;

                if (sw.Elapsed.TotalMilliseconds > nextFrameMs + TargetFrameMs)
                    nextFrameMs = sw.Elapsed.TotalMilliseconds + TargetFrameMs;
            }
            else
            {
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
        AppLog.Info($"Stopping render loop for '{DisplayName}'");
        _running = false;
        _renderThread?.Join(TimeSpan.FromSeconds(2));
        AppLog.Info($"Render loop stopped for '{DisplayName}'");

        if (disposing)
        {
            foreach (var unsubscribe in _unsubscribeActions)
                unsubscribe();
            _unsubscribeActions.Clear();

            _saveTimer?.Dispose();
            _saveTimer = null;

            _resources?.Dispose();
            _resources = null;
        }

        base.Dispose(disposing);
    }
}
