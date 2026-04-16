using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace NrgOverlay.App.Settings;

/// <summary>
/// Per-overlay settings panel.
/// Call <see cref="Load"/> to bind a ViewModel and wire the preview callback
/// before making this panel visible.
/// </summary>
public partial class OverlaySettingsPanel : UserControl
{
    private Action? _preview;

    public OverlaySettingsPanel()
    {
        InitializeComponent();
    }

    // в”Ђв”Ђ Public entry point в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>
    /// Binds <paramref name="vm"/> as DataContext and configures which
    /// overlay-specific sections are shown.
    /// </summary>
    /// <param name="overlayId">Used to decide which optional sections to show.</param>
    /// <param name="vm">The ViewModel for this overlay.</param>
    /// <param name="preview">Callback invoked on every LostFocus / toggle change.</param>
    public void Load(string overlayId, OverlayConfigViewModel vm, Action preview)
    {
        _preview   = preview;
        DataContext = vm;

        // Show the sections relevant to this overlay type.
        // IDs match the constants in each overlay class (e.g. RelativeOverlay.OverlayId).
        bool isRelative = overlayId == "Relative";
        bool isSession  = overlayId == "SessionInfo";
        bool isDelta    = overlayId == "DeltaBar";

        RelativeSection.Visibility = isRelative ? Visibility.Visible : Visibility.Collapsed;
        SessionSection.Visibility  = isSession  ? Visibility.Visible : Visibility.Collapsed;
        DeltaSection.Visibility    = isDelta    ? Visibility.Visible : Visibility.Collapsed;

    }

    // в”Ђв”Ђ Event handlers вЂ” all trigger preview в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void ScreenPanel_LostFocus(object sender, RoutedEventArgs e)
    {
        // Fired when any input commits a value.
        _preview?.Invoke();
    }

    private void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        _preview?.Invoke();
    }

    private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _preview?.Invoke();
    }
}

