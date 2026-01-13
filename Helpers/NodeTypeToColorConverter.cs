using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ImageGen.Models;
using Color = System.Windows.Media.Color;

namespace ImageGen.Helpers;

public class NodeTypeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is NodeType type)
        {
            return type switch
            {
                NodeType.Begin => new SolidColorBrush(Color.FromRgb(30, 60, 90)), // Blue-ish
                NodeType.End => new SolidColorBrush(Color.FromRgb(90, 30, 30)),   // Red-ish
                NodeType.Base => new SolidColorBrush(Color.FromRgb(30, 90, 30)),  // Green-ish
                NodeType.Character => new SolidColorBrush(Color.FromRgb(90, 30, 90)), // Purple-ish
                _ => new SolidColorBrush(Color.FromRgb(51, 51, 51))               // Default Dark Gray
            };
        }
        return new SolidColorBrush(Color.FromRgb(51, 51, 51));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
