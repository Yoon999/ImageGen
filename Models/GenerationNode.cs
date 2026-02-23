using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ImageGen.Models;

public enum NodeType
{
    Normal,
    Begin,
    End,
    Base,
    Character,
    BaseConcat,
    Graph
}

public class GenerationNode : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _title = string.Empty;
    private string _basePrompt = string.Empty;
    private string _negativePrompt = string.Empty;
    // private string _characterPrompt = string.Empty;
    private string _presetName = string.Empty;
    private double _charX = 0.5;
    private double _charY = 0.5;
    private double _uiX = 0;
    private double _uiY = 0;
    private double _width = 200;
    private double _height = 150; // Approximate height
    private bool _isCollapsed;
    private bool _isSelected;
    private bool _isBypassed;
    private GenerationNode? _nextNode;
    private NodeType _type = NodeType.Normal;
    
    // For multiple outputs (Character nodes)
    [JsonIgnore]
    public ObservableCollection<GenerationNode> NextNodes { get; } = new();
    
    // For BaseConcat inputs (Runtime only, ordered)
    [JsonIgnore]
    public ObservableCollection<GenerationNode> InputNodes { get; } = new();
    
    // Persisted order of inputs for BaseConcat
    public List<string> InputOrder { get; set; } = new();
    
    public string Id 
    { 
        get => _id; 
        set { _id = value; OnPropertyChanged(); } 
    }

    public GenerationNode()
    {
        NextNodes.CollectionChanged += (s, e) => OnPropertyChanged(nameof(NextNodes));
        InputNodes.CollectionChanged += (s, e) => OnPropertyChanged(nameof(InputNodes));
    }

    public NodeType Type
    {
        get => _type;
        set 
        { 
            _type = value; 
            if (string.IsNullOrEmpty(_title))
            {
                _title = _type.ToString();
            }
            OnPropertyChanged(); 
        }
    }

    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set { _isCollapsed = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public bool IsBypassed
    {
        get => _isBypassed;
        set { _isBypassed = value; OnPropertyChanged(); }
    }

    // Used as Positive Prompt for Base/Character nodes, and legacy prompt for Normal nodes if needed
    // For Graph nodes, this stores the file path
    public string BasePrompt
    {
        get => _basePrompt;
        set { _basePrompt = value; OnPropertyChanged(); }
    }

    public string NegativePrompt
    {
        get => _negativePrompt;
        set { _negativePrompt = value; OnPropertyChanged(); }
    }
    
    // Used for storing the preset name in Character nodes
    public string PresetName
    {
        get => _presetName;
        set { _presetName = value; OnPropertyChanged(); }
    }

    public double CharX
    {
        get => _charX;
        set { _charX = value; OnPropertyChanged(); }
    }

    public double CharY
    {
        get => _charY;
        set { _charY = value; OnPropertyChanged(); }
    }

    // UI Position & Size
    public double UiX
    {
        get => _uiX;
        set { _uiX = value; OnPropertyChanged(); }
    }

    public double UiY
    {
        get => _uiY;
        set { _uiY = value; OnPropertyChanged(); }
    }

    public double Width
    {
        get => _width;
        set { _width = value; OnPropertyChanged(); }
    }

    public double Height
    {
        get => _height;
        set { _height = value; OnPropertyChanged(); }
    }
    
    // Connection (Single output for Normal/Begin/Base nodes mostly, but we can generalize)
    // We keep NextNode for backward compatibility or single-path logic (Begin->Normal->End)
    // But for Character nodes, we use NextNodes.
    [JsonIgnore]
    public GenerationNode? NextNode
    {
        get => _nextNode;
        set 
        { 
            _nextNode = value; 
            OnPropertyChanged(); 
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
