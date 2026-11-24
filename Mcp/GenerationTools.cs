using System.Text.Json;
using System.Text.Json.Serialization;
using ImageGen.Helpers;
using ImageGen.Models;
using ImageGen.Models.Api;
using ImageGen.Services;
using ModelContextProtocol;

namespace ImageGen.Mcp;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CharacterInput(string Prompt, string? NegativePrompt = null);

public sealed class GenerationTools(string appDirectory,
    Func<GenerationRequest, string, CancellationToken, Task<byte[]>> generateImage)
{
    private AppSettings ReadSettings()
    {
        try
        {
            using var stream = new FileStream(Path.Combine(appDirectory, "settings.json"), FileMode.Open,
                FileAccess.Read, FileShare.Read | FileShare.Delete);
            return JsonSerializer.Deserialize<AppSettings>(stream) ?? throw new JsonException();
        }
        catch (FileNotFoundException) { return new AppSettings(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new McpException("Cannot read ImageGen settings.");
        }
    }

    public string ListMacros() => JsonSerializer.Serialize((ReadSettings().Macros ?? [])
        .Select(m => new { name = m.Name, token = m.Token }));

    public async Task<object> Generate(string basePrompt, string? negativePrompt = null,
        CharacterInput[]? characters = null, CancellationToken cancellationToken = default)
    {
        var settings = ReadSettings();
        if (string.IsNullOrWhiteSpace(basePrompt)) throw new McpException("Enter a basePrompt.");
        if (string.IsNullOrWhiteSpace(settings.ApiToken)) throw new McpException("Set the API token in ImageGen first.");
        var source = new GenerationRequest { model = settings.Model, parameters = settings.LastParameters ?? new() };
        negativePrompt ??= source.parameters.uc;
        if (characters is { Length: > 0 } && !GenerationRequestBuilder.IsV4(source))
            throw new McpException("Select a character-prompt compatible model in ImageGen.");
        if (characters?.Any(c => string.IsNullOrWhiteSpace(c?.Prompt)) == true)
            throw new McpException("Each character needs a prompt.");
        var request = GenerationRequestBuilder.BuildStandaloneRequest(source, basePrompt, negativePrompt,
            (characters ?? []).Select(c => new CharacterPromptSettings { Prompt = c.Prompt, NegativePrompt = c.NegativePrompt ?? "" }),
            settings.IsRandomSeed);
        GenerationRequestBuilder.ApplySharedOptions(request, basePrompt, negativePrompt, settings.SmeaMode, settings.Variety);
        request.parameters.uc = negativePrompt;
        request.parameters.n_samples = 1;
        request.parameters.add_original_image = false;
        try { GenerationRequestBuilder.ExpandMacros(request, settings.Macros ?? []); }
        catch (ArgumentException ex) { throw new McpException(ex.Message); }

        byte[] data;
        try { data = await generateImage(request, settings.ApiToken, cancellationToken); }
        catch (HttpRequestException ex) { throw new McpException($"NovelAI generation failed: {ex.Message}"); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new McpException("NovelAI generation timed out."); }
        var png = ZipHelper.ExtractFirstImage(data) ?? data;
        if (!png.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            throw new McpException("NovelAI returned no PNG.");
        try
        {
            var directory = Path.GetFullPath(string.IsNullOrWhiteSpace(settings.SaveDirectory) ? "Output" : settings.SaveDirectory, appDirectory);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"mcp_{Guid.NewGuid():N}.png");
            var temp = path + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(temp, png, cancellationToken);
                File.Move(temp, path);
            }
            finally { File.Delete(temp); }
            return new { path, seed = request.parameters.seed };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { throw new McpException($"Could not save the PNG: {ex.Message}"); }
    }
}
