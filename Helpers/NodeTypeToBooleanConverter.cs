using System.Globalization;
using System.Windows.Data;
using ImageGen.Models;

namespace ImageGen.Helpers;

public class NodeTypeToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is NodeType type)
        {
            string param = parameter as string ?? "";
            
            if (param == "IsReadOnlyTitle")
            {
                // Begin and End nodes should be read-only
                return type == NodeType.Begin || type == NodeType.End;
            }

            if (param == "IsFocusable")
            {
                // All nodes should be focusable
                return type != NodeType.Begin && type != NodeType.End;
            }
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
