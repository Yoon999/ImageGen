using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
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
    private readonly ImageEncodingService _imageEncodingService;

    private string _prompt = string.Empty;
    private string _apiToken = string.Empty;
    private bool _isGenerating;
    private string _statusMessage = "Ready";
    private BitmapImage? _generatedImage;
    private string _saveDirectory;

    private BitmapImage? _exifImage;
    private string _exifData = string.Empty;
    private int _selectedMainTabIndex;

    private string _lastRequestJson = string.Empty;
    private int? _anlasBalance;
    private bool _isRefreshingAnlas;
    private int? _lastAnlasCost;
    private string _generationMode = "Text2Image";
    private string _smeaMode = "none";
    private bool _variety;
    private string _sourceImagePath = string.Empty;
    private string _maskImagePath = string.Empty;
    private BitmapImage? _sourceImagePreview;
    private BitmapImage? _maskImagePreview;
    private double _imageStrength = 0.7;
    private double _imageNoise;
    private bool _addOriginalImage = true;
    private string _characterReferencePath = string.Empty;
    private BitmapImage? _characterReferencePreview;
    private bool _characterReferenceStyleAware = true;
    private double _characterReferenceFidelity = 1.0;

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
        "k_dpmpp_2s_ancestral",
        "k_dpmpp_2m_sde",
        "k_dpmpp_2m",
        "k_dpmpp_sde",
        "ddim"
    };

    public ObservableCollection<string> Models { get; } = new()
    {
        "nai-diffusion-3",
        "nai-diffusion-4-curated-preview",
        "nai-diffusion-4-full",
        "nai-diffusion-4-5-curated",
        "nai-diffusion-4-5-full",
        "nai-diffusion-furry-3"
    };

    public ObservableCollection<string> Schedulers { get; } = new()
    {
        "native",
        "karras",
        "exponential",
        "polyexponential"
    };

    public ObservableCollection<string> SmeaModes { get; } = new()
    {
        "none",
        "SMEA",
        "SMEA+DYN"
    };

    public ObservableCollection<string> GenerationModes { get; } = new()
    {
        "Text2Image",
        "Img2Img",
        "Inpaint"
    };

    public ObservableCollection<VibeReferenceImage> VibeReferences { get; } = new();

    // Node Graph ViewModel
    public NodeGraphViewModel NodeGraphViewModel { get; }
    public DirectorToolsViewModel DirectorToolsViewModel { get; }

    public MainViewModel()
    {
        _novelAiService = new NovelAiApiService();
        _imageService = new ImageService();
        _settingsService = new SettingsService();
        _characterPresetService = new CharacterPresetService();
        _tagSuggestionService = new TagSuggestionService(_novelAiService);
        _imageEncodingService = new ImageEncodingService();
        _imageGenerationWorkflow = new ImageGenerationWorkflow(_novelAiService, _imageService);
        DirectorToolsViewModel = new DirectorToolsViewModel(this, _imageGenerationWorkflow, _imageEncodingService);

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

        var imageInputSettings = settings.ImageInput ?? new ImageInputSettings();
        _generationMode = GenerationModes.Contains(imageInputSettings.GenerationMode)
            ? imageInputSettings.GenerationMode
            : "Text2Image";
        _sourceImagePath = imageInputSettings.SourceImagePath;
        _maskImagePath = imageInputSettings.MaskImagePath;
        _imageStrength = imageInputSettings.Strength;
        _imageNoise = imageInputSettings.Noise;
        _addOriginalImage = imageInputSettings.AddOriginalImage;
        _sourceImagePreview = LoadSavedPreview(_sourceImagePath);
        _maskImagePreview = LoadSavedPreview(_maskImagePath);

        var referenceSettings = settings.References ?? new ReferenceSettings();
        _characterReferencePath = referenceSettings.CharacterReferencePath;
        _characterReferenceStyleAware = referenceSettings.CharacterReferenceStyleAware;
        _characterReferenceFidelity = referenceSettings.CharacterReferenceFidelity;
        _characterReferencePreview = LoadSavedPreview(_characterReferencePath);

        foreach (var savedReference in referenceSettings.VibeReferences ?? new List<VibeReferenceSettings>())
        {
            var reference = new VibeReferenceImage
            {
                FilePath = savedReference.FilePath,
                Preview = LoadSavedPreview(savedReference.FilePath),
                InformationExtracted = savedReference.InformationExtracted,
                Strength = savedReference.Strength
            };
            reference.PropertyChanged += VibeReference_PropertyChanged;
            VibeReferences.Add(reference);
        }

        VibeReferences.CollectionChanged += VibeReferences_CollectionChanged;

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
        RefreshAnlasCommand = new RelayCommand(ExecuteRefreshAnlas, CanExecuteRefreshAnlas);
        OpenSaveDirectoryCommand = new RelayCommand(ExecuteOpenSaveDirectory);
        BrowseSourceImageCommand = new RelayCommand(ExecuteBrowseSourceImage);
        BrowseMaskImageCommand = new RelayCommand(ExecuteBrowseMaskImage);
        BrowseCharacterReferenceCommand = new RelayCommand(ExecuteBrowseCharacterReference);
        AddVibeReferenceCommand = new RelayCommand(ExecuteAddVibeReference);
        RemoveVibeReferenceCommand = new RelayCommand(ExecuteRemoveVibeReference);
        ClearCharacterReferenceCommand = new RelayCommand(_ => ClearCharacterReference());
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

    private void VibeReferences_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (VibeReferenceImage item in e.NewItems)
            {
                item.PropertyChanged += VibeReference_PropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (VibeReferenceImage item in e.OldItems)
            {
                item.PropertyChanged -= VibeReference_PropertyChanged;
            }
        }

        SaveCurrentSettings();
    }

    private void VibeReference_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
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

    public int SelectedMainTabIndex
    {
        get => _selectedMainTabIndex;
        set
        {
            if (_selectedMainTabIndex != value)
            {
                _selectedMainTabIndex = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public int? AnlasBalance
    {
        get => _anlasBalance;
        set
        {
            if (_anlasBalance != value)
            {
                _anlasBalance = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AnlasDisplay));
            }
        }
    }

    public string AnlasDisplay => AnlasBalance.HasValue ? AnlasBalance.Value.ToString("N0") : "-";

    public int? LastAnlasCost
    {
        get => _lastAnlasCost;
        set
        {
            if (_lastAnlasCost != value)
            {
                _lastAnlasCost = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastAnlasCostDisplay));
            }
        }
    }

    public string LastAnlasCostDisplay => LastAnlasCost.HasValue ? LastAnlasCost.Value.ToString("N0") : "-";

    public bool IsRefreshingAnlas
    {
        get => _isRefreshingAnlas;
        set
        {
            if (_isRefreshingAnlas != value)
            {
                _isRefreshingAnlas = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
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

    public string SelectedModel
    {
        get => Request.model;
        set
        {
            if (Request.model != value)
            {
                Request.model = value;
                OnPropertyChanged();
                SaveCurrentSettings();
            }
        }
    }

    public string SelectedScheduler
    {
        get => Request.parameters.noise_schedule;
        set
        {
            if (Request.parameters.noise_schedule != value)
            {
                Request.parameters.noise_schedule = value;
                OnPropertyChanged();
                SaveCurrentSettings();
            }
        }
    }

    public string SmeaMode
    {
        get => _smeaMode;
        set
        {
            if (_smeaMode != value)
            {
                _smeaMode = value;
                OnPropertyChanged();
                SaveCurrentSettings();
            }
        }
    }

    public bool Decrisper
    {
        get => Request.parameters.dynamic_thresholding;
        set
        {
            if (Request.parameters.dynamic_thresholding != value)
            {
                Request.parameters.dynamic_thresholding = value;
                OnPropertyChanged();
                SaveCurrentSettings();
            }
        }
    }

    public bool Variety
    {
        get => _variety;
        set
        {
            if (_variety != value)
            {
                _variety = value;
                OnPropertyChanged();
                SaveCurrentSettings();
            }
        }
    }

    public double UncondScale
    {
        get => Request.parameters.uncond_scale;
        set
        {
            if (Request.parameters.uncond_scale != value)
            {
                Request.parameters.uncond_scale = value;
                OnPropertyChanged();
                SaveCurrentSettings();
            }
        }
    }

    public string GenerationMode
    {
        get => _generationMode;
        set
        {
            if (_generationMode == value) return;
            _generationMode = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
            SaveCurrentSettings();
        }
    }

    public string SourceImagePath
    {
        get => _sourceImagePath;
        set
        {
            if (_sourceImagePath == value) return;
            _sourceImagePath = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
            SaveCurrentSettings();
        }
    }

    public string MaskImagePath
    {
        get => _maskImagePath;
        set
        {
            if (_maskImagePath == value) return;
            _maskImagePath = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
            SaveCurrentSettings();
        }
    }

    public BitmapImage? SourceImagePreview
    {
        get => _sourceImagePreview;
        set { _sourceImagePreview = value; OnPropertyChanged(); }
    }

    public BitmapImage? MaskImagePreview
    {
        get => _maskImagePreview;
        set { _maskImagePreview = value; OnPropertyChanged(); }
    }

    public double ImageStrength
    {
        get => _imageStrength;
        set
        {
            if (_imageStrength == value) return;
            _imageStrength = value;
            OnPropertyChanged();
            SaveCurrentSettings();
        }
    }

    public double ImageNoise
    {
        get => _imageNoise;
        set
        {
            if (_imageNoise == value) return;
            _imageNoise = value;
            OnPropertyChanged();
            SaveCurrentSettings();
        }
    }

    public bool AddOriginalImage
    {
        get => _addOriginalImage;
        set
        {
            if (_addOriginalImage == value) return;
            _addOriginalImage = value;
            OnPropertyChanged();
            SaveCurrentSettings();
        }
    }

    public string CharacterReferencePath
    {
        get => _characterReferencePath;
        set
        {
            if (_characterReferencePath == value) return;
            _characterReferencePath = value;
            OnPropertyChanged();
            SaveCurrentSettings();
        }
    }

    public BitmapImage? CharacterReferencePreview
    {
        get => _characterReferencePreview;
        set { _characterReferencePreview = value; OnPropertyChanged(); }
    }

    public bool CharacterReferenceStyleAware
    {
        get => _characterReferenceStyleAware;
        set
        {
            if (_characterReferenceStyleAware == value) return;
            _characterReferenceStyleAware = value;
            OnPropertyChanged();
            SaveCurrentSettings();
        }
    }

    public double CharacterReferenceFidelity
    {
        get => _characterReferenceFidelity;
        set
        {
            if (_characterReferenceFidelity == value) return;
            _characterReferenceFidelity = value;
            OnPropertyChanged();
            SaveCurrentSettings();
        }
    }

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
    public ICommand RefreshAnlasCommand { get; }
    public ICommand OpenSaveDirectoryCommand { get; }
    public ICommand BrowseSourceImageCommand { get; }
    public ICommand BrowseMaskImageCommand { get; }
    public ICommand BrowseCharacterReferenceCommand { get; }
    public ICommand AddVibeReferenceCommand { get; }
    public ICommand RemoveVibeReferenceCommand { get; }
    public ICommand ClearCharacterReferenceCommand { get; }

    private bool CanExecuteGenerate(object? parameter)
    {
        if (SelectedMainTabIndex == 1)
        {
            return NodeGraphViewModel.GenerateChainCommand.CanExecute(null);
        }

        if (SelectedMainTabIndex == 2)
        {
            return false;
        }

        if (GenerationMode == "Img2Img" && !File.Exists(SourceImagePath))
        {
            return false;
        }

        if (GenerationMode == "Inpaint" && (!File.Exists(SourceImagePath) || !File.Exists(MaskImagePath)))
        {
            return false;
        }

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

    private void ExecuteOpenSaveDirectory(object? parameter)
    {
        try
        {
            Directory.CreateDirectory(SaveDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = SaveDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open save directory: {ex.Message}";
            Logger.LogError("Error opening save directory", ex);
        }
    }

    private void ExecuteBrowseSourceImage(object? parameter)
    {
        var path = SelectImageFile();
        if (path == null) return;
        LoadSourceImage(path);
    }

    public void LoadSourceImage(string path)
    {
        SourceImagePath = path;
        SourceImagePreview = _imageEncodingService.LoadPreview(path);
    }

    private void ExecuteBrowseMaskImage(object? parameter)
    {
        var path = SelectImageFile();
        if (path == null) return;
        LoadMaskImage(path);
    }

    public void LoadMaskImage(string path)
    {
        MaskImagePath = path;
        MaskImagePreview = _imageEncodingService.LoadPreview(path);
    }

    private void ExecuteBrowseCharacterReference(object? parameter)
    {
        var path = SelectImageFile();
        if (path == null) return;
        LoadCharacterReference(path);
    }

    public void LoadCharacterReference(string path)
    {
        CharacterReferencePath = path;
        CharacterReferencePreview = _imageEncodingService.LoadPreview(path);
    }

    private void ExecuteAddVibeReference(object? parameter)
    {
        var path = SelectImageFile();
        if (path == null) return;
        VibeReferences.Add(new VibeReferenceImage
        {
            FilePath = path,
            Preview = _imageEncodingService.LoadPreview(path)
        });
    }

    private void ExecuteRemoveVibeReference(object? parameter)
    {
        if (parameter is VibeReferenceImage reference)
        {
            VibeReferences.Remove(reference);
        }
    }

    private void ClearCharacterReference()
    {
        CharacterReferencePath = string.Empty;
        CharacterReferencePreview = null;
    }

    private static string? SelectImageFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private bool CanExecuteRefreshAnlas(object? parameter)
    {
        return !IsRefreshingAnlas && !string.IsNullOrWhiteSpace(ApiToken);
    }

    private async void ExecuteRefreshAnlas(object? parameter)
    {
        await RefreshAnlasAsync(true);
    }

    public async Task RefreshAnlasAsync(bool updateStatus = true)
    {
        try
        {
            IsRefreshingAnlas = true;
            if (updateStatus)
            {
                StatusMessage = "Refreshing anlas...";
            }

            AnlasBalance = await _novelAiService.GetAnlasAsync(ApiToken);

            if (updateStatus)
            {
                StatusMessage = $"Anlas refreshed: {AnlasDisplay}";
            }
        }
        catch (Exception ex)
        {
            if (updateStatus)
            {
                StatusMessage = $"Failed to refresh anlas: {ex.Message}";
            }

            Logger.LogError("Error refreshing anlas", ex);
        }
        finally
        {
            IsRefreshingAnlas = false;
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
            }).ToList(),
            ImageInput = new ImageInputSettings
            {
                GenerationMode = GenerationMode,
                SourceImagePath = SourceImagePath,
                MaskImagePath = MaskImagePath,
                Strength = ImageStrength,
                Noise = ImageNoise,
                AddOriginalImage = AddOriginalImage
            },
            References = new ReferenceSettings
            {
                VibeReferences = VibeReferences.Select(reference => new VibeReferenceSettings
                {
                    FilePath = reference.FilePath,
                    InformationExtracted = reference.InformationExtracted,
                    Strength = reference.Strength
                }).ToList(),
                CharacterReferencePath = CharacterReferencePath,
                CharacterReferenceStyleAware = CharacterReferenceStyleAware,
                CharacterReferenceFidelity = CharacterReferenceFidelity
            }
        };
        _settingsService.SaveSettings(settings);
    }

    private BitmapImage? LoadSavedPreview(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            return _imageEncodingService.LoadPreview(filePath);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to restore image preview: {filePath}", ex);
            return null;
        }
    }

    private async void ExecuteGenerate(object? parameter)
    {
        if (SelectedMainTabIndex == 1)
        {
            NodeGraphViewModel.GenerateChainCommand.Execute(null);
            return;
        }

        if (SelectedMainTabIndex == 2)
        {
            return;
        }

        await ExecuteGenerateRefactored();
    }

    private async Task ExecuteGenerateRefactored()
    {
        try
        {
            var uiModel = Request.model;
            var uiSampler = Request.parameters.sampler;
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

            ApplyGeneratorOptions(request);

            Request = GenerationRequestBuilder.Clone(request);
            ClearVolatileRequestData(Request);
            Request.model = uiModel;
            Request.parameters.sampler = uiSampler;
            OnPropertyChanged(nameof(Request));
            OnPropertyChanged(nameof(Seed));

            string currentRequestJson = JsonSerializer.Serialize(Request)
                                        + SourceImagePath
                                        + MaskImagePath
                                        + CharacterReferencePath
                                        + string.Join("|", VibeReferences.Select(r => r.FilePath));
            if (!IsRandomSeed && currentRequestJson == _lastRequestJson)
            {
                var duplicateResult = MessageBox.Show(
                    "The settings are identical to the last generation. Generate anyway?",
                    "Duplicate Settings",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (duplicateResult == MessageBoxResult.No)
                {
                    return;
                }
            }

            IsGenerating = true;
            StatusMessage = "Generating image...";
            SaveCurrentSettings();

            var result = await _imageGenerationWorkflow.GenerateAndSaveWithCostAsync(
                request,
                ApiToken,
                SaveDirectory,
                "img",
                bitmap => GeneratedImage = bitmap);

            LastAnlasCost = result.AnlasCost;

            if (result.FileName == null)
            {
                return;
            }

            StatusMessage = $"Saved to {result.FileName}";
            _lastRequestJson = currentRequestJson;
            await RefreshAnlasAsync(false);
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

    private void ApplyGeneratorOptions(GenerationRequest request)
    {
        ApplySharedGeneratorOptions(request);
        request.parameters.image = null;
        request.parameters.mask = null;
        request.parameters.strength = null;
        request.parameters.noise = null;
        request.parameters.reference_image_multiple.Clear();
        request.parameters.reference_information_extracted_multiple.Clear();
        request.parameters.reference_strength_multiple.Clear();
        request.parameters.director_reference_images = null;
        request.parameters.director_reference_descriptions = null;
        request.parameters.director_reference_strength_values = null;
        request.parameters.director_reference_secondary_strength_values = null;
        request.parameters.director_reference_information_extracted = null;

        request.action = GenerationMode switch
        {
            "Img2Img" => "img2img",
            "Inpaint" => "infill",
            _ => "generate"
        };

        if (request.action is "img2img" or "infill")
        {
            request.parameters.image = _imageEncodingService.EncodeImageFile(
                SourceImagePath,
                request.parameters.width,
                request.parameters.height);
            request.parameters.strength = ImageStrength;
            request.parameters.noise = request.action == "img2img" ? ImageNoise : null;
        }

        if (request.action == "infill")
        {
            request.parameters.mask = _imageEncodingService.EncodeMaskFile(
                MaskImagePath,
                request.parameters.width,
                request.parameters.height,
                request.model.Contains("nai-diffusion-4", StringComparison.OrdinalIgnoreCase));
            request.parameters.add_original_image = AddOriginalImage;
            if (!request.model.Contains("inpainting", StringComparison.OrdinalIgnoreCase)
                && !request.model.Contains("nai-diffusion-2", StringComparison.OrdinalIgnoreCase))
            {
                request.model = $"{request.model}-inpainting";
            }
        }

        foreach (var reference in VibeReferences.Where(r => File.Exists(r.FilePath)))
        {
            request.parameters.reference_image_multiple.Add(_imageEncodingService.EncodeImageFile(
                reference.FilePath,
                request.parameters.width,
                request.parameters.height));
            request.parameters.reference_information_extracted_multiple.Add(reference.InformationExtracted);
            request.parameters.reference_strength_multiple.Add(reference.Strength);
        }

        if (File.Exists(CharacterReferencePath))
        {
            var referenceImage = _imageEncodingService.EncodeCharacterReferenceFile(
                CharacterReferencePath,
                out _,
                out _);
            request.parameters.director_reference_images = new List<string> { referenceImage };
            request.parameters.director_reference_descriptions = new List<DirectorReferenceDescription>
            {
                new()
                {
                    UseCoords = false,
                    UseOrder = false,
                    LegacyUc = false,
                    Caption = new V4ExternalCaption
                    {
                        BaseCaption = CharacterReferenceStyleAware ? "character&style" : "character"
                    }
                }
            };
            request.parameters.director_reference_strength_values = new List<double> { 1.0 };
            request.parameters.director_reference_secondary_strength_values = new List<double> { 1.0 - CharacterReferenceFidelity };
            request.parameters.director_reference_information_extracted = new List<double> { 1.0 };
        }
    }

    public void ApplySharedGeneratorOptions(GenerationRequest request)
    {
        request.parameters.prompt = Prompt;
        request.parameters.negative_prompt = NegativePrompt;
        request.parameters.sm = (SmeaMode == "SMEA" || SmeaMode == "SMEA+DYN") && request.parameters.sampler != "ddim";
        request.parameters.sm_dyn = SmeaMode == "SMEA+DYN" && request.parameters.sampler != "ddim";
        request.parameters.skip_cfg_above_sigma = Variety
            ? CalculateSkipCfgAboveSigma(request.parameters.width, request.parameters.height)
            : null;
        request.parameters.image = null;
        request.parameters.mask = null;
        request.parameters.strength = null;
        request.parameters.noise = null;
        request.parameters.reference_image_multiple.Clear();
        request.parameters.reference_information_extracted_multiple.Clear();
        request.parameters.reference_strength_multiple.Clear();
        request.parameters.director_reference_images = null;
        request.parameters.director_reference_descriptions = null;
        request.parameters.director_reference_strength_values = null;
        request.parameters.director_reference_secondary_strength_values = null;
        request.parameters.director_reference_information_extracted = null;

        if (request.parameters.sampler == "ddim" && !request.model.Contains("nai-diffusion-2", StringComparison.OrdinalIgnoreCase))
        {
            request.parameters.sampler = "ddim_v3";
        }

        if (request.parameters.sampler == "k_euler_ancestral" && request.parameters.noise_schedule != "native")
        {
            request.parameters.deliberate_euler_ancestral_bug = false;
            request.parameters.prefer_brownian = true;
        }
    }

    private static void ClearVolatileRequestData(GenerationRequest request)
    {
        request.parameters.image = null;
        request.parameters.mask = null;
        request.parameters.reference_image_multiple.Clear();
        request.parameters.reference_information_extracted_multiple.Clear();
        request.parameters.reference_strength_multiple.Clear();
        request.parameters.director_reference_images = null;
        request.parameters.director_reference_descriptions = null;
        request.parameters.director_reference_strength_values = null;
        request.parameters.director_reference_secondary_strength_values = null;
        request.parameters.director_reference_information_extracted = null;
    }

    private static double CalculateSkipCfgAboveSigma(int width, int height)
    {
        return Math.Sqrt(width * height / 1011712d) * 19d;
    }

    public async Task<bool> ConfirmCloseAsync()
    {
        return await NodeGraphViewModel.ConfirmCloseAsync();
    }

    public bool ConfirmClose()
    {
        return NodeGraphViewModel.ConfirmClose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
