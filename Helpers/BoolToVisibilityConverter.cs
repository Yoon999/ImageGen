using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ImageGen.Helpers;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolean)
        {
            bool isInverse = parameter is string str && str.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
            
            if (isInverse)
            {
                return boolean ? Visibility.Collapsed : Visibility.Visible;
            }
            
            return boolean ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            bool isInverse = parameter is string str && str.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
            
            if (isInverse)
            {
                return visibility != Visibility.Visible;
            }
            
            return visibility == Visibility.Visible;
        }
        return false;
    }
}
