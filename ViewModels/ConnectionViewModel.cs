using System.ComponentModel;
using System.Runtime.CompilerServices;
using ImageGen.Models;

namespace ImageGen.ViewModels;

public class ConnectionViewModel : INotifyPropertyChanged
{
    private readonly GenerationNode _source;
    private readonly GenerationNode _target;

    public ConnectionViewModel(GenerationNode source, GenerationNode target)
    {
        _source = source;
        _target = target;

        _source.PropertyChanged += OnNodePropertyChanged;
        _target.PropertyChanged += OnNodePropertyChanged;
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GenerationNode.UiX) || 
            e.PropertyName == nameof(GenerationNode.UiY) ||
            e.PropertyName == nameof(GenerationNode.Width) ||
            e.PropertyName == nameof(GenerationNode.Height))
        {
            OnPropertyChanged(nameof(X1));
            OnPropertyChanged(nameof(Y1));
            OnPropertyChanged(nameof(X2));
            OnPropertyChanged(nameof(Y2));
        }
    }

    public double X1 => _source.UiX + _source.Width; 
    public double Y1 => _source.UiY + (_source.Height / 2); 

    public double X2 => _target.UiX;
    public double Y2 => _target.UiY + GetTargetInputOffset();

    private double GetTargetInputOffset()
    {
        var visiblePorts = GetVisibleInputPorts(_target.Type);
        if (visiblePorts.Count == 0)
        {
            return _target.Height / 2;
        }

        var desiredPort = GetDesiredTargetPort(_source.Type);
        if (!visiblePorts.Contains(desiredPort))
        {
            desiredPort = visiblePorts.Contains(InputPortKind.Base) ? InputPortKind.Base : visiblePorts[0];
        }

        double totalHeight = visiblePorts.Sum(GetPortSlotHeight);
        double top = (_target.Height - totalHeight) / 2;
        double cursor = top;

        foreach (var port in visiblePorts)
        {
            double slotHeight = GetPortSlotHeight(port);
            if (port == desiredPort)
            {
                return cursor + (slotHeight / 2);
            }

            cursor += slotHeight;
        }

        return _target.Height / 2;
    }

    public GenerationNode Source => _source;
    public GenerationNode Target => _target;

    private enum InputPortKind
    {
        Flow,
        Base,
        Character
    }

    private static InputPortKind GetDesiredTargetPort(NodeType sourceType)
    {
        return sourceType switch
        {
            NodeType.Base or NodeType.BaseConcat => InputPortKind.Base,
            NodeType.Character => InputPortKind.Character,
            _ => InputPortKind.Flow
        };
    }

    private static List<InputPortKind> GetVisibleInputPorts(NodeType targetType)
    {
        var ports = new List<InputPortKind>();

        if (targetType is not NodeType.Begin and not NodeType.Base and not NodeType.Character)
        {
            ports.Add(InputPortKind.Flow);
        }

        if (targetType is NodeType.Normal or NodeType.BaseConcat)
        {
            ports.Add(InputPortKind.Base);
        }

        if (targetType == NodeType.Normal)
        {
            ports.Add(InputPortKind.Character);
        }

        return ports;
    }

    private static double GetPortSlotHeight(InputPortKind port)
    {
        return port == InputPortKind.Flow ? 22 : 20;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    
    public void Cleanup()
    {
        _source.PropertyChanged -= OnNodePropertyChanged;
        _target.PropertyChanged -= OnNodePropertyChanged;
    }
}
