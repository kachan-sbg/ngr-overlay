using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Rendering;
using Vortice.Direct2D1;

namespace SimOverlay.Overlays;

/// <summary>
/// Session info panel — track name, session type, time remaining, temperatures,
/// current/best/delta lap times, and game clock.
/// Phase 4 will implement full rendering; this stub satisfies Phase 3 wiring.
/// </summary>
public sealed class SessionInfoOverlay : BaseOverlay
{
    public const string OverlayId   = "SessionInfo";
    public const string WindowTitle = "SimOverlay \u2014 Session Info";

    public static OverlayConfig DefaultConfig => new()
    {
        Id              = OverlayId,
        Enabled         = true,
        X               = 100,
        Y               = 600,
        Width           = 400,
        Height          = 160,
        Opacity         = 0.85f,
        BackgroundColor = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.75f },
        FontSize        = 13f,
        ShowWeather     = true,
        ShowDelta       = true,
        ShowGameTime    = true,
    };

    public SessionInfoOverlay(
        ISimDataBus bus,
        OverlayConfig config,
        ConfigStore configStore,
        AppConfig appConfig)
        : base(WindowTitle, config, bus, configStore, appConfig)
    {
    }

    /// <inheritdoc/>
    protected override void OnRender(ID2D1RenderTarget context, OverlayConfig config)
    {
        // Phase 4: draw session info fields.
    }
}
