using ImageGen.Models.Api;

namespace ImageGen.Models;

public class AppSettings
{
    public string ApiToken { get; set; } = string.Empty;
    public string SaveDirectory { get; set; } = string.Empty;
    public string LastPrompt { get; set; } = string.Empty;
    public bool IsRandomSeed { get; set; } = true; // ±âº»°ª true
    public RequestParameters LastParameters { get; set; } = new RequestParameters();
}
