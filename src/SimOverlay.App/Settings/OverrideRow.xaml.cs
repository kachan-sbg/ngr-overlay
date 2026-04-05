using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using UserControl = System.Windows.Controls.UserControl;

namespace SimOverlay.App.Settings;

/// <summary>
/// A Stream Override row: [☐ Custom] [Label] [Content].
/// When HasOverride is false the content is visually dimmed and the hosted
/// input control should have its IsEnabled bound to HasOverride in the parent.
/// </summary>
[ContentProperty(nameof(Children))]
public partial class OverrideRow : UserControl
{
    // ── HasOverride ───────────────────────────────────────────────────────────
    public static readonly DependencyProperty HasOverrideProperty =
        DependencyProperty.Register(
            nameof(HasOverride), typeof(bool), typeof(OverrideRow),
            new FrameworkPropertyMetadata(false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public bool HasOverride
    {
        get => (bool)GetValue(HasOverrideProperty);
        set => SetValue(HasOverrideProperty, value);
    }

    // ── Label ─────────────────────────────────────────────────────────────────
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(OverrideRow),
            new PropertyMetadata(string.Empty, OnLabelChanged));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((OverrideRow)d).LabelBlock.Text = (string)e.NewValue;
    }

    // ── Children proxy ────────────────────────────────────────────────────────
    public UIElementCollection Children => ContentSlot.Children;

    public OverrideRow()
    {
        InitializeComponent();
    }
}
