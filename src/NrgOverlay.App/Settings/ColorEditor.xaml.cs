using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace NrgOverlay.App.Settings;

/// <summary>
/// Compact inline RGBA color editor.
/// Set DataContext to a <see cref="ColorViewModel"/>.
/// </summary>
public partial class ColorEditor : UserControl
{
    public ColorEditor()
    {
        InitializeComponent();
    }
}

