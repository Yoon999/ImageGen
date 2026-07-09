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
        byte[]? imageData = null;

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

        if (imageData == null)
        {
            return null;
        }

        TryUpdatePreview(imageData, updatePreview);

        var fileName = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        await _imageService.SaveImageAsync(imageData, saveDirectory, fileName);
        return fileName;
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
}
