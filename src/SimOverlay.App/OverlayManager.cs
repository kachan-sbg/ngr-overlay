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
        var existing = FindConfig(overlayId);
        if (existing is null) return;

        // Copy all fields from the incoming config into the live AppConfig entry
        // so the reference held by the overlay and the config file stay in sync.
        CopyConfig(config, existing);

        var overlay = GetOverlay(overlayId);
        if (overlay is null) return;

        overlay.UpdateConfig(existing);
        overlay.SetPosition(existing.X, existing.Y);
        overlay.SetSize(existing.Width, existing.Height);
        ApplyVisibility(overlay, existing);

        _configStore.Save(_appConfig);
    }

    private static void CopyConfig(OverlayConfig src, OverlayConfig dst)
    {
        dst.Enabled              = src.Enabled;
        dst.X                    = src.X;
        dst.Y                    = src.Y;
        dst.Width                = src.Width;
        dst.Height               = src.Height;
        dst.Opacity              = src.Opacity;
        dst.BackgroundColor      = src.BackgroundColor;
        dst.TextColor            = src.TextColor;
        dst.FontSize             = src.FontSize;
        dst.ShowIRating          = src.ShowIRating;
        dst.ShowLicense          = src.ShowLicense;
        dst.MaxDriversShown      = src.MaxDriversShown;
        dst.PlayerHighlightColor = src.PlayerHighlightColor;
        dst.ShowWeather          = src.ShowWeather;
        dst.ShowDelta            = src.ShowDelta;
        dst.ShowGameTime         = src.ShowGameTime;
        dst.Use12HourClock       = src.Use12HourClock;
        dst.TemperatureUnit      = src.TemperatureUnit;
        dst.DeltaBarMaxSeconds   = src.DeltaBarMaxSeconds;
        dst.FasterColor          = src.FasterColor;
        dst.SlowerColor          = src.SlowerColor;
        dst.ShowTrendArrow       = src.ShowTrendArrow;
        dst.ShowDeltaText        = src.ShowDeltaText;
        dst.StreamOverride       = src.StreamOverride;
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
