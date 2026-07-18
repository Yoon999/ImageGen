using ImageGen.Models.Api;
using ImageGen.ViewModels;
using System.Text.Json.Serialization;

namespace ImageGen.Models;

public class AppSettings
{
    public string ApiToken { get; set; } = string.Empty;
    public string SaveDirectory { get; set; } = string.Empty;
    public string LastPrompt { get; set; } = string.Empty;
    public bool IsRandomSeed { get; set; } = true; // 기본값 true
    public RequestParameters LastParameters { get; set; } = new RequestParameters();
    public List<CharacterPromptSettings> CharacterPrompts { get; set; } = new List<CharacterPromptSettings>();
    public ImageInputSettings ImageInput { get; set; } = new ImageInputSettings();
    public ReferenceSettings References { get; set; } = new ReferenceSettings();
}

public class CharacterPromptSettings
{
    public string Prompt { get; set; } = string.Empty;
    public string NegativePrompt { get; set; } = string.Empty;
    public double X { get; set; } = 0.5;
    public double Y { get; set; } = 0.5;
    public string PresetPath { get; set; } = string.Empty;
}

public class ImageInputSettings
{
    public string GenerationMode { get; set; } = "Text2Image";
    public string SourceImagePath { get; set; } = string.Empty;
    public double Strength { get; set; } = 0.7;
    public double Noise { get; set; }
    public bool AddOriginalImage { get; set; } = true;
}

public class ReferenceSettings
{
    public List<VibeReferenceSettings> VibeReferences { get; set; } = new List<VibeReferenceSettings>();
    public string? PreciseReferencePath { get; set; }
    public string? PreciseReferenceType { get; set; }
    public double? PreciseReferenceStrength { get; set; }
    public double? PreciseReferenceFidelity { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CharacterReferencePath { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? CharacterReferenceStyleAware { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? CharacterReferenceFidelity { get; set; }
}

public class VibeReferenceSettings
{
    public string FilePath { get; set; } = string.Empty;
    public double InformationExtracted { get; set; } = 1.0;
    public double Strength { get; set; } = 0.6;
}
