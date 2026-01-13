using ImageGen.Models.Api;
using ImageGen.ViewModels;

namespace ImageGen.Models;

public class AppSettings
{
    public string ApiToken { get; set; } = string.Empty;
    public string SaveDirectory { get; set; } = string.Empty;
    public string LastPrompt { get; set; } = string.Empty;
    public bool IsRandomSeed { get; set; } = true; // 기본값 true
    public RequestParameters LastParameters { get; set; } = new RequestParameters();
    public List<CharacterPromptSettings> CharacterPrompts { get; set; } = new List<CharacterPromptSettings>();
}

public class CharacterPromptSettings
{
    public string Prompt { get; set; } = string.Empty;
    public string NegativePrompt { get; set; } = string.Empty;
    public double X { get; set; } = 0.5;
    public double Y { get; set; } = 0.5;
}
