using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Rendering;
using Vortice.Direct2D1;

namespace SimOverlay.Overlays;

/// <summary>
/// Delta bar — animated green/red bar showing the gap vs. player's best lap,
/// with an optional trend arrow and numeric delta text.
/// Phase 4 will implement full rendering; this stub satisfies Phase 3 wiring.
/// </summary>
public sealed class DeltaBarOverlay : BaseOverlay
{
    public const string OverlayId   = "DeltaBar";
    public const string WindowTitle = "SimOverlay \u2014 Delta";

    public static OverlayConfig DefaultConfig => new()
    {
        Id                 = OverlayId,
        Enabled            = true,
        X                  = 100,
        Y                  = 780,
        Width              = 400,
        Height             = 40,
        Opacity            = 0.85f,
        BackgroundColor    = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.75f },
        FontSize           = 13f,
        DeltaBarMaxSeconds = 2f,
        FasterColor        = ColorConfig.Green,
        SlowerColor        = ColorConfig.Red,
        ShowTrendArrow     = true,
        ShowDeltaText      = true,
    };

    public DeltaBarOverlay(
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
        // Phase 4: draw delta bar and trend arrow.
    }
}
