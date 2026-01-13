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
using MessageBox = System.Windows.MessageBox;

namespace ImageGen.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly INovelAiService _novelAiService;
    private readonly IImageService _imageService;
    private readonly SettingsService _settingsService;
    
    private string _prompt = string.Empty;
    private string _apiToken = string.Empty;
    private bool _isGenerating;
    private string _statusMessage = "Ready";
    private BitmapImage? _generatedImage;
    private string _saveDirectory;
    
    // EXIF Viewer 관련
    private BitmapImage? _exifImage;
    private string _exifData = string.Empty;
    
    // 중복 생성 방지용
    private string _lastRequestJson = string.Empty;
    
    // 태그 자동완성 관련
    private CancellationTokenSource? _debounceCts;
    private ObservableCollection<TagSuggestion> _tagSuggestions = new();
    private TagSuggestion? _selectedSuggestion;
    private bool _isUpdatingPrompt;

    // Seed 관련
    private bool _isRandomSeed = true;

    // Character Prompts
    public ObservableCollection<CharacterPromptViewModel> CharacterPrompts { get; } = new();

    // Sampler 목록
    public ObservableCollection<string> Samplers { get; } = new ObservableCollection<string>
    {
        "k_euler_ancestral",
        "k_euler",
        "k_dpmpp_2s_ancestral"
    };

    public MainViewModel()
    {
        _novelAiService = new NovelAiApiService();
        _imageService = new ImageService();
        _settingsService = new SettingsService();
        
        // 설정 로드
        var settings = _settingsService.LoadSettings();
        _apiToken = settings.ApiToken;
        _saveDirectory = string.IsNullOrWhiteSpace(settings.SaveDirectory) 
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output") 
            : settings.SaveDirectory;
        _prompt = settings.LastPrompt;
        
        // 파라미터 복원 (null 체크)
        if (settings.LastParameters != null)
        {
            Request.parameters = settings.LastParameters;
            _isRandomSeed = settings.IsRandomSeed;
        }
        
        // 캐릭터 프롬프트 복원
        if (settings.CharacterPrompts != null)
        {
            foreach (var charSettings in settings.CharacterPrompts)
            {
                var charViewModel = new CharacterPromptViewModel
                {
                    Prompt = charSettings.Prompt,
                    NegativePrompt = charSettings.NegativePrompt,
                    X = charSettings.X,
                    Y = charSettings.Y
                };
                // 각 캐릭터 ViewModel의 변경 사항도 감지하여 저장
                charViewModel.PropertyChanged += (s, e) => SaveCurrentSettings();
                CharacterPrompts.Add(charViewModel);
            }
        }
        
        // CharacterPrompts 컬렉션 변경 감지 (추가/삭제 시 저장)
        CharacterPrompts.CollectionChanged += CharacterPrompts_CollectionChanged;
        
        // 기본값 설정 (만약 로드된 값이 없거나 비어있다면)
        if (string.IsNullOrEmpty(Request.parameters.uc))
        {
            Request.parameters.uc = "low quality, worst quality, jpeg artifacts, 2::signature, watermark, copyright name, artist name, logo, artist logo, weibo username, twitter username::, mosaic censoring, bar censor, censored, lowres, bad anatomy, bad hands, abstract, 2::multiple views::, deformed, \nhair flaps, armpit hair, shota, loli, beard,\n\nalphes (style), zun (style), toriyama akira (style), \nasanagi, bkub, bb_(baalbuddy), neocoill, gaoo_(frpjx283), yukito_(dreamrider), konoshige_(ryuun), milkpanda, nameo_(judgemasterkou)";
        }
        
        // Sampler 기본값 확인
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
                // 이벤트 핸들러 제거는 선택사항이지만 메모리 누수 방지를 위해 권장됨
                // 여기서는 람다식이라 정확한 제거가 어렵지만, ViewModel 수명이 짧으므로 큰 문제는 아님
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
                SaveCurrentSettings(); // 변경 시 저장
                // OnPromptChanged() 제거: View에서 처리
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
            // 선택 시 로직은 View에서 처리하므로 여기서는 제거
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
                SaveCurrentSettings(); // 변경 시 저장
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
                SaveCurrentSettings(); // 변경 시 저장
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
            SaveCurrentSettings(); // 변경 시 저장
        }
    }

    // CfgRescale 바인딩 속성
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

    // Negative Prompt 바인딩 속성
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

    // SelectedSampler 바인딩 속성
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
    
    // EXIF Viewer 속성
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

    private bool CanExecuteGenerate(object? parameter)
    {
        return !string.IsNullOrWhiteSpace(ApiToken) && !string.IsNullOrWhiteSpace(Prompt) && !IsGenerating;
    }

    private void ExecuteSelectFolder(object? parameter)
    {
        using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
        {
            dialog.Description = "Select a folder to save generated images";
            dialog.UseDescriptionForTitle = true;
            dialog.SelectedPath = SaveDirectory;
            dialog.ShowNewFolderButton = true;

            System.Windows.Forms.DialogResult result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                SaveDirectory = dialog.SelectedPath;
            }
        }
    }

    private void ExecuteRandomizeSeed(object? parameter)
    {
        var random = new Random();
        long newSeed = random.NextInt64(1, 9999999999); 
        Request.parameters.seed = newSeed;
        IsRandomSeed = false; 
        OnPropertyChanged(nameof(Request));
        SaveCurrentSettings(); // 변경 시 저장
    }
    
    private void ExecuteLoadExifImage(object? parameter)
    {
        // 파일 열기 다이얼로그 (WPF용)
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
        CharacterPrompts.Add(new CharacterPromptViewModel());
    }

    private void ExecuteRemoveCharacter(object? parameter)
    {
        if (parameter is CharacterPromptViewModel character)
        {
            CharacterPrompts.Remove(character);
        }
    }

    public void LoadExifImage(string filePath)
    {
        try
        {
            // 이미지 표시
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            ExifImage = bitmap;

            // 메타데이터 추출
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
                SaveCurrentSettings(); // 변경 시 저장
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
                Y = cp.Y
            }).ToList()
        };
        _settingsService.SaveSettings(settings);
    }

    private async void ExecuteGenerate(object? parameter)
    {
        try
        {
            // V4 모델인지 확인 (nai-diffusion-4 포함 여부)
            bool isV4 = Request.model.Contains("nai-diffusion-4");

            if (isV4)
            {
                // V4 모델일 경우 v4_prompt 구조체 설정
                var v4Prompt = new V4ConditionInput
                {
                    Caption = new V4ExternalCaption
                    {
                        BaseCaption = Prompt
                    },
                    UseCoords = true, 
                    UseOrder = true   
                };

                // Negative Prompt 설정 (사용자 입력값 사용)
                var v4NegativePrompt = new V4ConditionInput
                {
                    Caption = new V4ExternalCaption
                    {
                        BaseCaption = NegativePrompt // 사용자가 입력한 Negative Prompt 사용
                    },
                    UseCoords = false,
                    UseOrder = false
                };

                // 캐릭터 프롬프트 추가
                foreach (var charPrompt in CharacterPrompts)
                {
                    // Positive Prompt 추가
                    if (!string.IsNullOrWhiteSpace(charPrompt.Prompt))
                    {
                        v4Prompt.Caption.CharCaptions.Add(new V4ExternalCharacterCaption
                        {
                            CharCaption = charPrompt.Prompt,
                            Centers = new List<Coordinates>
                            {
                                new Coordinates { x = charPrompt.X, y = charPrompt.Y }
                            }
                        });
                    }

                    // Negative Prompt 추가 (캐릭터별 Negative Prompt가 있는 경우)
                    if (!string.IsNullOrWhiteSpace(charPrompt.NegativePrompt))
                    {
                        v4NegativePrompt.Caption.CharCaptions.Add(new V4ExternalCharacterCaption
                        {
                            CharCaption = charPrompt.NegativePrompt,
                            Centers = new List<Coordinates>
                            {
                                new Coordinates { x = charPrompt.X, y = charPrompt.Y }
                            }
                        });
                    }
                }

                Request.parameters.V4Prompt = v4Prompt;
                Request.parameters.V4NegativePrompt = v4NegativePrompt;
                
                // V4 파라미터 설정
                Request.parameters.noise_schedule = "karras";
                // CfgRescale은 바인딩된 값 사용
                Request.parameters.prefer_brownian = true;
                // uc는 이미 바인딩되어 있지만 명시적으로 확인
                
                Request.input = Prompt; 
            }
            else
            {
                // V3 이하 모델
                Request.input = Prompt;
                Request.parameters.V4Prompt = null;
                Request.parameters.V4NegativePrompt = null;
            }
            
            // 랜덤 시드 처리: 클라이언트에서 직접 생성하여 전송
            if (IsRandomSeed)
            {
                var random = new Random();
                Request.parameters.seed = random.NextInt64(1, 9999999999);
                OnPropertyChanged(nameof(Seed)); // UI 갱신
            }
            
            string currentRequestJson = JsonSerializer.Serialize(Request);
            
            // 랜덤 시드일 때는 매번 시드가 바뀌므로 중복 검사가 자연스럽게 통과됨.
            // 고정 시드일 때만 중복 검사 수행
            if (!IsRandomSeed && currentRequestJson == _lastRequestJson)
            {
                var result = MessageBox.Show("The settings are identical to the last generation. Generate anyway?", 
                                             "Duplicate Settings", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            IsGenerating = true;
            StatusMessage = "Generating image...";
            
            // 생성 시작 전 설정 저장 (파라미터 등 최신 상태 유지)
            SaveCurrentSettings();

            byte[]? imageData = null;
            
            // 스트림 처리를 백그라운드 스레드로 이동하여 UI 스레드 블로킹 방지
            await Task.Run(async () =>
            {
                var lastUpdate = DateTime.MinValue;
                await foreach (var zipData in _novelAiService.GenerateImageStreamAsync(Request, ApiToken))
                {
                    imageData = zipData;

                    // 시간 기반 스로틀링 (약 60ms 마다 갱신, 약 15fps)
                    var now = DateTime.Now;
                    if ((now - lastUpdate).TotalMilliseconds < 60) continue;
                    lastUpdate = now;

                    try
                    {
                        var bitmap = _imageService.ConvertToBitmapImage(imageData);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            GeneratedImage = bitmap;
                        });
                    }
                    catch
                    {
                        // ignored
                    }
                }
            });

             if (imageData == null) return;
             GeneratedImage = await Task.Run(() => _imageService.ConvertToBitmapImage(imageData));
             
            if (!Directory.Exists(SaveDirectory))
            {
                try
                {
                    Directory.CreateDirectory(SaveDirectory);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error creating directory: {ex.Message}";
                    Logger.LogError($"Failed to create directory: {SaveDirectory}", ex);
                    return;
                }
            }
            
            string fileName = $"img_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            await _imageService.SaveImageAsync(imageData, SaveDirectory, fileName);

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

    // View에서 호출할 메서드: 태그 검색
    public void SearchTags(string query)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Delay(300, token).ContinueWith(async _ =>
        {
            if (token.IsCancellationRequested) return;
            
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2) 
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => TagSuggestions.Clear());
                return;
            }

            if (string.IsNullOrWhiteSpace(ApiToken)) return;

            try
            {
                var suggestions = await _novelAiService.SuggestTagsAsync(query, Request.model, ApiToken);
                
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    TagSuggestions.Clear();
                    foreach (var tag in suggestions)
                    {
                        TagSuggestions.Add(tag);
                    }
                });
            }
            catch (Exception ex)
            {
                // Logger.LogError("Tag suggestion failed", ex);
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
