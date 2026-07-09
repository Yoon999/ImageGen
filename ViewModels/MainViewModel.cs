using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ImageGen.Helpers;
using ImageGen.Models;
using ImageGen.Models.Api;
using ImageGen.Services;
using ImageGen.Services.Interfaces;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace ImageGen.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly INovelAiService _novelAiService;
    private readonly IImageService _imageService;
    private readonly SettingsService _settingsService;
    private readonly CharacterPresetService _characterPresetService;
    private readonly TagSuggestionService _tagSuggestionService;
    private readonly ImageGenerationWorkflow _imageGenerationWorkflow;

    private string _prompt = string.Empty;
    private string _apiToken = string.Empty;
    private bool _isGenerating;
    private string _statusMessage = "Ready";
    private BitmapImage? _generatedImage;
    private string _saveDirectory;

    private BitmapImage? _exifImage;
    private string _exifData = string.Empty;

    private string _lastRequestJson = string.Empty;

    private ObservableCollection<TagSuggestion> _tagSuggestions = new();
    private TagSuggestion? _selectedSuggestion;
    // private bool _isUpdatingPrompt;

    private bool _isRandomSeed = true;

    // Character Prompts
    public ObservableCollection<CharacterPromptViewModel> CharacterPrompts { get; } = new();
    
    public ObservableCollection<string> Samplers { get; } = new ObservableCollection<string>
    {
        "k_euler_ancestral",
        "k_euler",
        "k_dpmpp_2s_ancestral"
    };

    // Node Graph ViewModel
    public NodeGraphViewModel NodeGraphViewModel { get; }

    public MainViewModel()
    {
        _novelAiService = new NovelAiApiService();
        _imageService = new ImageService();
        _settingsService = new SettingsService();
        _characterPresetService = new CharacterPresetService();
        _tagSuggestionService = new TagSuggestionService(_novelAiService);
        _imageGenerationWorkflow = new ImageGenerationWorkflow(_novelAiService, _imageService);

        // Initialize NodeGraphViewModel
        NodeGraphViewModel = new NodeGraphViewModel(
            this,
            _novelAiService,
            _imageService,
            _characterPresetService,
            _imageGenerationWorkflow);

        var settings = _settingsService.LoadSettings();
        _apiToken = settings.ApiToken;
        _saveDirectory = string.IsNullOrWhiteSpace(settings.SaveDirectory)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output")
            : settings.SaveDirectory;
        _prompt = settings.LastPrompt;

        if (settings.LastParameters != null)
        {
            Request.parameters = settings.LastParameters;
            _isRandomSeed = settings.IsRandomSeed;
        }

        if (settings.CharacterPrompts != null)
        {
            foreach (var charSettings in settings.CharacterPrompts)
            {
                var charViewModel = new CharacterPromptViewModel(_characterPresetService)
                {
                    Prompt = charSettings.Prompt,
                    NegativePrompt = charSettings.NegativePrompt,
                    X = charSettings.X,
                    Y = charSettings.Y
                };

                if (!string.IsNullOrEmpty(charSettings.PresetPath))
                {
                    charViewModel.SelectedPreset = _characterPresetService.FindPresetByPath(charSettings.PresetPath);
                }

                charViewModel.PropertyChanged += (s, e) => SaveCurrentSettings();
                CharacterPrompts.Add(charViewModel);
            }
        }

        CharacterPrompts.CollectionChanged += CharacterPrompts_CollectionChanged;

        if (string.IsNullOrEmpty(Request.parameters.uc))
        {
            Request.parameters.uc = "";
        }

        if (!Samplers.Contains(Request.parameters.sampler))
        {
            Request.parameters.sampler = "k_euler";
        }

        GenerateCommand = new RelayCommand(ExecuteGenerate, CanExecuteGenerate);
        SelectFolderCommand = new RelayCommand(ExecuteSelectFolder);
        RandomizeSeedCommand = new RelayCommand(ExecuteRandomizeSeed);
        LoadExifImageCommand = new RelayCommand(ExecuteLoadExifImage);
        AddCharacterCommand = new RelayCommand(ExecuteAddCharacter);
        RemoveCharacterCommand = new RelayCommand(ExecuteRemoveCharacter);
        CopyImageCommand = new RelayCommand(ExecuteCopyImage, CanExecuteCopyImage);
    }

    private void CharacterPrompts_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (CharacterPromptViewModel item in e.NewItems)
            {
                item.PropertyChanged += (s, args) => SaveCurrentSettings();
            }
        }

        if (e.OldItems != null)
        {
            foreach (CharacterPromptViewModel item in e.OldItems)
            {
            }
        }

        SaveCurrentSettings();
    }

    public string Prompt
    {
        get => _prompt;
        set
        {
            if (_prompt != value)
            {
                _prompt = value;
                OnPropertyChanged();
                SaveCurrentSettings();
            }
        }
    }

    public ObservableCollection<TagSuggestion> TagSuggestions
    {
        get => _tagSuggestions;
        set
        {
            _tagSuggestions = value;
            OnPropertyChanged();
        }
    }

    public TagSuggestion? SelectedSuggestion
    {
        get => _selectedSuggestion;
        set
        {
            _selectedSuggestion = value;
            OnPropertyChanged();
        }
    }

    public string ApiToken
    {
        get => _apiToken;
        set
        {
            if (_apiToken != value)
            {
                _apiToken = value;
                OnPropertyChanged();
                SaveCurrentSettings();
            }
        }
    }

    public string SaveDirectory
    {
        get => _saveDirectory;
        set
        {
            if (_saveDirectory != value)
            {
                _saveDirectory = value;
                OnPropertyChanged();
                SaveCurrentSettings();
            }
        }
    }

    public bool IsGenerating
    {
        get => _isGenerating;
        set
        {
            _isGenerating = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public BitmapImage? GeneratedImage
    {
        get => _generatedImage;
        set
        {
            _generatedImage = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public GenerationRequest Request { get; set; } = new GenerationRequest();

    public bool IsRandomSeed
    {
        get => _isRandomSeed;
        set
        {
            _isRandomSeed = value;
            OnPropertyChanged();
            if (_isRandomSeed)
            {
                Request.parameters.seed = 0;
                OnPropertyChanged(nameof(Request));
            }
            SaveCurrentSettings();
        }
    }

    public double CfgRescale
    {
        get => Request.parameters.cfg_rescale;
        set
        {
            if (Request.parameters.cfg_rescale != value)
            {
                Request.parameters.cfg_rescale = value;
                OnPropertyChanged();
                SaveCurrentSettings();
            }
        }
    }

    public string NegativePrompt
    {
        get => Request.parameters.uc;
        set
        {
            if (Request.parameters.uc != value)
            {
                Request.parameters.uc = value;
                OnPropertyChanged();
                SaveCurrentSettings();
            }
        }
    }

    public string SelectedSampler
    {
        get => Request.parameters.sampler;
        set
        {
            if (Request.parameters.sampler != value)
            {
                Request.parameters.sampler = value;
                OnPropertyChanged();
                SaveCurrentSettings();
            }
        }
    }

    public BitmapImage? ExifImage
    {
        get => _exifImage;
        set
        {
            _exifImage = value;
            OnPropertyChanged();
        }
    }

    public string ExifData
    {
        get => _exifData;
        set
        {
            _exifData = value;
            OnPropertyChanged();
        }
    }

    public ICommand GenerateCommand { get; }
    public ICommand SelectFolderCommand { get; }
    public ICommand RandomizeSeedCommand { get; }
    public ICommand LoadExifImageCommand { get; }
    public ICommand AddCharacterCommand { get; }
    public ICommand RemoveCharacterCommand { get; }
    public ICommand CopyImageCommand { get; }

    private bool CanExecuteGenerate(object? parameter)
    {
        return !string.IsNullOrWhiteSpace(ApiToken) && !string.IsNullOrWhiteSpace(Prompt) && !IsGenerating;
    }

    private void ExecuteSelectFolder(object? parameter)
    {
        using (var dialog = new FolderBrowserDialog())
        {
            dialog.Description = "Select a folder to save generated images";
            dialog.UseDescriptionForTitle = true;
            dialog.SelectedPath = SaveDirectory;
            dialog.ShowNewFolderButton = true;

            DialogResult result = dialog.ShowDialog();

            if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                SaveDirectory = dialog.SelectedPath;
            }
        }
    }

    private void ExecuteRandomizeSeed(object? parameter)
    {
        long newSeed = RandomSeedService.NextSeed();
        Request.parameters.seed = newSeed;
        IsRandomSeed = false;
        OnPropertyChanged(nameof(Request));
        SaveCurrentSettings();
    }

    private void ExecuteLoadExifImage(object? parameter)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            LoadExifImage(openFileDialog.FileName);
        }
    }

    private void ExecuteAddCharacter(object? parameter)
    {
        CharacterPrompts.Add(new CharacterPromptViewModel(_characterPresetService));
    }

    private void ExecuteRemoveCharacter(object? parameter)
    {
        if (parameter is CharacterPromptViewModel character)
        {
            CharacterPrompts.Remove(character);
        }
    }

    private bool CanExecuteCopyImage(object? parameter)
    {
        return GeneratedImage != null;
    }

    private void ExecuteCopyImage(object? parameter)
    {
        if (GeneratedImage != null)
        {
            try
            {
                Clipboard.SetImage(GeneratedImage);
                StatusMessage = "Image copied to clipboard";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to copy image: {ex.Message}";
            }
        }
    }

    public void LoadExifImage(string filePath)
    {
        try
        {
            // ?대?吏 ?쒖떆
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            ExifImage = bitmap;

            // 硫뷀??곗씠??異붿텧
            ExifData = ExifHelper.ExtractMetadata(filePath);
        }
        catch (Exception ex)
        {
            ExifData = $"Error loading image: {ex.Message}";
            Logger.LogError("Error loading EXIF image", ex);
        }
    }

    public long Seed
    {
        get => Request.parameters.seed;
        set
        {
            if (Request.parameters.seed != value)
            {
                Request.parameters.seed = value;
                OnPropertyChanged();
                IsRandomSeed = (value == 0);
                SaveCurrentSettings();
            }
        }
    }

    private void SaveCurrentSettings()
    {
        var settings = new AppSettings
        {
            ApiToken = ApiToken,
            SaveDirectory = SaveDirectory,
            LastPrompt = Prompt,
            IsRandomSeed = IsRandomSeed,
            LastParameters = Request.parameters,
            CharacterPrompts = CharacterPrompts.Select(cp => new CharacterPromptSettings
            {
                Prompt = cp.Prompt,
                NegativePrompt = cp.NegativePrompt,
                X = cp.X,
                Y = cp.Y,
                PresetPath = cp.SelectedPreset?.FullPath ?? string.Empty
            }).ToList()
        };
        _settingsService.SaveSettings(settings);
    }

    private async void ExecuteGenerate(object? parameter)
    {
        await ExecuteGenerateRefactored();
    }

    private async Task ExecuteGenerateRefactored()
    {
        try
        {
            var request = GenerationRequestBuilder.BuildStandaloneRequest(
                Request,
                Prompt,
                NegativePrompt,
                CharacterPrompts.Select(cp => new CharacterPromptSettings
                {
                    Prompt = cp.Prompt,
                    NegativePrompt = cp.NegativePrompt,
                    X = cp.X,
                    Y = cp.Y,
                    PresetPath = cp.SelectedPreset?.FullPath ?? string.Empty
                }),
                IsRandomSeed);

            Request = request;
            OnPropertyChanged(nameof(Request));
            OnPropertyChanged(nameof(Seed));

            string currentRequestJson = JsonSerializer.Serialize(Request);
            if (!IsRandomSeed && currentRequestJson == _lastRequestJson)
            {
                var result = MessageBox.Show(
                    "The settings are identical to the last generation. Generate anyway?",
                    "Duplicate Settings",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            IsGenerating = true;
            StatusMessage = "Generating image...";
            SaveCurrentSettings();

            var fileName = await _imageGenerationWorkflow.GenerateAndSaveAsync(
                Request,
                ApiToken,
                SaveDirectory,
                "img",
                bitmap => GeneratedImage = bitmap);

            if (fileName == null)
            {
                return;
            }

            StatusMessage = $"Saved to {fileName}";
            _lastRequestJson = currentRequestJson;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Logger.LogError("Error during image generation process", ex);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    public void SearchTags(string query)
    {
        _tagSuggestionService.Search(query, Request.model, ApiToken, TagSuggestions);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
