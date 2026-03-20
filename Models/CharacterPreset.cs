using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ImageGen.Models;

public class CharacterPreset
{
    // Folder or file name
    public string Name { get; set; } = string.Empty;
    
    // True if this is a folder containing other presets
    public bool IsFolder { get; set; } = false;
    
    // Folder content
    public List<CharacterPreset> Children { get; set; } = new();
    
    // Preset data (only if IsFolder == false)
    public string Prompt { get; set; } = string.Empty;
    public string NegativePrompt { get; set; } = string.Empty;
    public double X { get; set; } = 0.5;
    public double Y { get; set; } = 0.5;

    [JsonIgnore]
    public string FullPath { get; set; } = string.Empty;
}
