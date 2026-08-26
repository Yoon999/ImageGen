using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageGen.Services;

public sealed class ImageMetadataRemovalService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp"
    };

    public ImageMetadataRemovalResult RemoveMetadata(string inputPath)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Image file was not found.", inputPath);
        }

        using var inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var decoder = BitmapDecoder.Create(
            inputStream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var source = decoder.Frames[0];
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int stride = checked(converted.PixelWidth * 4);
        var pixels = new byte[checked(stride * converted.PixelHeight)];
        converted.CopyPixels(pixels, stride, 0);

        for (int alphaIndex = 3; alphaIndex < pixels.Length; alphaIndex += 4)
        {
            pixels[alphaIndex] &= 0xFE;
        }

        double dpiX = source.DpiX > 0 ? source.DpiX : 96;
        double dpiY = source.DpiY > 0 ? source.DpiY : 96;
        var cleanedImage = BitmapSource.Create(
            converted.PixelWidth,
            converted.PixelHeight,
            dpiX,
            dpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        cleanedImage.Freeze();

        string outputPath = CreateUniqueOutputPath(inputPath);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(cleanedImage));

        using var outputStream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        encoder.Save(outputStream);

        return new ImageMetadataRemovalResult(inputPath, outputPath);
    }

    public static bool IsSupportedImagePath(string filePath)
    {
        return File.Exists(filePath) && SupportedExtensions.Contains(Path.GetExtension(filePath));
    }

    private static string CreateUniqueOutputPath(string inputPath)
    {
        string directory = Path.GetDirectoryName(inputPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        string fileName = Path.GetFileNameWithoutExtension(inputPath);
        string candidate = Path.Combine(directory, $"{fileName}_cleaned.png");
        int suffix = 2;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{fileName}_cleaned_{suffix}.png");
            suffix++;
        }

        return candidate;
    }
}

public sealed record ImageMetadataRemovalResult(string InputPath, string OutputPath);
