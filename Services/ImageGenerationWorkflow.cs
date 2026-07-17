using System.Windows;
using System.Windows.Media.Imaging;
using ImageGen.Helpers;
using ImageGen.Models.Api;
using ImageGen.Services.Interfaces;
using Application = System.Windows.Application;

namespace ImageGen.Services;

public class ImageGenerationWorkflow
{
    private readonly INovelAiService _novelAiService;
    private readonly IImageService _imageService;

    public ImageGenerationWorkflow(INovelAiService novelAiService, IImageService imageService)
    {
        _novelAiService = novelAiService;
        _imageService = imageService;
    }

    public async Task<string?> GenerateAndSaveAsync(
        GenerationRequest request,
        string accessToken,
        string saveDirectory,
        string filePrefix,
        Action<BitmapImage> updatePreview)
    {
        var result = await GenerateAndSaveWithCostAsync(request, accessToken, saveDirectory, filePrefix, updatePreview);
        return result.FileName;
    }

    public async Task<ImageGenerationResult> GenerateAndSaveWithCostAsync(
        GenerationRequest request,
        string accessToken,
        string saveDirectory,
        string filePrefix,
        Action<BitmapImage> updatePreview)
    {
        byte[]? imageData = null;
        int? startAnlas = await TryGetAnlasAsync(accessToken);

        if (request.action == "generate")
        {
            await Task.Run(async () =>
            {
                var lastUpdate = DateTime.MinValue;
                await foreach (var streamData in _novelAiService.GenerateImageStreamAsync(request, accessToken))
                {
                    imageData = streamData;
                    var now = DateTime.Now;
                    if ((now - lastUpdate).TotalMilliseconds < 60)
                    {
                        continue;
                    }

                    lastUpdate = now;
                    TryUpdatePreview(streamData, updatePreview);
                }
            });
        }
        else
        {
            var zipData = await _novelAiService.GenerateImageAsync(request, accessToken);
            imageData = ZipHelper.ExtractFirstImage(zipData) ?? zipData;
        }

        if (imageData == null)
        {
            return new ImageGenerationResult(null, null);
        }

        TryUpdatePreview(imageData, updatePreview);

        var fileName = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        await _imageService.SaveImageAsync(imageData, saveDirectory, fileName);
        int? endAnlas = await TryGetAnlasAsync(accessToken);
        return new ImageGenerationResult(fileName, CalculateCost(startAnlas, endAnlas));
    }

    public async Task<ImageGenerationResult> AugmentAndSaveAsync(
        AugmentImageRequest request,
        string accessToken,
        string saveDirectory,
        string filePrefix,
        Action<BitmapImage> updatePreview)
    {
        int? startAnlas = await TryGetAnlasAsync(accessToken);
        var zipData = await _novelAiService.AugmentImageAsync(request, accessToken);
        var imageData = ZipHelper.ExtractFirstImage(zipData) ?? zipData;

        TryUpdatePreview(imageData, updatePreview);

        var fileName = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        await _imageService.SaveImageAsync(imageData, saveDirectory, fileName);
        int? endAnlas = await TryGetAnlasAsync(accessToken);
        return new ImageGenerationResult(fileName, CalculateCost(startAnlas, endAnlas));
    }

    private void TryUpdatePreview(byte[] imageData, Action<BitmapImage> updatePreview)
    {
        try
        {
            var bitmap = _imageService.ConvertToBitmapImage(imageData);
            Dispatch(() => updatePreview(bitmap));
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update generated image preview", ex);
        }
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    private async Task<int?> TryGetAnlasAsync(string accessToken)
    {
        try
        {
            return await _novelAiService.GetAnlasAsync(accessToken);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to read anlas for generation cost", ex);
            return null;
        }
    }

    private static int? CalculateCost(int? startAnlas, int? endAnlas)
    {
        if (!startAnlas.HasValue || !endAnlas.HasValue)
        {
            return null;
        }

        return Math.Max(0, startAnlas.Value - endAnlas.Value);
    }
}

public record ImageGenerationResult(string? FileName, int? AnlasCost);
