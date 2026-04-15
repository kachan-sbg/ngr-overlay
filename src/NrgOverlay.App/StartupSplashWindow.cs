using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaLinearGradientBrush = System.Windows.Media.LinearGradientBrush;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
using MediaStretch = System.Windows.Media.Stretch;
using WpfImage = System.Windows.Controls.Image;

namespace NrgOverlay.App;

internal sealed class StartupSplashWindow : Window
{
    public static readonly TimeSpan MinimumVisibleDuration = TimeSpan.FromSeconds(2.5);

    public StartupSplashWindow()
    {
        Width = 620;
        Height = 320;
        MinWidth = Width;
        MinHeight = Height;
        MaxWidth = Width;
        MaxHeight = Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        AllowsTransparency = true;
        Background = MediaBrushes.Transparent;

        var root = new Border
        {
            CornerRadius = new CornerRadius(20),
            BorderBrush = new MediaSolidColorBrush(MediaColor.FromRgb(0xD6, 0xCA, 0xB2)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24),
            Background = new MediaLinearGradientBrush(
                MediaColor.FromRgb(0xF8, 0xF2, 0xE7),
                MediaColor.FromRgb(0xE8, 0xF2, 0xEE),
                135),
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var logo = new WpfImage
        {
            Width = 520,
            Height = 180,
            Stretch = MediaStretch.Uniform,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
        };
        LoadLogo(logo);
        Grid.SetRow(logo, 0);
        grid.Children.Add(logo);

        var footer = new TextBlock
        {
            Text = "NrgOverlay is starting...",
            Foreground = new MediaSolidColorBrush(MediaColor.FromRgb(0x2B, 0x60, 0x62)),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            Margin = new Thickness(0, 16, 0, 6),
        };
        Grid.SetRow(footer, 1);
        grid.Children.Add(footer);

        root.Child = grid;
        Content = root;
    }

    private static void LoadLogo(WpfImage target)
    {
        var pngPath = Path.Combine(AppContext.BaseDirectory, "Resources", "nrgoverlay-logo.png");
        if (!File.Exists(pngPath))
            return;

        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri(pngPath, UriKind.Absolute);
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            target.Source = img;
        }
        catch
        {
            // Cosmetic only.
        }
    }
}

