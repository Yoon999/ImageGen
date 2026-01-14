using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using ImageGen.Models;
using ImageGen.Models.Api;
using ImageGen.Services;
using ImageGen.Services.Interfaces;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ImageGen.ViewModels;

public class NodeGraphViewModel : INotifyPropertyChanged
{
    private readonly MainViewModel _mainViewModel;
    private readonly INovelAiService _novelAiService;
    private readonly IImageService _imageService;
    
    private GenerationNode? _selectedNode;
    private bool _isConnecting;
    private GenerationNode? _connectingSource;
    private bool _isGeneratingChain;
    
    // Temporary connection line for dragging
    private double _tempX1;
    private double _tempY1;
    private double _tempX2;
    private double _tempY2;

    public ObservableCollection<GenerationNode> Nodes { get; } = new();
    public ObservableCollection<ConnectionViewModel> Connections { get; } = new();
    public ObservableCollection<CharacterPreset> CharacterPresets { get; } = new();
    
    public ICommand AddNodeCommand { get; }
    public ICommand AddBaseNodeCommand { get; }
    public ICommand AddCharacterNodeCommand { get; }
    public ICommand AddBaseConcatNodeCommand { get; }
    public ICommand AddBeginNodeCommand { get; }
    public ICommand AddEndNodeCommand { get; }
    public ICommand DeleteNodeCommand { get; }
    public ICommand DuplicateNodeCommand { get; }
    public ICommand StartConnectionCommand { get; }
    public ICommand CompleteConnectionCommand { get; }
    public ICommand ClearConnectionsCommand { get; }
    public ICommand DisconnectInputCommand { get; }
    public ICommand GenerateChainCommand { get; }
    public ICommand CancelConnectionCommand { get; }
    public ICommand SaveCharacterPresetCommand { get; }
    public ICommand UpdateCharacterPresetCommand { get; }
    public ICommand LoadCharacterPresetCommand { get; }
    public ICommand SaveGraphCommand { get; }
    public ICommand LoadGraphCommand { get; }
    public ICommand RefreshPresetsCommand { get; }
    public ICommand ToggleCollapseCommand { get; }

    public NodeGraphViewModel(MainViewModel mainViewModel, INovelAiService novelAiService, IImageService imageService)
    {
        _mainViewModel = mainViewModel;
        _novelAiService = novelAiService;
        _imageService = imageService;

        AddNodeCommand = new RelayCommand(ExecuteAddNode);
        AddBaseNodeCommand = new RelayCommand(ExecuteAddBaseNode);
        AddCharacterNodeCommand = new RelayCommand(ExecuteAddCharacterNode);
        AddBaseConcatNodeCommand = new RelayCommand(ExecuteAddBaseConcatNode);
        AddBeginNodeCommand = new RelayCommand(ExecuteAddBeginNode);
        AddEndNodeCommand = new RelayCommand(ExecuteAddEndNode);
        DeleteNodeCommand = new RelayCommand(ExecuteDeleteNode);
        DuplicateNodeCommand = new RelayCommand(ExecuteDuplicateNode);
        StartConnectionCommand = new RelayCommand(ExecuteStartConnection);
        CompleteConnectionCommand = new RelayCommand(ExecuteCompleteConnection);
        ClearConnectionsCommand = new RelayCommand(ExecuteClearConnections);
        DisconnectInputCommand = new RelayCommand(ExecuteDisconnectInput);
        GenerateChainCommand = new RelayCommand(ExecuteGenerateChain, CanExecuteGenerateChain);
        CancelConnectionCommand = new RelayCommand(ExecuteCancelConnection);
        SaveCharacterPresetCommand = new RelayCommand(ExecuteSaveCharacterPreset);
        UpdateCharacterPresetCommand = new RelayCommand(ExecuteUpdateCharacterPreset);
        LoadCharacterPresetCommand = new RelayCommand(ExecuteLoadCharacterPreset);
        SaveGraphCommand = new RelayCommand(ExecuteSaveGraph);
        LoadGraphCommand = new RelayCommand(ExecuteLoadGraph);
        RefreshPresetsCommand = new RelayCommand(_ => LoadPresets());
        ToggleCollapseCommand = new RelayCommand(ExecuteToggleCollapse);
        
        // Listen for node changes to update connections BEFORE adding initial nodes
        Nodes.CollectionChanged += Nodes_CollectionChanged;
        
        // Add initial nodes
        InitializePermanentNodes();
        LoadPresets();
    }

