namespace NrgOverlay.Core.Config;

public sealed class ColorConfig
{
    public float R { get; set; }
    public float G { get; set; }
    public float B { get; set; }
    public float A { get; set; } = 1f;

    public static ColorConfig White => new() { R = 1f, G = 1f, B = 1f, A = 1f };
    public static ColorConfig Black => new() { R = 0f, G = 0f, B = 0f, A = 1f };
    public static ColorConfig DarkBackground => new() { R = 0f, G = 0f, B = 0f, A = 0.75f };
    public static ColorConfig PlayerHighlight => new() { R = 0.2f, G = 0.5f, B = 1f, A = 0.35f };
    public static ColorConfig Green => new() { R = 0f, G = 0.87f, B = 0f, A = 1f };
    public static ColorConfig Red => new() { R = 0.87f, G = 0.13f, B = 0.13f, A = 1f };
    public static ColorConfig Blue => new() { R = 0.2f, G = 0.4f, B = 1f, A = 1f };
}

