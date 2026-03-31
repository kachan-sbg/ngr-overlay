namespace SimOverlay.Core.Config;

public sealed class AppConfig
{
    public GlobalSettings GlobalSettings { get; set; } = new();
    public List<OverlayConfig> Overlays { get; set; } = [];
}