    private void LoadPresets()
    {
        CharacterPresets.Clear();
        foreach (var preset in new CharacterPresetService().GetPresets())
        {
            CharacterPresets.Add(preset);
        }
    }

    private void InitializePermanentNodes()
    {
        // Ensure Begin and End nodes exist
        if (!Nodes.Any(n => n.Type == NodeType.Begin))
        {
            Nodes.Add(new GenerationNode { UiX = 50, UiY = 100, Type = NodeType.Begin, BasePrompt = "Start" });
        }
        
        if (!Nodes.Any(n => n.Type == NodeType.End))
        {
            Nodes.Add(new GenerationNode { UiX = 600, UiY = 100, Type = NodeType.End, BasePrompt = "End" });
        }
    }

    private void Nodes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (GenerationNode node in e.NewItems)
            {
                node.PropertyChanged += Node_PropertyChanged;
            }
        }
        if (e.OldItems != null)
        {
            foreach (GenerationNode node in e.OldItems)
            {
                node.PropertyChanged -= Node_PropertyChanged;
            }
        }
    }

    private void Node_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GenerationNode.NextNode) || e.PropertyName == nameof(GenerationNode.NextNodes))
        {
            UpdateConnections();
        }
    }

    private void UpdateConnections()
    {
        // Simple rebuild for now (can be optimized)
        foreach (var conn in Connections) conn.Cleanup();
        Connections.Clear();

        foreach (var node in Nodes)
        {
            // Handle single connection (legacy/main flow)
            if (node.NextNode != null && Nodes.Contains(node.NextNode))
            {
                Connections.Add(new ConnectionViewModel(node, node.NextNode));
            }
            
            // Handle multiple connections (Character/Base nodes)
            foreach (var target in node.NextNodes)
            {
                if (Nodes.Contains(target))
                {
                    Connections.Add(new ConnectionViewModel(node, target));
                }
            }
        }
    }

    public bool IsGeneratingChain
    {
        get => _isGeneratingChain;
        set
        {
            _isGeneratingChain = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }
    
    public bool IsConnecting
    {
        get => _isConnecting;
        set
        {
            _isConnecting = value;
            OnPropertyChanged();
        }
    }
    
    public double TempX1
    {
        get => _tempX1;
        set { _tempX1 = value; OnPropertyChanged(); }
    }
    
    public double TempY1
    {
        get => _tempY1;
        set { _tempY1 = value; OnPropertyChanged(); }
    }
    
    public double TempX2
    {
        get => _tempX2;
        set { _tempX2 = value; OnPropertyChanged(); }
    }
    
    public double TempY2
    {
        get => _tempY2;
        set { _tempY2 = value; OnPropertyChanged(); }
    }

    public void UpdateTempConnection(double x, double y)
    {
        if (IsConnecting)
        {
            TempX2 = x;
            TempY2 = y;
        }
    }

    private void ExecuteAddNode(object? parameter)
    {
        var node = new GenerationNode
        {
            UiX = 200,
            UiY = 200,
            BasePrompt = "New Node",
            CharX = 0.5,
            CharY = 0.5,
            Type = NodeType.Normal
        };
        Nodes.Add(node);
    }

    private void ExecuteAddBaseNode(object? parameter)
    {
        var node = new GenerationNode
        {
            UiX = 200,
            UiY = 200,
            BasePrompt = "Positive Prompt",
            NegativePrompt = "Negative Prompt",
            Type = NodeType.Base
        };
        Nodes.Add(node);
    }

    private void ExecuteAddCharacterNode(object? parameter)
    {
        var node = new GenerationNode
        {
            UiX = 200,
            UiY = 200,
            BasePrompt = "Positive Prompt",
            NegativePrompt = "Negative Prompt",
            CharX = 0.5,
            CharY = 0.5,
            Type = NodeType.Character
        };
        Nodes.Add(node);
    }
    
    private void ExecuteAddBaseConcatNode(object? parameter)
    {
        var node = new GenerationNode
        {
            UiX = 200,
            UiY = 200,
            BasePrompt = "Concatenated Prompt",
            Type = NodeType.BaseConcat
        };
        Nodes.Add(node);
    }
    
    private void ExecuteAddBeginNode(object? parameter)
    {
        // Begin node is now permanent, this might be redundant or used for recovery
        if (Nodes.Any(n => n.Type == NodeType.Begin))
        {
            MessageBox.Show("Only one Begin node is allowed.");
            return;
        }
        
        var node = new GenerationNode
        {
            UiX = 50,
            UiY = 200,
            BasePrompt = "Start",
            Type = NodeType.Begin
        };
        Nodes.Add(node);
    }

    private void ExecuteAddEndNode(object? parameter)
    {
        // End node is now permanent
        if (Nodes.Any(n => n.Type == NodeType.End))
        {
            MessageBox.Show("Only one End node is allowed.");
            return;
        }

        var node = new GenerationNode
        {
            UiX = 400,
            UiY = 200,
            BasePrompt = "End",
            Type = NodeType.End
        };
        Nodes.Add(node);
    }

    private void ExecuteDeleteNode(object? parameter)
    {
        if (parameter is GenerationNode node)
        {
            // Prevent deletion of Begin and End nodes
            if (node.Type == NodeType.Begin || node.Type == NodeType.End)
            {
                return;
            }

            // Remove connections to this node
            foreach (var n in Nodes)
            {
                if (n.NextNode == node)
                {
                    n.NextNode = null;
                }
                if (n.NextNodes.Contains(node))
                {
                    n.NextNodes.Remove(node);
                }
            }
            Nodes.Remove(node);
            UpdateConnections();
        }
    }

    private void ExecuteDuplicateNode(object? parameter)
    {
        if (parameter is GenerationNode node)
        {
            // Prevent duplication of Begin and End nodes
            if (node.Type == NodeType.Begin || node.Type == NodeType.End)
            {
                return;
            }

            var newNode = new GenerationNode
            {
                UiX = node.UiX + 20,
                UiY = node.UiY + 20,
                Type = node.Type,
                Title = node.Title,
                BasePrompt = node.BasePrompt,
                NegativePrompt = node.NegativePrompt,
                // CharacterPrompt = node.CharacterPrompt,
                PresetName = node.PresetName,
                CharX = node.CharX,
                CharY = node.CharY,
                Width = node.Width,
                Height = node.Height,
                IsCollapsed = node.IsCollapsed
            };
            
            Nodes.Add(newNode);
        }
    }

    private void ExecuteStartConnection(object? parameter)
    {
        if (parameter is GenerationNode node)
        {
            if (node.Type == NodeType.End) return; // End node cannot have output
            
            IsConnecting = true;
            _connectingSource = node;
            
            // Set initial temp line position (Right side center)
            TempX1 = node.UiX + node.Width;
            TempY1 = node.UiY + (node.Height / 2);
            TempX2 = TempX1;
            TempY2 = TempY1;
        }
    }

    private void ExecuteCompleteConnection(object? parameter)
    {
        if (IsConnecting && _connectingSource != null && parameter is GenerationNode targetNode)
        {
            if (_connectingSource != targetNode)
            {
                if (targetNode.Type == NodeType.Begin)
                {
                     // Begin node cannot be a target
                     IsConnecting = false;
                     _connectingSource = null;
                     return;
                }
                
                // If source is Character or Base node, allow multiple outputs
                // BaseConcat also acts as a Base node source
                if (_connectingSource.Type == NodeType.Character || _connectingSource.Type == NodeType.Base || _connectingSource.Type == NodeType.BaseConcat)
                {
                    if (!_connectingSource.NextNodes.Contains(targetNode))
                    {
                        _connectingSource.NextNodes.Add(targetNode);
                        UpdateConnections(); // Explicitly update because ObservableCollection change might not trigger property changed for NextNodes property itself
                    }
                }
                else
                {
                    // Normal/Begin nodes usually have single output flow
                    _connectingSource.NextNode = targetNode;
                }
            }
            IsConnecting = false;
            _connectingSource = null;
        }
    }
    
    private void ExecuteCancelConnection(object? parameter)
    {
        IsConnecting = false;
        _connectingSource = null;
    }
    
    private void ExecuteClearConnections(object? parameter)
    {
        if (parameter is GenerationNode node)
        {
            node.NextNode = null;
            node.NextNodes.Clear();
            UpdateConnections();
        }
    }
    
    private void ExecuteDisconnectInput(object? parameter)
    {
        if (parameter is GenerationNode targetNode)
        {
            foreach (var node in Nodes)
            {
                if (node.NextNode == targetNode)
                {
                    node.NextNode = null;
                }
                if (node.NextNodes.Contains(targetNode))
                {
                    node.NextNodes.Remove(targetNode);
                }
            }
            UpdateConnections();
        }
    }

    private void ExecuteSaveCharacterPreset(object? parameter)
    {
        if (parameter is GenerationNode node && node.Type == NodeType.Character)
        {
            string name = node.PresetName;
            
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Character_{DateTime.Now:yyyyMMdd_HHmmss}";
                if (!string.IsNullOrWhiteSpace(node.BasePrompt) && node.BasePrompt.Length < 20)
                {
                    name = node.BasePrompt;
                }
                node.PresetName = name; // Update node's preset name
            }

            var preset = new CharacterPreset
            {
                Name = name,
                Prompt = node.BasePrompt,
                NegativePrompt = node.NegativePrompt,
                X = node.CharX,
                Y = node.CharY
            };
            
            new CharacterPresetService().SavePreset(preset);
            LoadPresets(); // Refresh list
            MessageBox.Show($"Saved preset: {name}");
        }
    }

    private void ExecuteUpdateCharacterPreset(object? parameter)
    {
        // This command is intended to update an existing preset selected in the ComboBox
        // We need both the Node (source of data) and the Selected Preset (target to update)
        
        if (parameter is object[] args && args.Length == 2)
        {
            if (args[0] is GenerationNode node && args[1] is CharacterPreset selectedPreset)
            {
                // Update the selected preset with current node data
                selectedPreset.Prompt = node.BasePrompt;
                selectedPreset.NegativePrompt = node.NegativePrompt;
                selectedPreset.X = node.CharX;
                selectedPreset.Y = node.CharY;
                
                // Save using service (it handles update by name)
                new CharacterPresetService().SavePreset(selectedPreset);
                LoadPresets();
                MessageBox.Show($"Updated preset: {selectedPreset.Name}");
            }
            else
            {
                MessageBox.Show("Please select a preset to update.");
            }
        }
    }

    private void ExecuteLoadCharacterPreset(object? parameter)
    {
        if (parameter is object[] args && args.Length == 2)
        {
            if (args[0] is GenerationNode node && args[1] is CharacterPreset preset)
            {
                node.BasePrompt = preset.Prompt;
                node.NegativePrompt = preset.NegativePrompt;
                node.CharX = preset.X;
                node.CharY = preset.Y;
                node.PresetName = preset.Name; // Also load the name
            }
        }
    }

    private void ExecuteSaveGraph(object? parameter)
    {
        var saveData = new NodeGraphSaveData
        {
            Nodes = Nodes.ToList()
        };

        foreach (var node in Nodes)
        {
            if (node.NextNode != null)
            {
                saveData.Connections.Add(new NodeConnectionData
                {
                    SourceId = node.Id,
                    TargetId = node.NextNode.Id
                });
            }
            
            foreach (var target in node.NextNodes)
            {
                saveData.Connections.Add(new NodeConnectionData
                {
                    SourceId = node.Id,
                    TargetId = target.Id
                });
            }
        }

        var dialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = "json",
            FileName = "graph_layout.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string json = JsonSerializer.Serialize(saveData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, json);
                MessageBox.Show("Graph saved successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving graph: {ex.Message}");
            }
        }
    }

    private void ExecuteLoadGraph(object? parameter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string json = File.ReadAllText(dialog.FileName);
                var saveData = JsonSerializer.Deserialize<NodeGraphSaveData>(json);

                if (saveData != null)
                {
                    Nodes.Clear();
                    Connections.Clear();

                    // Add nodes
                    foreach (var node in saveData.Nodes)
                    {
                        Nodes.Add(node);
                    }

                    // Restore connections
                    foreach (var conn in saveData.Connections)
                    {
                        var source = Nodes.FirstOrDefault(n => n.Id == conn.SourceId);
                        var target = Nodes.FirstOrDefault(n => n.Id == conn.TargetId);

                        if (source != null && target != null)
                        {
                            if (source.Type == NodeType.Character || source.Type == NodeType.Base || source.Type == NodeType.BaseConcat)
                            {
                                source.NextNodes.Add(target);
                            }
                            else
                            {
                                source.NextNode = target;
                            }
                        }
                    }
                    
                    UpdateConnections();
                    MessageBox.Show("Graph loaded successfully.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading graph: {ex.Message}");
            }
        }
    }

    private void ExecuteToggleCollapse(object? parameter)
    {
        if (parameter is GenerationNode node)
        {
            node.IsCollapsed = !node.IsCollapsed;
        }
    }

    private bool CanExecuteGenerateChain(object? parameter)
    {
        return !IsGeneratingChain && !_mainViewModel.IsGenerating && !string.IsNullOrWhiteSpace(_mainViewModel.ApiToken);
    }

    private async void ExecuteGenerateChain(object? parameter)
    {
        GenerationNode? startNode = parameter as GenerationNode;
        
        if (startNode == null)
        {
            // Find Begin node
            startNode = Nodes.FirstOrDefault(n => n.Type == NodeType.Begin);
        }

        if (startNode == null) 
        {
             MessageBox.Show("No starting node found. Please add a Begin node or select a start node.");
             return;
        }

        IsGeneratingChain = true;
        _mainViewModel.StatusMessage = "Starting chain generation...";

        try
        {
            var currentNode = startNode.NextNode;
            int stepIndex = 1;
            
            while (currentNode != null)
            {
                if (currentNode.Type == NodeType.End)
                {
                    _mainViewModel.StatusMessage = "Reached End node.";
                    break;
                }
                
                if (currentNode.Type == NodeType.Normal)
                {
                    _mainViewModel.StatusMessage = $"Generating node {stepIndex}...";
                    
                    // Find connected Base and Character nodes
                    // We need to check both NextNode and NextNodes of all nodes to find who points to currentNode
                    var incomingNodes = Nodes.Where(n => n.NextNode == currentNode || n.NextNodes.Contains(currentNode)).ToList();

                    // Handle BaseConcat nodes logic here if needed
                    // For now, we just treat BaseConcat as a Base node provider if it's connected
                    // But we need to resolve its content first (concatenate inputs)
                    
                    // First, resolve any BaseConcat nodes in the incoming list
                    foreach (var node in incomingNodes.Where(n => n.Type == NodeType.BaseConcat).ToList())
                    {
                        // Find inputs to this BaseConcat node
                        var concatInputs = Nodes.Where(n => n.NextNodes.Contains(node)).ToList();
                        
                        // Concatenate prompts
                        string combinedPositive = "";
                        string combinedNegative = "";
                        
                        foreach (var input in concatInputs)
                        {
                            if (!string.IsNullOrWhiteSpace(input.BasePrompt))
                            {
                                if (!string.IsNullOrWhiteSpace(combinedPositive)) combinedPositive += ", ";
                                combinedPositive += input.BasePrompt;
                            }
                            if (!string.IsNullOrWhiteSpace(input.NegativePrompt))
                            {
                                if (!string.IsNullOrWhiteSpace(combinedNegative)) combinedNegative += ", ";
                                combinedNegative += input.NegativePrompt;
                            }
                        }
                        
                        // Update the BaseConcat node's prompt temporarily (or permanently?)
                        // Ideally we shouldn't modify the node's stored prompt if it's meant to be dynamic,
                        // but for generation we need the value.
                        // Let's assume BaseConcat node's BasePrompt is the output.
                        node.BasePrompt = combinedPositive;
                        node.NegativePrompt = combinedNegative;
                    }

                    var baseNode = incomingNodes.FirstOrDefault(n => n.Type == NodeType.Base || n.Type == NodeType.BaseConcat);
                    var charNodes = incomingNodes.Where(n => n.Type == NodeType.Character).ToList();
                    
                    // Prepare Request
                    var request = CloneRequest(_mainViewModel.Request);
                    bool isV4 = request.model.Contains("nai-diffusion-4");

                    if (isV4)
                    {
                        if (request.parameters.V4Prompt == null)
                        {
                            request.parameters.V4Prompt = new V4ConditionInput
                            {
                                UseCoords = true,
                                UseOrder = true
                            };
                        }

                        // Set Base Prompt
                        if (baseNode != null)
                        {
                            request.parameters.V4Prompt.Caption.BaseCaption = baseNode.BasePrompt;
                            
                            if (request.parameters.V4NegativePrompt == null)
                            {
                                request.parameters.V4NegativePrompt = new V4ConditionInput
                                {
                                    Caption = new V4ExternalCaption(),
                                    UseCoords = false,
                                    UseOrder = false
                                };
                            }
                            request.parameters.V4NegativePrompt.Caption.BaseCaption = baseNode.NegativePrompt;
                        }
                        else
                        {
                            request.parameters.V4Prompt.Caption.BaseCaption = currentNode.BasePrompt;
                        }
                        
                        // Set Character Prompts
                        request.parameters.V4Prompt.Caption.CharCaptions.Clear();
                        foreach (var charNode in charNodes)
                        {
                            request.parameters.V4Prompt.Caption.CharCaptions.Add(new V4ExternalCharacterCaption
                            {
                                CharCaption = charNode.BasePrompt, // Character node uses BasePrompt for positive
                                Centers = new List<Coordinates>
                                {
                                    new Coordinates { x = charNode.CharX, y = charNode.CharY }
                                }
                            });
                            
                            request.parameters.V4NegativePrompt?.Caption.CharCaptions.Clear();
                            request.parameters.V4NegativePrompt?.Caption.CharCaptions.Add(new V4ExternalCharacterCaption
                            {
                                CharCaption = charNode.NegativePrompt,
                                Centers = new List<Coordinates>
                                {
                                    new Coordinates { x = charNode.CharX, y = charNode.CharY }
                                }
                            });
                        }
                        
                        request.input = request.parameters.V4Prompt.Caption.BaseCaption; 
                    }
                    else
                    {
                        // Legacy handling
                        string fullPrompt = "";
                        if (baseNode != null) fullPrompt += baseNode.BasePrompt;
                        else fullPrompt += currentNode.BasePrompt;
                        
                        foreach (var charNode in charNodes)
                        {
                            if (!string.IsNullOrWhiteSpace(fullPrompt)) fullPrompt += ", ";
                            fullPrompt += charNode.BasePrompt;
                        }
                        request.input = fullPrompt;
                    }

                    if (_mainViewModel.IsRandomSeed)
                    {
                        request.parameters.seed = new Random().NextInt64(1, 9999999999);
                    }

                    // Generate
                    byte[]? imageData = null;
                
                    await Task.Run(async () =>
                    {
                        var lastUpdate = DateTime.MinValue;
                        await foreach (var zipData in _novelAiService.GenerateImageStreamAsync(request, _mainViewModel.ApiToken))
                        {
                            imageData = zipData;
                            var now = DateTime.Now;
                            if ((now - lastUpdate).TotalMilliseconds < 60) continue;
                            lastUpdate = now;

                            try
                            {
                                var bitmap = _imageService.ConvertToBitmapImage(imageData);
                                Application.Current.Dispatcher.Invoke(() => { _mainViewModel.GeneratedImage = bitmap; });
                            }
                            catch { }
                        }
                    });

                    if (imageData != null)
                    {
                        _mainViewModel.GeneratedImage = await Task.Run(() => _imageService.ConvertToBitmapImage(imageData));
                        
                        if (!Directory.Exists(_mainViewModel.SaveDirectory))
                        {
                            Directory.CreateDirectory(_mainViewModel.SaveDirectory);
                        }
                            
                        string fileName = $"chain_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                        await _imageService.SaveImageAsync(imageData, _mainViewModel.SaveDirectory, fileName);
                            
                        var bitmap = await Task.Run(() => _imageService.ConvertToBitmapImage(imageData));
                        Application.Current.Dispatcher.Invoke(() => { _mainViewModel.GeneratedImage = bitmap; });
                    }
                    
                    stepIndex++;
                }

                currentNode = currentNode.NextNode;
                
                await Task.Delay(500);
            }
            
            _mainViewModel.StatusMessage = "Chain generation complete.";
        }
        catch (Exception ex)
        {
            _mainViewModel.StatusMessage = $"Chain Error: {ex.Message}";
        }
        finally
        {
            IsGeneratingChain = false;
        }
    }

    private GenerationRequest CloneRequest(GenerationRequest original)
    {
        var json = JsonSerializer.Serialize(original);
        return JsonSerializer.Deserialize<GenerationRequest>(json) ?? new GenerationRequest();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
