using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Overlays;
using SimOverlay.Rendering;

namespace SimOverlay.App;

/// <summary>
/// Owns the three MVP overlay windows. Reads per-overlay config from
/// <see cref="AppConfig"/>, creates windows, and applies initial visibility.
/// Provides <see cref="EnableOverlay"/> / <see cref="DisableOverlay"/> for
/// the tray icon and settings window.
/// </summary>
public sealed class OverlayManager : IDisposable
{
    private readonly AppConfig    _appConfig;
    private readonly ConfigStore  _configStore;

    private readonly RelativeOverlay    _relative;
    private readonly SessionInfoOverlay _sessionInfo;
    private readonly DeltaBarOverlay    _deltaBar;

    /// <param name="bus">Shared data bus — forwarded to every overlay.</param>
    /// <param name="appConfig">Loaded config; missing overlay entries get defaults added.</param>
    /// <param name="configStore">Used to persist enable/disable changes immediately.</param>
    public OverlayManager(ISimDataBus bus, AppConfig appConfig, ConfigStore configStore)
    {
        _appConfig   = appConfig;
        _configStore = configStore;

        var relConfig     = GetOrAddConfig(RelativeOverlay.OverlayId,    RelativeOverlay.DefaultConfig);
        var sessionConfig = GetOrAddConfig(SessionInfoOverlay.OverlayId, SessionInfoOverlay.DefaultConfig);
        var deltaConfig   = GetOrAddConfig(DeltaBarOverlay.OverlayId,    DeltaBarOverlay.DefaultConfig);

        _relative    = new RelativeOverlay(bus, relConfig,     configStore, appConfig);
        _sessionInfo = new SessionInfoOverlay(bus, sessionConfig, configStore, appConfig);
        _deltaBar    = new DeltaBarOverlay(bus, deltaConfig,   configStore, appConfig);

        ApplyVisibility(_relative,    relConfig);
        ApplyVisibility(_sessionInfo, sessionConfig);
        ApplyVisibility(_deltaBar,    deltaConfig);
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

    private BaseOverlay? GetOverlay(string overlayId) => overlayId switch
    {
        RelativeOverlay.OverlayId    => _relative,
        SessionInfoOverlay.OverlayId => _sessionInfo,
        DeltaBarOverlay.OverlayId    => _deltaBar,
        _                            => null,
    };

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
        [_relative.Handle, _sessionInfo.Handle, _deltaBar.Handle];

    /// <summary>
    /// Re-asserts all overlays to the top of the TOPMOST z-order band.
    /// Called from <see cref="ZOrderHook"/> when another topmost window changes z-order.
    /// </summary>
    public void BringAllToFront()
    {
        _relative.BringToFront();
        _sessionInfo.BringToFront();
        _deltaBar.BringToFront();
    }

    public void Dispose()
    {
        _relative.Dispose();
        _sessionInfo.Dispose();
        _deltaBar.Dispose();
    }
}
