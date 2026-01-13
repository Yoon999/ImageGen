using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using ImageGen.Helpers;
using ImageGen.Models.Api;
using ImageGen.Services.Interfaces;

namespace ImageGen.Services;

public class NovelAiApiService : INovelAiService
{
    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri(BaseUrl),
        Timeout = TimeSpan.FromMinutes(5) // 생성 시간이 걸릴 수 있으므로 타임아웃 넉넉하게 설정
    };
    private const string BaseUrl = "https://image.novelai.net";

    public async Task<byte[]> GenerateImageAsync(GenerationRequest request, string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/ai/generate-image", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var errorMessage = $"API Error: {response.StatusCode} - {errorContent}";
                Logger.LogError(errorMessage);
                throw new HttpRequestException(errorMessage);
            }

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError("Exception in GenerateImageAsync", ex);
            throw;
        }
    }

    public async IAsyncEnumerable<byte[]> GenerateImageStreamAsync(GenerationRequest request, string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var jsonContent = JsonContent.Create(request);
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/ai/generate-image-stream");
        requestMessage.Content = jsonContent;
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            var errorMessage = $"API Error: {response.StatusCode} - {errorContent}";
            Logger.LogError(errorMessage);
            throw new HttpRequestException(errorMessage);
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data:")) continue;
            
            var data = line.Substring(5).Trim();
            if (data == "[DONE]") break;
                
            var streamResponse = JsonSerializer.Deserialize<StreamResponse>(data);
            if (streamResponse == null || string.IsNullOrEmpty(streamResponse.Image)) continue;
            // Console.WriteLine(streamResponse.EventType + " | " + streamResponse.StepIndex + " | " + streamResponse.GenId + " | " + streamResponse.Image);
            yield return Convert.FromBase64String(streamResponse.Image);
        }
    }

    public async Task<List<TagSuggestion>> SuggestTagsAsync(string prompt, string model, string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["model"] = model;
            query["prompt"] = prompt;
            
            var uri = $"/ai/generate-image/suggest-tags?{query}";
            var response = await _httpClient.GetAsync(uri);

            if (!response.IsSuccessStatusCode)
            {
                return new List<TagSuggestion>();
            }

            var result = await response.Content.ReadFromJsonAsync<TagSuggestionResponse>();
            return result?.Tags ?? new List<TagSuggestion>();
        }
        catch (Exception ex)
        {
            return new List<TagSuggestion>();
        }
    }
}
