using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Core.Events;
using SimOverlay.Rendering;

namespace SimOverlay.App;

/// <summary>
/// Owns all overlay windows. Reads per-overlay config from <see cref="AppConfig"/>,
/// creates windows via <see cref="IOverlayFactory"/>, and manages visibility.
/// Provides <see cref="EnableOverlay"/> / <see cref="DisableOverlay"/> for
/// the tray icon and settings window.
/// </summary>
public sealed class OverlayManager : IDisposable
{
    private readonly ISimDataBus  _bus;
    private readonly AppConfig    _appConfig;
    private readonly ConfigStore  _configStore;

    private bool _editModeActive;

    /// <summary>True when overlays are unlocked for dragging/resizing.</summary>
    public bool EditModeActive => _editModeActive;

    /// <summary>True when stream-mode overrides are active.</summary>
    public bool StreamModeActive => _appConfig.GlobalSettings.StreamModeActive;

    private readonly Dictionary<string, BaseOverlay> _overlays;

    /// <param name="bus">Shared data bus — forwarded to every overlay via the factory.</param>
    /// <param name="appConfig">Loaded config; missing overlay entries get defaults added.</param>
    /// <param name="configStore">Used to persist enable/disable changes immediately.</param>
    /// <param name="factory">Creates overlay instances; carries the registered overlay set.</param>
    public OverlayManager(ISimDataBus bus, AppConfig appConfig, ConfigStore configStore, IOverlayFactory factory)
    {
        _bus         = bus;
        _appConfig   = appConfig;
        _configStore = configStore;

        _overlays = new Dictionary<string, BaseOverlay>();
        foreach (var (id, defaultConfig) in factory.DefaultConfigs)
        {
            var config  = GetOrAddConfig(id, defaultConfig);
            var overlay = factory.Create(config);
            _overlays[id] = overlay;
            ApplyVisibility(overlay, config);
        }
    }

    // -------------------------------------------------------------------------
    // Settings — preview and apply
    // -------------------------------------------------------------------------

    /// <summary>
    /// Pushes a config snapshot to the live overlay for immediate visual preview.
    /// Does <em>not</em> persist to disk. Called on every Settings field blur.
    /// </summary>
    public void PreviewConfig(string overlayId, OverlayConfig config)
    {
        GetOverlay(overlayId)?.UpdateConfig(config);
    }

    /// <summary>
    /// Applies a config to the live overlay (appearance + position + size) and
    /// persists it to disk. Called when the user clicks Apply in Settings.
    /// </summary>
    public void ApplyConfig(string overlayId, OverlayConfig config)
    {
        var index = _appConfig.Overlays.FindIndex(c => c.Id == overlayId);
        if (index < 0) return;

        // Deep-clone breaks shared references with the Settings ViewModel.
        // Replace in the list so overlay and list share the same new instance.
        var cloned = config.DeepClone();
        _appConfig.Overlays[index] = cloned;

        var overlay = GetOverlay(overlayId);
        if (overlay is null) return;

        overlay.UpdateConfig(cloned);
        overlay.SetPosition(cloned.X, cloned.Y);
        overlay.SetSize(cloned.Width, cloned.Height);
        ApplyVisibility(overlay, cloned);

        _configStore.Save(_appConfig);
    }

    // -------------------------------------------------------------------------
    // Edit mode / stream mode
    // -------------------------------------------------------------------------

    /// <summary>
    /// Enables or disables edit mode globally (unlocks all overlays for drag/resize).
    /// Publishes <see cref="EditModeChangedEvent"/> on the data bus.
    /// </summary>
    public void SetEditMode(bool active)
    {
        _editModeActive = active;
        // IsLocked=true means normal (click-through); IsLocked=false means edit mode.
        _bus.Publish(new EditModeChangedEvent(IsLocked: !active));
    }

    /// <summary>
    /// Activates or deactivates stream mode and persists it to config.
    /// Publishes <see cref="StreamModeChangedEvent"/> so overlays invalidate
    /// their resource caches and re-resolve the effective config.
    /// </summary>
    public void SetStreamMode(bool active)
    {
        _appConfig.GlobalSettings.StreamModeActive = active;
        _bus.Publish(new StreamModeChangedEvent(IsActive: active));
        _configStore.Save(_appConfig);
    }

    // -------------------------------------------------------------------------
    // Enable / disable
    // -------------------------------------------------------------------------

    /// <summary>
    /// Shows the overlay and persists <c>Enabled = true</c> in config.
    /// No-op if <paramref name="overlayId"/> is unrecognised.
    /// </summary>
    public void EnableOverlay(string overlayId)
    {
        var config = FindConfig(overlayId);
        if (config is null) return;

        config.Enabled = true;
        GetOverlay(overlayId)?.Show();
        _configStore.Save(_appConfig);
    }

    /// <summary>
    /// Hides the overlay and persists <c>Enabled = false</c> in config.
    /// No-op if <paramref name="overlayId"/> is unrecognised.
    /// </summary>
    public void DisableOverlay(string overlayId)
    {
        var config = FindConfig(overlayId);
        if (config is null) return;

        config.Enabled = false;
        GetOverlay(overlayId)?.Hide();
        _configStore.Save(_appConfig);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private OverlayConfig? FindConfig(string overlayId) =>
        _appConfig.Overlays.FirstOrDefault(c => c.Id == overlayId);

    private BaseOverlay? GetOverlay(string overlayId) =>
        _overlays.TryGetValue(overlayId, out var overlay) ? overlay : null;

    private static void ApplyVisibility(BaseOverlay overlay, OverlayConfig config)
    {
        if (config.Enabled)
            overlay.Show();
        else
            overlay.Hide();
    }

    /// <summary>
    /// Returns the existing config for <paramref name="overlayId"/> from
    /// <see cref="_appConfig"/>, or appends the supplied default and returns it.
    /// </summary>
    private OverlayConfig GetOrAddConfig(string overlayId, OverlayConfig defaultConfig)
    {
        var existing = _appConfig.Overlays.FirstOrDefault(c => c.Id == overlayId);
        if (existing is not null)
            return existing;

        _appConfig.Overlays.Add(defaultConfig);
        return defaultConfig;
    }

    /// <summary>HWNDs of all overlay windows, for use with <see cref="ZOrderHook"/>.</summary>
    public IReadOnlyList<nint> OwnedHandles =>
        _overlays.Values.Select(o => o.Handle).ToList();

    /// <summary>
    /// Re-asserts all overlays to the top of the TOPMOST z-order band.
    /// Called from <see cref="ZOrderHook"/> when another topmost window changes z-order.
    /// </summary>
    public void BringAllToFront()
    {
        foreach (var overlay in _overlays.Values)
            overlay.BringToFront();
    }

    public void Dispose()
    {
        foreach (var overlay in _overlays.Values)
            overlay.Dispose();
    }
}
