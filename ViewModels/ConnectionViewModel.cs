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

    // Calculate connection points
    // Source Output: Right side center
    // Target Input: Left side center
    
    // Adjust for margins/padding if necessary. 
    // Assuming Width/Height are actual render sizes updated by SizeChanged event.

    public double X1 => _source.UiX + _source.Width; 
    public double Y1 => _source.UiY + (_source.Height / 2); 

    public double X2 => _target.UiX;
    public double Y2 
    {
        get
        {
            // If source is Base, connect to Base Input Pin (offset from top)
            if (_source.Type == NodeType.Base)
            {
                // Base Input Pin is the second one (index 1) in the stack panel
                // Approx offset: 12 (Main) + 4 (Margin) + 10 (Base) / 2 = ~21?
                // Let's approximate based on visual layout
                // Main Input: Center - 10?
                // Base Input: Center
                // Character Input: Center + 10?
                
                // Actually, the stack panel is centered vertically.
                // Main Input (12px)
                // Base Input (10px)
                // Character Input (10px)
                // Total height ~ 32px + margins.
                
                // If we want to be precise, we need to know the exact layout.
                // For now, let's just offset slightly if it's a specific type.
                
                return _target.UiY + (_target.Height / 2); // Keep it simple for now, or adjust if needed visually
            }
            
            if (_source.Type == NodeType.Character)
            {
                return _target.UiY + (_target.Height / 2) + 10; // Offset down slightly
            }
            
            return _target.UiY + (_target.Height / 2);
        }
    }

    public GenerationNode Source => _source;
    public GenerationNode Target => _target;

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
