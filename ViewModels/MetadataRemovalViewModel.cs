using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ImageGen.Helpers;
using ImageGen.Services;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ImageGen.ViewModels;

public sealed class MetadataRemovalViewModel : INotifyPropertyChanged
{
    private readonly ImageMetadataRemovalService _metadataRemovalService;
    private bool _isProcessing;
    private string _statusMessage = "Add or drop images to begin.";

    public MetadataRemovalViewModel(ImageMetadataRemovalService metadataRemovalService)
    {
        _metadataRemovalService = metadataRemovalService;
        AddFilesCommand = new RelayCommand(_ => ExecuteAddFiles(), _ => !IsProcessing);
        ClearCommand = new RelayCommand(_ => Clear(), _ => Items.Count > 0 && !IsProcessing);
        ProcessCommand = new RelayCommand(ExecuteProcess, _ => Items.Count > 0 && !IsProcessing);
    }

    public ObservableCollection<MetadataRemovalItem> Items { get; } = new();

    public bool IsProcessing
    {
        get => _isProcessing;
        private set
        {
            if (_isProcessing == value) return;
            _isProcessing = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value) return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddFilesCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand ProcessCommand { get; }

    public void AddFiles(IEnumerable<string> filePaths)
    {
        if (IsProcessing) return;

        var existingPaths = Items
            .Select(item => item.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        int addedCount = 0;
        int unsupportedCount = 0;

        foreach (string filePath in filePaths)
        {
            if (!ImageMetadataRemovalService.IsSupportedImagePath(filePath))
            {
                unsupportedCount++;
                continue;
            }

            string fullPath = Path.GetFullPath(filePath);
            if (!existingPaths.Add(fullPath)) continue;

            Items.Add(new MetadataRemovalItem(fullPath));
            addedCount++;
        }

        StatusMessage = addedCount > 0
            ? $"{Items.Count} image(s) ready."
            : unsupportedCount > 0
                ? "No supported image files were added."
                : "The selected images are already in the list.";
        CommandManager.InvalidateRequerySuggested();
    }

    private void ExecuteAddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select images to clean",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp|All files|*.*",
            Multiselect = true,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            AddFiles(dialog.FileNames);
        }
    }

    private void Clear()
    {
        Items.Clear();
        StatusMessage = "Add or drop images to begin.";
        CommandManager.InvalidateRequerySuggested();
    }

    private async void ExecuteProcess(object? parameter)
    {
        IsProcessing = true;
        int succeeded = 0;
        int failed = 0;

        try
        {
            foreach (MetadataRemovalItem item in Items)
            {
                item.Status = "Processing...";
                item.OutputPath = string.Empty;

                try
                {
                    ImageMetadataRemovalResult result = await Task.Run(
                        () => _metadataRemovalService.RemoveMetadata(item.FilePath));
                    item.OutputPath = result.OutputPath;
                    item.Status = "Completed";
                    succeeded++;
                }
                catch (Exception ex)
                {
                    item.Status = $"Failed: {ex.Message}";
                    Logger.LogError($"Failed to remove image metadata: {item.FilePath}", ex);
                    failed++;
                }
            }

            StatusMessage = $"Completed: {succeeded}, Failed: {failed}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class MetadataRemovalItem : INotifyPropertyChanged
{
    private string _status = "Ready";
    private string _outputPath = string.Empty;

    public MetadataRemovalItem(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
    }

    public string FilePath { get; }
    public string FileName { get; }

    public string Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (_outputPath == value) return;
            _outputPath = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
