namespace ImageGen.Models;

public class CharacterPreset
{
    public string Name { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string NegativePrompt { get; set; } = string.Empty;
    public double X { get; set; } = 0.5;
    public double Y { get; set; } = 0.5;
}
