using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ImageGen.ViewModels;

public class CharacterPromptViewModel : INotifyPropertyChanged
{
    private string _prompt = string.Empty;
    private string _negativePrompt = string.Empty;
    private double _x = 0.5;
    private double _y = 0.5;

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

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
