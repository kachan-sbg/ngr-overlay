using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using UserControl = System.Windows.Controls.UserControl;

namespace SimOverlay.App.Settings;

/// <summary>
/// A labelled row used in the Settings panels.
/// Declare child elements inside the tag — they are placed in the content slot.
/// </summary>
[ContentProperty(nameof(Children))]
public partial class FieldRow : UserControl
{
    // ── Label dependency property ─────────────────────────────────────────────
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(FieldRow),
            new PropertyMetadata(string.Empty, OnLabelChanged));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FieldRow)d).LabelBlock.Text = (string)e.NewValue;
    }

    // ── Children proxy ────────────────────────────────────────────────────────
    public UIElementCollection Children => ContentSlot.Children;

    public FieldRow()
    {
        InitializeComponent();
    }
}
