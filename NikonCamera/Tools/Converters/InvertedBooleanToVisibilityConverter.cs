using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NikonCamera.Tools.Converters;

public class InvertedBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value != null && value.GetType() != typeof(bool)) return Visibility.Visible;

        var isTrue = value != null && (bool) value;

        return isTrue 
            ? Visibility.Collapsed 
            : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return true;
    }
}