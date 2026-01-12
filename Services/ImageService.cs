using System.IO;
using System.Windows.Media.Imaging;
using ImageGen.Services.Interfaces;

namespace ImageGen.Services;

public class ImageService : IImageService
{
    public async Task SaveImageAsync(byte[] imageData, string directoryPath, string fileName)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string fullPath = Path.Combine(directoryPath, fileName);
        await File.WriteAllBytesAsync(fullPath, imageData);
    }

    public BitmapImage ConvertToBitmapImage(byte[] imageData)
    {
        if (imageData == null || imageData.Length == 0) return null;

        var image = new BitmapImage();
        using (var mem = new MemoryStream(imageData))
        {
            mem.Position = 0;
            image.BeginInit();
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = null;
            image.StreamSource = mem;
            image.EndInit();
        }
        image.Freeze(); // UI 스레드 간 공유를 위해 Freeze 호출
        return image;
    }
}
