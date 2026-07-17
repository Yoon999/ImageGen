using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace ImageGen.Models;

public class VibeReferenceImage : INotifyPropertyChanged
{
    private string _filePath = string.Empty;
    private double _informationExtracted = 1.0;
    private double _strength = 0.6;

    public string FilePath
    {
        get => _filePath;
        set { _filePath = value; OnPropertyChanged(); }
    }

    public BitmapImage? Preview { get; set; }

    public double InformationExtracted
    {
        get => _informationExtracted;
        set { _informationExtracted = value; OnPropertyChanged(); }
    }

    public double Strength
    {
        get => _strength;
        set { _strength = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
