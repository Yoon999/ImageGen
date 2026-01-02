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
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://image.novelai.net";

    public NovelAiApiService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromMinutes(5) // 생성 시간이 걸릴 수 있으므로 타임아웃 넉넉하게 설정
        };
    }

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
                Logger.LogError(errorMessage); // API 에러 로그 기록
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
                // 태그 추천 실패는 치명적이지 않으므로 빈 리스트 반환하거나 로그 남김
                // Logger.LogInfo($"Tag suggestion failed: {response.StatusCode}");
                return new List<TagSuggestion>();
            }

            var result = await response.Content.ReadFromJsonAsync<TagSuggestionResponse>();
            return result?.Tags ?? new List<TagSuggestion>();
        }
        catch (Exception ex)
        {
            // Logger.LogError("Exception in SuggestTagsAsync", ex);
            return new List<TagSuggestion>();
        }
    }
}
