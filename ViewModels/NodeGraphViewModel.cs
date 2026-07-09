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
using ImageGen.Views;
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
    private readonly CharacterPresetService _presetService;
    private readonly TagSuggestionService _tagSuggestionService;
    private readonly ImageGenerationWorkflow _imageGenerationWorkflow;
    private readonly NodeGraphService _nodeGraphService = new();

    private bool _isConnecting;
    private GenerationNode? _connectingSource;
    private bool _isGeneratingChain;

    // Temporary connection line for dragging
    private double _tempX1;
    private double _tempY1;
    private double _tempX2;
    private double _tempY2;

    // Zoom
    private double _zoomScale = 1.0;

    // Selection Box
    private bool _isSelecting;
    private double _selectionX;
    private double _selectionY;
    private double _selectionWidth;
    private double _selectionHeight;

    private ObservableCollection<TagSuggestion> _tagSuggestions = new();

    public ObservableCollection<GenerationNode> Nodes { get; } = new();
    public ObservableCollection<ConnectionViewModel> Connections { get; } = new();
    public ObservableCollection<string> CharacterPresetPaths { get; } = new();

    public ObservableCollection<TagSuggestion> TagSuggestions
    {
        get => _tagSuggestions;
        set
        {
            _tagSuggestions = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddNodeCommand { get; }
    public ICommand AddBaseNodeCommand { get; }
    public ICommand AddCharacterNodeCommand { get; }
    public ICommand AddBaseConcatNodeCommand { get; }
    public ICommand AddGraphNodeCommand { get; }
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
    public ICommand OpenPresetWindowCommand { get; }
    public ICommand OpenSavePresetWindowCommand { get; }
    public ICommand ClearCharacterPresetCommand { get; }
    public ICommand SaveGraphCommand { get; }
    public ICommand LoadGraphCommand { get; }
    public ICommand RefreshPresetsCommand { get; }
    public ICommand ToggleCollapseCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ResetZoomCommand { get; }
    public ICommand GoToBeginNodeCommand { get; }
    public ICommand SelectGraphFileCommand { get; }
    public ICommand CopyNodesCommand { get; }
    public ICommand PasteNodesCommand { get; }
    public ICommand ToggleBypassCommand { get; }
    public ICommand MoveInputUpCommand { get; }
    public ICommand MoveInputDownCommand { get; }

    public NodeGraphViewModel(
        MainViewModel mainViewModel,
        INovelAiService novelAiService,
        IImageService imageService,
        CharacterPresetService presetService,
        ImageGenerationWorkflow imageGenerationWorkflow)
    {
        _mainViewModel = mainViewModel;
        _novelAiService = novelAiService;
        _imageService = imageService;
        _presetService = presetService;
        _tagSuggestionService = new TagSuggestionService(_novelAiService);
        _imageGenerationWorkflow = imageGenerationWorkflow;

        AddNodeCommand = new RelayCommand(ExecuteAddNode);
        AddBaseNodeCommand = new RelayCommand(ExecuteAddBaseNode);
        AddCharacterNodeCommand = new RelayCommand(ExecuteAddCharacterNode);
        AddBaseConcatNodeCommand = new RelayCommand(ExecuteAddBaseConcatNode);
        AddGraphNodeCommand = new RelayCommand(ExecuteAddGraphNode);
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
        UpdateCharacterPresetCommand = new RelayCommand(ExecuteUpdateCharacterPreset, CanExecuteNodePresetAction);
        LoadCharacterPresetCommand = new RelayCommand(ExecuteLoadCharacterPreset);
        OpenPresetWindowCommand = new RelayCommand(ExecuteOpenPresetWindow);
        OpenSavePresetWindowCommand = new RelayCommand(ExecuteOpenSavePresetWindow);
        ClearCharacterPresetCommand = new RelayCommand(ExecuteClearCharacterPreset, CanExecuteNodePresetAction);
        SaveGraphCommand = new RelayCommand(ExecuteSaveGraph);
        LoadGraphCommand = new RelayCommand(ExecuteLoadGraph);
        RefreshPresetsCommand = new RelayCommand(_ => LoadPresets());
        ToggleCollapseCommand = new RelayCommand(ExecuteToggleCollapse);
        ZoomInCommand = new RelayCommand(_ => ZoomScale = Math.Min(3.0, ZoomScale + 0.1));
        ZoomOutCommand = new RelayCommand(_ => ZoomScale = Math.Max(0.2, ZoomScale - 0.1));
        ResetZoomCommand = new RelayCommand(_ => ZoomScale = 1.0);
        GoToBeginNodeCommand = new RelayCommand(ExecuteGoToBeginNode);
        SelectGraphFileCommand = new RelayCommand(ExecuteSelectGraphFile);
        CopyNodesCommand = new RelayCommand(ExecuteCopyNodes);
        PasteNodesCommand = new RelayCommand(ExecutePasteNodes);
        ToggleBypassCommand = new RelayCommand(ExecuteToggleBypass);
        MoveInputUpCommand = new RelayCommand(ExecuteMoveInputUp);
        MoveInputDownCommand = new RelayCommand(ExecuteMoveInputDown);

        // Listen for node changes to update connections BEFORE adding initial nodes
        Nodes.CollectionChanged += Nodes_CollectionChanged;

        // Add initial nodes
        InitializePermanentNodes();
        LoadPresets();
    }

    private void LoadPresets()
    {
        CharacterPresetPaths.Clear();
        foreach (var path in _presetService.GetPresetPaths())
        {
            CharacterPresetPaths.Add(path);
        }
    }

    private void FlattenPresets(List<CharacterPreset> nodes, string currentPath, ObservableCollection<string> result)
    {
        foreach (var node in nodes)
        {
            string path = string.IsNullOrEmpty(currentPath) ? node.Name : $"{currentPath}/{node.Name}";
            if (node.IsFolder)
            {
                FlattenPresets(node.Children, path, result);
            }
            else
            {
                result.Add(path);
            }
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
        else if (e.PropertyName == nameof(GenerationNode.PresetName))
        {
            CommandManager.InvalidateRequerySuggested();
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

                // Update InputNodes for BaseConcat
                if (node.NextNode.Type == NodeType.BaseConcat)
                {
                    UpdateBaseConcatInputs(node.NextNode);
                }
            }

            // Handle multiple connections (Character/Base nodes)
            foreach (var target in node.NextNodes)
            {
                if (Nodes.Contains(target))
                {
                    Connections.Add(new ConnectionViewModel(node, target));

                    // Update InputNodes for BaseConcat
                    if (target.Type == NodeType.BaseConcat)
                    {
                        UpdateBaseConcatInputs(target);
                    }
                }
            }
        }
    }

    private void UpdateBaseConcatInputs(GenerationNode concatNode)
    {
        // Find all nodes connecting to this BaseConcat node
        var incoming = Nodes.Where(n => n.NextNode == concatNode || n.NextNodes.Contains(concatNode)).ToList();

        // Remove nodes that are no longer connected
        var toRemove = concatNode.InputNodes.Where(n => !incoming.Contains(n)).ToList();
        foreach (var n in toRemove)
        {
            concatNode.InputNodes.Remove(n);
            if (concatNode.InputOrder.Contains(n.Id))
            {
                concatNode.InputOrder.Remove(n.Id);
            }
        }

        // Add new nodes
        foreach (var n in incoming)
        {
            if (!concatNode.InputNodes.Contains(n))
            {
                concatNode.InputNodes.Add(n);
                if (!concatNode.InputOrder.Contains(n.Id))
                {
                    concatNode.InputOrder.Add(n.Id);
                }
            }
        }

        // Sort InputNodes based on InputOrder
        var sorted = concatNode.InputNodes.OrderBy(n => concatNode.InputOrder.IndexOf(n.Id)).ToList();
        concatNode.InputNodes.Clear();
        foreach (var n in sorted)
        {
            concatNode.InputNodes.Add(n);
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

    public double ZoomScale
    {
        get => _zoomScale;
        set
        {
            _zoomScale = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelecting
    {
        get => _isSelecting;
        set { _isSelecting = value; OnPropertyChanged(); }
    }

    public double SelectionX
    {
        get => _selectionX;
        set { _selectionX = value; OnPropertyChanged(); }
    }

    public double SelectionY
    {
        get => _selectionY;
        set { _selectionY = value; OnPropertyChanged(); }
    }

    public double SelectionWidth
    {
        get => _selectionWidth;
        set { _selectionWidth = value; OnPropertyChanged(); }
    }

    public double SelectionHeight
    {
        get => _selectionHeight;
        set { _selectionHeight = value; OnPropertyChanged(); }
    }

    public void UpdateTempConnection(double x, double y)
    {
        if (IsConnecting)
        {
            TempX2 = x;
            TempY2 = y;
        }
    }

    public void UpdateSelectionBox(double x, double y, double width, double height)
    {
        SelectionX = x;
        SelectionY = y;
        SelectionWidth = width;
        SelectionHeight = height;
    }

    public void SelectNodesInArea(double x, double y, double width, double height)
    {
        foreach (var node in Nodes)
        {
            // Simple AABB collision detection
            bool isInside = node.UiX < x + width &&
                            node.UiX + node.Width > x &&
                            node.UiY < y + height &&
                            node.UiY + node.Height > y;

            node.IsSelected = isInside;
        }
    }

    public void ClearSelection()
    {
        foreach (var node in Nodes)
        {
            node.IsSelected = false;
        }
    }

    private void ExecuteAddNode(object? parameter)
    {
        var node = new GenerationNode
        {
            UiX = 200,
            UiY = 200,
            BasePrompt = "",
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

    private void ExecuteAddGraphNode(object? parameter)
    {
        var node = new GenerationNode
        {
            UiX = 200,
            UiY = 200,
            BasePrompt = "Select Graph File...",
            Type = NodeType.Graph
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
        var nodesToDelete = new List<GenerationNode>();

        if (parameter is GenerationNode node)
        {
            nodesToDelete.Add(node);
        }
        else
        {
            // Delete all selected nodes
            nodesToDelete.AddRange(Nodes.Where(n => n.IsSelected));
        }

        foreach (var n in nodesToDelete)
        {
            // Prevent deletion of Begin and End nodes
            if (n.Type == NodeType.Begin || n.Type == NodeType.End)
            {
                continue;
            }

            // Remove connections to this node
            foreach (var other in Nodes)
            {
                if (other.NextNode == n)
                {
                    other.NextNode = null;
                }
                if (other.NextNodes.Contains(n))
                {
                    other.NextNodes.Remove(n);
                }
            }
            Nodes.Remove(n);
        }
        UpdateConnections();
    }

    private void ExecuteDuplicateNode(object? parameter)
    {
        if (parameter is not GenerationNode node) return;
        if (node.Type is NodeType.Begin or NodeType.End) return;

        Nodes.Add(_nodeGraphService.CloneNode(node));
    }

    private void ExecuteCopyNodes(object? parameter)
    {
        var selectedNodes = Nodes.Where(n => n.IsSelected && n.Type != NodeType.Begin && n.Type != NodeType.End).ToList();
        if (selectedNodes.Any())
        {
            string json = JsonSerializer.Serialize(_nodeGraphService.CreateClipboardData(selectedNodes));
            System.Windows.Clipboard.SetText(json);
        }
    }

    private void ExecutePasteNodes(object? parameter)
    {
        try
        {
            string json = System.Windows.Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(json)) return;

            var saveData = JsonSerializer.Deserialize<NodeGraphSaveData>(json);
            if (saveData == null || saveData.Nodes.Count == 0) return;

            var idMap = _nodeGraphService.CloneNodes(saveData.Nodes);

            foreach (var newNode in idMap.Values)
            {
                Nodes.Add(newNode);
                newNode.IsSelected = true;
            }

            foreach (var node in Nodes)
            {
                if (!idMap.ContainsValue(node)) node.IsSelected = false;
            }

            _nodeGraphService.RestoreConnections(idMap.Values.ToList(), saveData.Connections);

            UpdateConnections();
        }
        catch
        {
            // Ignore invalid clipboard data
        }
    }

    private void ExecuteToggleBypass(object? parameter)
    {
        if (parameter is GenerationNode node)
        {
            node.IsBypassed = !node.IsBypassed;
        }
        else
        {
            foreach (var n in Nodes.Where(n => n.IsSelected))
            {
                n.IsBypassed = !n.IsBypassed;
            }
        }
    }

    private void ExecuteStartConnection(object? parameter)
    {
        if (parameter is not GenerationNode node) return;
        if (node.Type == NodeType.End) return; // End node cannot have output

        IsConnecting = true;
        _connectingSource = node;

        // Set initial temp line position (Right side center)
        TempX1 = node.UiX + node.Width;
        TempY1 = node.UiY + (node.Height / 2);
        TempX2 = TempX1;
        TempY2 = TempY1;
    }

    private void ExecuteCompleteConnection(object? parameter)
    {
        if (!IsConnecting || _connectingSource == null || parameter is not GenerationNode targetNode) return;

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
            if (_connectingSource.Type is NodeType.Character or NodeType.Base or NodeType.BaseConcat)
            {
                if (!_connectingSource.NextNodes.Contains(targetNode))
                {
                    _connectingSource.NextNodes.Add(targetNode);
                    UpdateConnections();
                }
            }
            else
            {
                _connectingSource.NextNode = targetNode;
            }
        }

        IsConnecting = false;
        _connectingSource = null;
    }

    private void ExecuteCancelConnection(object? parameter)
    {
        IsConnecting = false;
        _connectingSource = null;
    }

    private void ExecuteClearConnections(object? parameter)
    {
        if (parameter is not GenerationNode node) return;

        node.NextNode = null;
        node.NextNodes.Clear();
        UpdateConnections();
    }

    private void ExecuteDisconnectInput(object? parameter)
    {
        if (parameter is not GenerationNode targetNode) return;

        foreach (var node in Nodes)
        {
            if (node.NextNode == targetNode)
            {
                node.NextNode = null;
            }

            node.NextNodes.Remove(targetNode);
        }
        UpdateConnections();
    }

    private void ExecuteSaveCharacterPreset(object? parameter)
    {
        if (parameter is not GenerationNode { Type: NodeType.Character } node) return;

        string path = node.PresetName;

        if (string.IsNullOrWhiteSpace(path))
        {
            path = $"Character_{DateTime.Now:yyyyMMdd_HHmmss}";
            if (!string.IsNullOrWhiteSpace(node.BasePrompt) && node.BasePrompt.Length < 20)
            {
                path = node.BasePrompt;
            }
            node.PresetName = path; // Update node's preset path
        }

        var preset = CharacterPresetService.CreatePreset(
            node.BasePrompt,
            node.NegativePrompt,
            node.CharX,
            node.CharY);

        _presetService.SavePreset(path, preset);
        LoadPresets(); // Refresh list
        MessageBox.Show($"Saved preset: {path}");
    }

    private bool CanExecuteNodePresetAction(object? parameter)
    {
        if (parameter is object[] { Length: 2 } args)
        {
            if (args[0] is GenerationNode node)
            {
                 return node.IsPresetSelected;
            }
        }
        else if (parameter is GenerationNode singleNode)
        {
            return singleNode.IsPresetSelected;
        }
        return false;
    }

    private void ExecuteUpdateCharacterPreset(object? parameter)
    {
        // This command is intended to update an existing preset selected in the ComboBox
        // We need both the Node (source of data) and the Selected Preset Path (target to update)

        if (parameter is not object[] { Length: 2 } args) return;

        if (args[0] is GenerationNode node && args[1] is string selectedPath)
        {
            var presetData = CharacterPresetService.CreatePreset(
                node.BasePrompt,
                node.NegativePrompt,
                node.CharX,
                node.CharY);

            _presetService.SavePreset(selectedPath, presetData);
            LoadPresets();
            MessageBox.Show($"Updated preset: {selectedPath}");
        }
        else
        {
            MessageBox.Show("Please select a preset to update.");
        }
    }

    private void ExecuteLoadCharacterPreset(object? parameter)
    {
        // Now it's handled by OpenPresetWindowCommand mostly,
        // but keeping this if we still want to support typing path and hitting load
        if (parameter is not GenerationNode node) return;

        string path = node.PresetName;
        if (string.IsNullOrWhiteSpace(path)) return;

        var preset = _presetService.FindPresetByPath(path);
        if (preset == null || preset.IsFolder) return;

        node.BasePrompt = preset.Prompt;
        node.NegativePrompt = preset.NegativePrompt;
        node.CharX = preset.X;
        node.CharY = preset.Y;
    }

    private void ExecuteClearCharacterPreset(object? parameter)
    {
         if (parameter is GenerationNode node)
         {
             node.PresetName = string.Empty;
         }
    }

    private void ExecuteOpenPresetWindow(object? parameter)
    {
        if (parameter is not GenerationNode node) return;

        var presets = _presetService.GetPresets();

        var window = new PresetSelectionWindow(presets);

        if (window.ShowDialog() == true && window.SelectedPreset != null)
        {
            node.BasePrompt = window.SelectedPreset.Prompt;
            node.NegativePrompt = window.SelectedPreset.NegativePrompt;
            node.CharX = window.SelectedPreset.X;
            node.CharY = window.SelectedPreset.Y;
            node.PresetName = window.SelectedPreset.FullPath;
        }
    }

    private void ExecuteOpenSavePresetWindow(object? parameter)
    {
        if (parameter is not GenerationNode node) return;

        var presets = _presetService.GetPresets();

        var window = new PresetSaveWindow(presets, node.PresetName);

        if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.SavePath))
        {
            // Update node first so that when LoadPresets causes re-evaluation it works
            node.PresetName = window.SavePath;

            var preset = CharacterPresetService.CreatePreset(
                node.BasePrompt,
                node.NegativePrompt,
                node.CharX,
                node.CharY);

            _presetService.SavePreset(window.SavePath, preset);
            LoadPresets();
            // Force re-evaluate if preset name display changed internally due to LoadPresets
            node.PresetName = window.SavePath;
        }
    }

    private void ExecuteSelectGraphFile(object? parameter)
    {
        if (parameter is not GenerationNode { Type: NodeType.Graph } node) return;
        var dialog = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        node.BasePrompt = dialog.FileName;
        node.Title = Path.GetFileNameWithoutExtension(dialog.FileName);
    }

    private async void ExecuteSaveGraph(object? parameter)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = "json",
            FileName = "graph_layout.json"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            await _nodeGraphService.SaveToFileAsync(dialog.FileName, _nodeGraphService.CreateSaveData(Nodes));
            MessageBox.Show("Graph saved successfully.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving graph: {ex.Message}");
        }
    }

    private async void ExecuteLoadGraph(object? parameter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var saveData = await _nodeGraphService.LoadFromFileAsync(dialog.FileName);

            if (saveData == null) return;
            Nodes.Clear();
            Connections.Clear();

            // Add nodes
            foreach (var node in saveData.Nodes)
            {
                Nodes.Add(node);
            }

            _nodeGraphService.RestoreConnections(Nodes, saveData.Connections);

            UpdateConnections();
            MessageBox.Show("Graph loaded successfully.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading graph: {ex.Message}");
        }
    }

    private void ExecuteToggleCollapse(object? parameter)
    {
        if (parameter is GenerationNode node)
        {
            node.IsCollapsed = !node.IsCollapsed;
        }
    }

    private void ExecuteMoveInputUp(object? parameter)
    {
        if (parameter is not object[] { Length: 2 } args) return;
        if (args[0] is not GenerationNode inputNode || args[1] is not GenerationNode concatNode) return;

        int index = concatNode.InputNodes.IndexOf(inputNode);
        if (index <= 0) return;

        concatNode.InputNodes.Move(index, index - 1);
        concatNode.InputOrder = concatNode.InputNodes.Select(n => n.Id).ToList();
    }

    private void ExecuteMoveInputDown(object? parameter)
    {
        if (parameter is not object[] { Length: 2 } args) return;
        if (args[0] is not GenerationNode inputNode || args[1] is not GenerationNode concatNode) return;

        int index = concatNode.InputNodes.IndexOf(inputNode);
        if (index >= concatNode.InputNodes.Count - 1) return;

        concatNode.InputNodes.Move(index, index + 1);
        concatNode.InputOrder = concatNode.InputNodes.Select(n => n.Id).ToList();
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
            await ProcessNodeChain(startNode);
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

    private async Task ProcessNodeChain(GenerationNode startNode)
    {
        await ProcessNodeChainCore(startNode, Nodes, true);
    }

    private async Task ProcessNodeChainRecursive(GenerationNode startNode, IList<GenerationNode> contextNodes)
    {
        await ProcessNodeChainCore(startNode, contextNodes, false);
    }

    private async Task ProcessNodeChainCore(GenerationNode startNode, IList<GenerationNode> contextNodes, bool reportEndNode)
    {
        var currentNode = startNode.NextNode;

        while (currentNode != null)
        {
            if (currentNode.Type == NodeType.End)
            {
                if (reportEndNode)
                {
                    _mainViewModel.StatusMessage = "Reached End node.";
                }
                break;
            }

            if (currentNode.IsBypassed)
            {
                if (reportEndNode)
                {
                    _mainViewModel.StatusMessage = $"Bypassing node: {currentNode.Title}";
                }
                currentNode = currentNode.NextNode;
                continue;
            }

            if (currentNode.Type == NodeType.Graph)
            {
                await ExecuteSubGraphNode(currentNode);
            }
            else if (currentNode.Type == NodeType.Normal)
            {
                await GenerateImageForNode(currentNode, contextNodes);
            }

            currentNode = currentNode.NextNode;
            await Task.Delay(500);
        }
    }

    private async Task ExecuteSubGraphNode(GenerationNode graphNode)
    {
        _mainViewModel.StatusMessage = $"Executing Sub-Graph: {graphNode.Title}...";

        if (!File.Exists(graphNode.BasePrompt))
        {
            return;
        }

        try
        {
            var saveData = await _nodeGraphService.LoadFromFileAsync(graphNode.BasePrompt);
            if (saveData == null || !saveData.Nodes.Any())
            {
                return;
            }

            _nodeGraphService.RestoreConnections(saveData.Nodes, saveData.Connections);
            var subBeginNode = saveData.Nodes.FirstOrDefault(n => n.Type == NodeType.Begin);
            if (subBeginNode != null)
            {
                await ProcessNodeChainRecursive(subBeginNode, saveData.Nodes);
            }
        }
        catch (Exception ex)
        {
            _mainViewModel.StatusMessage = $"Error executing sub-graph: {ex.Message}";
        }
    }

    private List<GenerationNode> GetEffectiveInputs(GenerationNode targetNode, IList<GenerationNode> contextNodes, HashSet<GenerationNode>? visited = null)
    {
        visited ??= new HashSet<GenerationNode>();
        if (!visited.Add(targetNode)) return new List<GenerationNode>(); // Cycle detection


        var inputs = new List<GenerationNode>();
        // Find nodes that point to targetNode
        var directInputs = contextNodes.Where(n => n.NextNode == targetNode || n.NextNodes.Contains(targetNode)).ToList();

        foreach (var input in directInputs)
        {
            if (input.IsBypassed)
            {
                continue;
            }
            inputs.Add(input);
        }

        return inputs.Distinct().ToList();
    }

    private async Task GenerateImageForNode(GenerationNode currentNode, IList<GenerationNode> contextNodes)
    {
        _mainViewModel.StatusMessage = $"Generating node...";

        // Resolve effective inputs (handling bypassed nodes)
        var incomingNodes = GetEffectiveInputs(currentNode, contextNodes);

        // Handle BaseConcat nodes logic
        // If current node is BaseConcat, update it first
        if (currentNode.Type == NodeType.BaseConcat)
        {
            UpdateBaseConcatNode(currentNode, contextNodes);
        }

        // Update incoming BaseConcat nodes
        foreach (var node in incomingNodes.Where(n => n.Type == NodeType.BaseConcat).ToList())
        {
            UpdateBaseConcatNode(node, contextNodes);
        }

        var nodeRequest = GenerationRequestBuilder.BuildNodeRequest(
            _mainViewModel.Request,
            currentNode,
            incomingNodes,
            _mainViewModel.IsRandomSeed);

        await _imageGenerationWorkflow.GenerateAndSaveAsync(
            nodeRequest,
            _mainViewModel.ApiToken,
            _mainViewModel.SaveDirectory,
            "chain",
            bitmap => _mainViewModel.GeneratedImage = bitmap);

    }

    private void UpdateBaseConcatNode(GenerationNode node, IList<GenerationNode> contextNodes)
    {
        var concatInputs = node.InputNodes.Any() ?
            node.InputNodes.Where(n => !n.IsBypassed).ToList() :
            GetEffectiveInputs(node, contextNodes).Where(n => !n.IsBypassed).ToList();

        // If using InputNodes, we might need to handle bypassed nodes recursively if they are in the list
        if (node.InputNodes.Any())
        {
            var resolvedInputs = new List<GenerationNode>();
            foreach (var input in node.InputNodes)
            {
                if (input.IsBypassed)
                {
                    resolvedInputs.AddRange(GetEffectiveInputs(input, contextNodes));
                }
                else
                {
                    resolvedInputs.Add(input);
                }
            }
            concatInputs = resolvedInputs.Distinct().Where(n => !n.IsBypassed).ToList();
        }

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

        node.BasePrompt = combinedPositive;
        node.NegativePrompt = combinedNegative;
    }

    // View에서 호출할 메서드: 태그 검색
    public void SearchTags(string query)
    {
        _tagSuggestionService.Search(query, _mainViewModel.Request.model, _mainViewModel.ApiToken, TagSuggestions);
    }

    private void ExecuteGoToBeginNode(object? parameter)
    {
        RequestBringIntoView?.Invoke(this, NodeType.Begin);
    }

    public event EventHandler<NodeType>? RequestBringIntoView;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
