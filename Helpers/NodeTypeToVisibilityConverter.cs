using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ImageGen.Models;

namespace ImageGen.Helpers;

public class NodeTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is NodeType type)
        {
            string param = parameter as string ?? "";
            
            // Input Port (Left side): Hidden for Begin node
            if (param == "Input")
            {
                // Base and Character nodes don't have input flow (they are sources)
                // Begin node doesn't have input flow
                // Normal and End nodes have input flow
                // BaseConcat has input flow (from Base nodes)
                return (type == NodeType.Begin || type == NodeType.Base || type == NodeType.Character) ? Visibility.Hidden : Visibility.Visible;
            }
            
            // Base/Character Input Ports (Specific for Normal Node)
            if (param == "BaseInput")
            {
                // Normal nodes accept Base inputs
                // BaseConcat nodes accept Base inputs
                return (type == NodeType.Normal || type == NodeType.BaseConcat) ? Visibility.Visible : Visibility.Collapsed;
            }
            
            if (param == "CharacterInput")
            {
                // Only Normal nodes accept Character inputs
                return type == NodeType.Normal ? Visibility.Visible : Visibility.Collapsed;
            }
            
            // Output Port (Right side): Hidden for End node
            if (param == "Output")
            {
                return type == NodeType.End ? Visibility.Hidden : Visibility.Visible;
            }

            // Content (Prompts, etc.): Visible only for Normal nodes
            if (param == "Content")
            {
                return type == NodeType.Normal ? Visibility.Visible : Visibility.Collapsed;
            }

            // Base Content
            if (param == "BaseContent")
            {
                return type == NodeType.Base ? Visibility.Visible : Visibility.Collapsed;
            }

            // Character Content
            if (param == "CharacterContent")
            {
                return type == NodeType.Character ? Visibility.Visible : Visibility.Collapsed;
            }
            
            // BaseConcat Content
            if (param == "BaseConcatContent")
            {
                return type == NodeType.BaseConcat ? Visibility.Visible : Visibility.Collapsed;
            }

            // Delete Button: Hidden for Begin and End nodes
            if (param == "Delete")
            {
                return (type == NodeType.Begin || type == NodeType.End) ? Visibility.Collapsed : Visibility.Visible;
            }
            
            if (param == "Collapse")
            {
                return (type == NodeType.Begin || type == NodeType.End) ? Visibility.Collapsed : Visibility.Visible;
            }
            
            // Default behavior (if any)
            return Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
