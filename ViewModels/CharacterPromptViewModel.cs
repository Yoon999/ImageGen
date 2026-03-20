using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ImageGen.Models;
using ImageGen.Services;
using ImageGen.Views;

namespace ImageGen.ViewModels;

public class CharacterPromptViewModel : INotifyPropertyChanged
{
    private string _prompt = string.Empty;
    private string _negativePrompt = string.Empty;
    private double _x = 0.5;
    private double _y = 0.5;
    private CharacterPreset? _selectedPreset;
    private string _newPresetPath = string.Empty; 
    
    public ICommand RefreshPresetsCommand { get; }

    public CharacterPromptViewModel()
    {
        LoadPresets();
        
        LoadPresetCommand = new RelayCommand(ExecuteLoadPreset, CanExecutePresetAction);
        UpdatePresetCommand = new RelayCommand(ExecuteUpdatePreset, CanExecutePresetAction);
        SavePresetCommand = new RelayCommand(ExecuteSavePreset);
        DeletePresetCommand = new RelayCommand(ExecuteDeletePreset, CanExecutePresetAction);
        ClearPresetCommand = new RelayCommand(ExecuteClearPreset);
        RefreshPresetsCommand = new RelayCommand(_ => LoadPresets());
        OpenPresetWindowCommand = new RelayCommand(ExecuteOpenPresetWindow);
        OpenSavePresetWindowCommand = new RelayCommand(ExecuteOpenSavePresetWindow);
    }

    public ObservableCollection<CharacterPreset> Presets { get; } = new();
    
    public CharacterPreset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (_selectedPreset != value)
            {
                _selectedPreset = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPresetSelected));
                OnPropertyChanged(nameof(SelectedPresetDisplay));
                CommandManager.InvalidateRequerySuggested();
                
                if (value != null && !value.IsFolder)
                {
                    NewPresetPath = value.FullPath;
                }
            }
        }
    }

    public bool IsPresetSelected => SelectedPreset != null && !SelectedPreset.IsFolder;

    public string SelectedPresetDisplay => IsPresetSelected ? SelectedPreset!.FullPath : "No preset selected";

    public string NewPresetPath
    {
        get => _newPresetPath;
        set
        {
            _newPresetPath = value;
            OnPropertyChanged();
        }
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
            }
        }
    }

    public string NegativePrompt
    {
        get => _negativePrompt;
        set
        {
            if (_negativePrompt != value)
            {
                _negativePrompt = value;
                OnPropertyChanged();
            }
        }
    }

    public double X
    {
        get => _x;
        set
        {
            if (_x != value)
            {
                _x = value;
                OnPropertyChanged();
            }
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            if (_y != value)
            {
                _y = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand LoadPresetCommand { get; }
    public ICommand UpdatePresetCommand { get; }
    public ICommand SavePresetCommand { get; }
    public ICommand DeletePresetCommand { get; }
    public ICommand ClearPresetCommand { get; }
    public ICommand OpenPresetWindowCommand { get; }
    public ICommand OpenSavePresetWindowCommand { get; }

    private void LoadPresets()
    {
        var savedSelectedPath = SelectedPreset?.FullPath;
        Presets.Clear();
        
        var service = new CharacterPresetService();
        var allPresets = service.GetPresets();
        
        foreach(var preset in allPresets)
        {
            Presets.Add(preset);
        }

        if (savedSelectedPath != null)
        {
            SelectedPreset = service.FindPresetByPath(savedSelectedPath);
        }
    }

    private void ApplyPreset(CharacterPreset preset)
    {
        if (preset.IsFolder) return;
        
        Prompt = preset.Prompt;
        NegativePrompt = preset.NegativePrompt;
        X = preset.X;
        Y = preset.Y;
    }

    private bool CanExecutePresetAction(object? parameter)
    {
        return IsPresetSelected;
    }

    private void ExecuteLoadPreset(object? parameter)
    {
        if (SelectedPreset != null)
        {
            ApplyPreset(SelectedPreset);
        }
    }

    private void ExecuteUpdatePreset(object? parameter)
    {
        if (SelectedPreset == null || SelectedPreset.IsFolder) return;
        
        var preset = new CharacterPreset
        {
            Prompt = Prompt,
            NegativePrompt = NegativePrompt,
            X = X,
            Y = Y
        };

        new CharacterPresetService().SavePreset(SelectedPreset.FullPath, preset);
        LoadPresets();
    }
    
    private void ExecuteSavePreset(object? parameter)
    {
        if (string.IsNullOrWhiteSpace(NewPresetPath)) return;

        var preset = new CharacterPreset
        {
            Prompt = Prompt,
            NegativePrompt = NegativePrompt,
            X = X,
            Y = Y
        };

        var service = new CharacterPresetService();
        service.SavePreset(NewPresetPath, preset);
        
        // 새로 저장한 후 로드 상태로 만들기
        // 주의: SelectedPreset을 null로 초기화한 후 다시 찾아서 넣어야
        // PropertyChanged가 제대로 발생하여 UI가 갱신될 수 있습니다. (특히 덮어쓰기 할 때 같은 객체면 무시됨)
        SelectedPreset = null;
        LoadPresets(); // 트리를 갱신하기 위해 호출
        SelectedPreset = new CharacterPresetService().FindPresetByPath(NewPresetPath);
    }

    private void ExecuteDeletePreset(object? parameter)
    {
        if (SelectedPreset == null || SelectedPreset.IsFolder) return;

        new CharacterPresetService().DeletePreset(SelectedPreset.FullPath);
        LoadPresets();
        SelectedPreset = null;
    }

    private void ExecuteClearPreset(object? parameter)
    {
        SelectedPreset = null;
    }

    private void ExecuteOpenPresetWindow(object? parameter)
    {
        var service = new CharacterPresetService();
        var presets = service.GetPresets();
        
        var window = new PresetSelectionWindow(presets);
        
        if (window.ShowDialog() == true && window.SelectedPreset != null)
        {
            SelectedPreset = window.SelectedPreset;
            ApplyPreset(window.SelectedPreset);
        }
    }

    private void ExecuteOpenSavePresetWindow(object? parameter)
    {
        var service = new CharacterPresetService();
        var presets = service.GetPresets();
        
        var window = new PresetSaveWindow(presets, NewPresetPath);
        
        if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.SavePath))
        {
            NewPresetPath = window.SavePath;
            ExecuteSavePreset(null);
            
            // ExecuteSavePreset internally updates SelectedPreset
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
