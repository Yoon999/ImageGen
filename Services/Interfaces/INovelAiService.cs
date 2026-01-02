using ImageGen.Models.Api;

namespace ImageGen.Services.Interfaces;

public interface INovelAiService
{
    /// <summary>
    /// NovelAI API에 이미지 생성을 요청합니다.
    /// </summary>
    /// <param name="request">생성 요청 파라미터</param>
    /// <param name="accessToken">Bearer 인증 토큰</param>
    /// <returns>생성된 이미지의 ZIP 바이너리 데이터</returns>
    Task<byte[]> GenerateImageAsync(GenerationRequest request, string accessToken);

    /// <summary>
    /// 입력된 프롬프트를 기반으로 태그를 추천받습니다.
    /// </summary>
    /// <param name="prompt">현재 입력 중인 태그 텍스트</param>
    /// <param name="model">사용 중인 모델 (예: nai-diffusion-3)</param>
    /// <param name="accessToken">Bearer 인증 토큰</param>
    /// <returns>추천 태그 목록</returns>
    Task<List<TagSuggestion>> SuggestTagsAsync(string prompt, string model, string accessToken);
}
