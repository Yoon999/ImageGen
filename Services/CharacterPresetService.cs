using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ImageGen.Models;

namespace ImageGen.Services;

public class CharacterPresetService
{
    private readonly string _filePath;
    private List<CharacterPreset> _presets = new();

    public CharacterPresetService()
    {
        _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "character_presets.json");
        LoadPresets();
    }

    public List<CharacterPreset> GetPresets()
    {
        return _presets;
    }

    public CharacterPreset? FindPresetByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var parts = path.Split('/');
        var currentList = _presets;
        CharacterPreset? result = null;

        foreach (var part in parts)
        {
            result = currentList.FirstOrDefault(p => p.Name == part);
            if (result == null) return null;
            if (result.IsFolder)
            {
                currentList = result.Children;
            }
        }
        
        return result;
    }

    public void SavePreset(string path, CharacterPreset presetData)
    {
        if (string.IsNullOrEmpty(path)) return;

        var parts = path.Split('/');
        var currentList = _presets;

        // Traverse or create folders
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var folderName = parts[i];
            var folder = currentList.FirstOrDefault(p => p.Name == folderName && p.IsFolder);
            
            if (folder == null)
            {
                folder = new CharacterPreset { Name = folderName, IsFolder = true };
                currentList.Add(folder);
            }
            
            currentList = folder.Children;
        }

        var fileName = parts.Last();
        var existing = currentList.FirstOrDefault(p => p.Name == fileName && !p.IsFolder);

        if (existing != null)
        {
            existing.Prompt = presetData.Prompt;
            existing.NegativePrompt = presetData.NegativePrompt;
            existing.X = presetData.X;
            existing.Y = presetData.Y;
        }
        else
        {
            presetData.Name = fileName;
            presetData.IsFolder = false;
            currentList.Add(presetData);
        }

        SaveToFile();
    }

    public void DeletePreset(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        var parts = path.Split('/');
        var currentList = _presets;
        
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var folder = currentList.FirstOrDefault(p => p.Name == parts[i] && p.IsFolder);
            if (folder == null) return; // Path not found
            currentList = folder.Children;
        }

        var fileName = parts.Last();
        var itemToRemove = currentList.FirstOrDefault(p => p.Name == fileName);
        
        if (itemToRemove != null)
        {
            currentList.Remove(itemToRemove);
            
            // Clean up empty folders recursively? For now just keep them or user can manually delete?
            // Since we don't have explicit folder delete in UI right now, leaving empty folders is safer.
            
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
                PopulateFullPaths(_presets, "");
            }
            catch
            {
                _presets = new List<CharacterPreset>();
            }
        }
    }
    
    private void PopulateFullPaths(List<CharacterPreset> nodes, string currentPath)
    {
        foreach (var node in nodes)
        {
            node.FullPath = string.IsNullOrEmpty(currentPath) ? node.Name : $"{currentPath}/{node.Name}";
            if (node.IsFolder)
            {
                PopulateFullPaths(node.Children, node.FullPath);
            }
        }
    }

    private void SaveToFile()
    {
        try
        {
            string json = JsonSerializer.Serialize(_presets, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
            // Update full paths after save
            PopulateFullPaths(_presets, "");
        }
        catch
        {
            // Ignore errors for now
        }
    }
}
