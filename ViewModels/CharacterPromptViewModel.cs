using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ImageGen.Models;
using ImageGen.Services;

namespace ImageGen.ViewModels;

public class CharacterPromptViewModel : INotifyPropertyChanged
{
    private string _prompt = string.Empty;
    private string _negativePrompt = string.Empty;
    private double _x = 0.5;
    private double _y = 0.5;
    private CharacterPreset? _selectedPreset;
    private string _newPresetName = string.Empty;
    
    public ICommand RefreshPresetsCommand { get; }

    public CharacterPromptViewModel()
    {
        LoadPresets();
        
        LoadPresetCommand = new RelayCommand(ExecuteLoadPreset);
        UpdatePresetCommand = new RelayCommand(ExecuteUpdatePreset);
        SavePresetCommand = new RelayCommand(ExecuteSavePreset);
        DeletePresetCommand = new RelayCommand(ExecuteDeletePreset);
        RefreshPresetsCommand = new RelayCommand(_ => LoadPresets());
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
            }
        }
    }

    public string NewPresetName
    {
        get => _newPresetName;
        set
        {
            _newPresetName = value;
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

    private void LoadPresets()
    {
        Presets.Clear();
        foreach (var preset in new CharacterPresetService().GetPresets())
        {
            Presets.Add(preset);
        }
    }

    private void ApplyPreset(CharacterPreset preset)
    {
        Prompt = preset.Prompt;
        NegativePrompt = preset.NegativePrompt;
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
        if (SelectedPreset == null) return;
        var preset = new CharacterPreset
        {
            Name = SelectedPreset.Name,
            Prompt = Prompt,
            NegativePrompt = NegativePrompt,
            X = X,
            Y = Y
        };

        new CharacterPresetService().SavePreset(preset);
        LoadPresets();
        
        // 방금 저장한 프리셋 선택
        SelectedPreset = Presets.FirstOrDefault(p => p.Name == preset.Name);
    }
    
    private void ExecuteSavePreset(object? parameter)
    {
        if (string.IsNullOrWhiteSpace(NewPresetName)) return;

        var preset = new CharacterPreset
        {
            Name = NewPresetName,
            Prompt = Prompt,
            NegativePrompt = NegativePrompt,
            X = X,
            Y = Y
        };

        new CharacterPresetService().SavePreset(preset);
        LoadPresets();
        
        // 방금 저장한 프리셋 선택
        SelectedPreset = Presets.FirstOrDefault(p => p.Name == NewPresetName);
    }

    private void ExecuteDeletePreset(object? parameter)
    {
        if (SelectedPreset == null) return;

        new CharacterPresetService().DeletePreset(SelectedPreset.Name);
        LoadPresets();
        SelectedPreset = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
