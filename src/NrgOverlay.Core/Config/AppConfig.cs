namespace NrgOverlay.Core.Config;

public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public GlobalSettings GlobalSettings { get; set; } = new();
    public List<OverlayConfig> Overlays { get; set; } = [];
}

