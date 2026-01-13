using System.IO;
using System.Text.Json;
using ImageGen.Models;

namespace ImageGen.Services;

public class CharacterPresetService
{
    private readonly string _filePath;
    private List<CharacterPreset> _presets = new();

    public CharacterPresetService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "ImageGen");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        _filePath = Path.Combine(folder, "character_presets.json");
        LoadPresets();
    }

    public List<CharacterPreset> GetPresets()
    {
        return _presets;
    }

    public void SavePreset(CharacterPreset preset)
    {
        var existing = _presets.FirstOrDefault(p => p.Name == preset.Name);
        if (existing != null)
        {
            existing.Prompt = preset.Prompt;
            existing.NegativePrompt = preset.NegativePrompt;
            existing.X = preset.X;
            existing.Y = preset.Y;
        }
        else
        {
            _presets.Add(preset);
        }
        SaveToFile();
   }

    public void DeletePreset(string name)
    {
        var preset = _presets.FirstOrDefault(p => p.Name == name);
        if (preset != null)
        {
            _presets.Remove(preset);
            SaveToFile();
        }
    }

    private void LoadPresets()
    {
        if (File.Exists(_filePath))
        {
            try
            {
                string json = File.ReadAllText(_filePath);
                _presets = JsonSerializer.Deserialize<List<CharacterPreset>>(json) ?? new List<CharacterPreset>();
            }
            catch
            {
                _presets = new List<CharacterPreset>();
            }
        }
    }

    private void SaveToFile()
    {
        try
        {
            string json = JsonSerializer.Serialize(_presets, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Ignore errors for now
        }
    }
}
