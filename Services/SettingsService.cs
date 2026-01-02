using System.IO;
using System.Text.Json;
using ImageGen.Helpers;
using ImageGen.Models;

namespace ImageGen.Services;

public class SettingsService
{
    private readonly string _settingsFilePath;
    private const string SettingsFileName = "settings.json";

    public SettingsService()
    {
        _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
    }

    public AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load settings", ex);
        }

        // 파일이 없거나 로드 실패 시 기본값 반환
        return new AppSettings
        {
            SaveDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output")
        };
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to save settings", ex);
        }
    }
}
