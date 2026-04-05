using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Rendering;
using Vortice.Direct2D1;

namespace SimOverlay.Overlays;

/// <summary>
/// Relative timing tower — shows ~15 drivers around the player with gap,
/// position, car, name, iRating, and license columns.
/// Phase 4 will implement full rendering; this stub satisfies Phase 3 wiring.
/// </summary>
public sealed class RelativeOverlay : BaseOverlay
{
    public const string OverlayId    = "Relative";
    public const string WindowTitle  = "SimOverlay \u2014 Relative";

    public static OverlayConfig DefaultConfig => new()
    {
        Id             = OverlayId,
        Enabled        = true,
        X              = 100,
        Y              = 200,
        Width          = 500,
        Height         = 380,
        Opacity        = 0.85f,
        BackgroundColor = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.75f },
        FontSize       = 13f,
        ShowIRating    = false,
        ShowLicense    = false,
        MaxDriversShown = 15,
    };

    public RelativeOverlay(
        ISimDataBus bus,
        OverlayConfig config,
        ConfigStore configStore,
        AppConfig appConfig)
        : base(WindowTitle, config, bus, configStore, appConfig)
    {
    }

    /// <inheritdoc/>
    protected override void OnRender(ID2D1DeviceContext context, OverlayConfig config)
    {
        // Phase 4: draw relative table rows.
    }
}
