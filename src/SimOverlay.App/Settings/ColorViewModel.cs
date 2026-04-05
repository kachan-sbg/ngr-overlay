using System.ComponentModel;
using System.Windows.Media;
using SimOverlay.Core.Config;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace SimOverlay.App.Settings;

/// <summary>
/// INPC wrapper for <see cref="ColorConfig"/> used in settings panels.
/// R/G/B/A are exposed as integers 0–255 for easy TextBox binding.
/// </summary>
public sealed class ColorViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int _r, _g, _b, _a = 255;

    public int R { get => _r; set { _r = Clamp(value); Notify(); } }
    public int G { get => _g; set { _g = Clamp(value); Notify(); } }
    public int B { get => _b; set { _b = Clamp(value); Notify(); } }
    public int A { get => _a; set { _a = Clamp(value); Notify(); } }

    /// <summary>Color preview brush — updates whenever any channel changes.</summary>
    public Brush PreviewBrush =>
        new SolidColorBrush(Color.FromArgb((byte)_a, (byte)_r, (byte)_g, (byte)_b));

    private static int Clamp(int v) => Math.Clamp(v, 0, 255);

    // Raises PropertyChanged for all properties at once (null = all).
    private void Notify() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public void LoadFrom(ColorConfig c)
    {
        _r = ToByte(c.R);
        _g = ToByte(c.G);
        _b = ToByte(c.B);
        _a = ToByte(c.A);
        Notify();
    }

    public ColorConfig ToColorConfig() => new()
    {
        R = _r / 255f,
        G = _g / 255f,
        B = _b / 255f,
        A = _a / 255f,
    };

    private static int ToByte(float f) =>
        (int)Math.Clamp(MathF.Round(f * 255f), 0f, 255f);
}
