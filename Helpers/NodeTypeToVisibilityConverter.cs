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
                return (type == NodeType.Begin || type == NodeType.Base || type == NodeType.Character) ? Visibility.Hidden : Visibility.Visible;
            }
            
            // Base/Character Input Ports (Specific for Normal Node)
            // Only Normal nodes should have these inputs visible?
            // Or maybe we just use the main input port for everything and distinguish by connection type?
            // The request said "Base, Character 인풋 핀 추가".
            // If we add specific pins, we need to handle them.
            // For now, let's just show them on Normal nodes.
            if (param == "BaseInput" || param == "CharacterInput")
            {
                // Only Normal nodes accept Base/Character inputs?
                // Actually, the user might want to chain them differently, but typically Base/Character feed into Normal.
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
