using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ImageGen.Helpers;
using ImageGen.Models.Api;
using ImageGen.Services;
using Clipboard = System.Windows.Clipboard;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ImageGen.ViewModels;

public class DirectorToolsViewModel : INotifyPropertyChanged
{
    private readonly MainViewModel _mainViewModel;
    private readonly ImageGenerationWorkflow _workflow;
    private readonly ImageEncodingService _imageEncodingService;
    private string _selectedTool = "bg-removal";
    private string _inputImagePath = string.Empty;
    private BitmapImage? _inputPreview;
    private BitmapImage? _resultImage;
    private bool _ignoreErrors;
    private int _defry;
    private string _prompt = string.Empty;
    private string _selectedMood = "neutral";
    private string _selectedEmotionStrength = "normal";
    private bool _isRunning;

    public DirectorToolsViewModel(
        MainViewModel mainViewModel,
        ImageGenerationWorkflow workflow,
        ImageEncodingService imageEncodingService)
    {
        _mainViewModel = mainViewModel;
        _workflow = workflow;
        _imageEncodingService = imageEncodingService;

        BrowseInputImageCommand = new RelayCommand(ExecuteBrowseInputImage);
        RunToolCommand = new RelayCommand(ExecuteRunTool, CanRunTool);
        CopyResultCommand = new RelayCommand(_ => ExecuteCopyResult(), _ => ResultImage != null);
        OpenSaveDirectoryCommand = new RelayCommand(_ => _mainViewModel.OpenSaveDirectoryCommand.Execute(null));
    }

    public ObservableCollection<string> Tools { get; } = new()
    {
        "bg-removal",
        "lineart",
        "sketch",
        "colorize",
        "emotion",
        "declutter"
    };

    public ObservableCollection<string> Moods { get; } = new()
    {
        "neutral", "happy", "sad", "angry", "scared", "surprised", "tired", "excited",
        "nervous", "thinking", "confused", "shy", "disgusted", "smug", "bored",
        "laughing", "irritated", "aroused", "embarrassed", "worried", "love",
        "determined", "hurt", "playful"
    };

    public ObservableCollection<string> EmotionStrengths { get; } = new()
    {
        "normal", "slightly_weak", "weak", "even_weaker", "very_weak", "weakest"
    };

    public string SelectedTool
    {
        get => _selectedTool;
        set { _selectedTool = value; OnPropertyChanged(); }
    }

    public string InputImagePath
    {
        get => _inputImagePath;
        set { _inputImagePath = value; OnPropertyChanged(); }
    }

    public BitmapImage? InputPreview
    {
        get => _inputPreview;
        set { _inputPreview = value; OnPropertyChanged(); }
    }

    public BitmapImage? ResultImage
    {
        get => _resultImage;
        set
        {
            _resultImage = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IgnoreErrors
    {
        get => _ignoreErrors;
        set { _ignoreErrors = value; OnPropertyChanged(); }
    }

    public int Defry
    {
        get => _defry;
        set { _defry = value; OnPropertyChanged(); }
    }

    public string Prompt
    {
        get => _prompt;
        set { _prompt = value; OnPropertyChanged(); }
    }

    public string SelectedMood
    {
        get => _selectedMood;
        set { _selectedMood = value; OnPropertyChanged(); }
    }

    public string SelectedEmotionStrength
    {
        get => _selectedEmotionStrength;
        set { _selectedEmotionStrength = value; OnPropertyChanged(); }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            _isRunning = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public ICommand BrowseInputImageCommand { get; }
    public ICommand RunToolCommand { get; }
    public ICommand CopyResultCommand { get; }
    public ICommand OpenSaveDirectoryCommand { get; }

    public void LoadInputImage(string filePath)
    {
        InputImagePath = filePath;
        InputPreview = _imageEncodingService.LoadPreview(filePath);
    }

    private void ExecuteBrowseInputImage(object? parameter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadInputImage(dialog.FileName);
        }
    }

    private bool CanRunTool(object? parameter)
    {
        return !IsRunning
               && !string.IsNullOrWhiteSpace(_mainViewModel.ApiToken)
               && File.Exists(InputImagePath);
    }

    private async void ExecuteRunTool(object? parameter)
    {
        try
        {
            IsRunning = true;
            _mainViewModel.StatusMessage = $"Running {SelectedTool}...";

            var (width, height) = _imageEncodingService.GetImageSize(InputImagePath);
            var request = new AugmentImageRequest
            {
                req_type = SelectedTool,
                width = width,
                height = height,
                image = _imageEncodingService.EncodeImageFile(InputImagePath, width, height)
            };

            if (SelectedTool == "colorize")
            {
                request.defry = Defry;
                request.prompt = Prompt;
            }
            else if (SelectedTool == "emotion")
            {
                request.defry = EmotionStrengths.IndexOf(SelectedEmotionStrength);
                request.prompt = $"{SelectedMood};;{Prompt}";
            }

            var result = await _workflow.AugmentAndSaveAsync(
                request,
                _mainViewModel.ApiToken,
                _mainViewModel.SaveDirectory,
                SelectedTool,
                bitmap => ResultImage = bitmap);

            _mainViewModel.LastAnlasCost = result.AnlasCost;
            await _mainViewModel.RefreshAnlasAsync(false);
            _mainViewModel.StatusMessage = result.FileName == null
                ? $"{SelectedTool} finished without output"
                : $"Saved to {result.FileName}";
        }
        catch (Exception ex)
        {
            _mainViewModel.StatusMessage = $"Director tool error: {ex.Message}";
            Logger.LogError("Director tool failed", ex);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void ExecuteCopyResult()
    {
        if (ResultImage == null)
        {
            return;
        }

        Clipboard.SetImage(ResultImage);
        _mainViewModel.StatusMessage = "Director result copied to clipboard";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
