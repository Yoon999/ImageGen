using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace ImageGen.Helpers;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolean)
        {
            if (parameter is string str)
            {
                if (str.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
                {
                    return boolean ? Visibility.Collapsed : Visibility.Visible;
                }
                
                if (str.Equals("SelectionColor", StringComparison.OrdinalIgnoreCase))
                {
                    return boolean ? Brushes.Yellow : new SolidColorBrush(Color.FromRgb(85, 85, 85)); // #555
                }
                
                if (str.Equals("SelectionThickness", StringComparison.OrdinalIgnoreCase))
                {
                    return boolean ? new Thickness(2) : new Thickness(1);
                }
                
                if (str.Equals("Opacity", StringComparison.OrdinalIgnoreCase))
                {
                    return boolean ? 0.5 : 1.0;
                }
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
