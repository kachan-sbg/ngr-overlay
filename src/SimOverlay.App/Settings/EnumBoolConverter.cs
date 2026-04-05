using System.Globalization;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;

namespace SimOverlay.App.Settings;

/// <summary>
/// Converts an enum value to bool for RadioButton bindings.
/// ConverterParameter must match the enum member name (as a string).
/// Usage: IsChecked="{Binding MyProp, Converter={x:Static local:EnumBoolConverter.Instance},
///                             ConverterParameter=MemberName}"
/// </summary>
public sealed class EnumBoolConverter : IValueConverter
{
    public static readonly EnumBoolConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return value.ToString() == parameter.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is not null)
            return Enum.Parse(targetType, parameter.ToString()!);
        return Binding.DoNothing;
    }
}
