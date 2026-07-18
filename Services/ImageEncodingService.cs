using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageGen.Models;
using Color = System.Windows.Media.Color;

namespace ImageGen.Services;

public class ImageEncodingService
{
    private static readonly (int Width, int Height)[] CharacterReferenceCanvases =
    {
        (1024, 1536),
        (1536, 1024),
        (1472, 1472)
    };

    public BitmapImage LoadPreview(string filePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(filePath);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 220;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public BitmapSource LoadImage(string filePath)
    {
        return LoadBitmap(filePath);
    }

    public string EncodeImageFile(string filePath, int width, int height)
    {
        var resized = ResizeTo(filePath, width, height, BitmapScalingMode.HighQuality);
        return EncodeBitmap(resized);
    }

    public string EncodeInpaintMask(InpaintMaskDocument document, int width, int height, bool isV4)
    {
        return EncodeBitmap(RenderInpaintMask(document, width, height, isV4));
    }

    public BitmapSource CreateInpaintMaskPreview(
        InpaintMaskDocument document,
        int requestWidth,
        int requestHeight,
        bool isV4,
        int maxPixelWidth = 360)
    {
        var source = LoadBitmap(document.SourceImagePath);
        var scale = Math.Min(1d, (double)maxPixelWidth / source.PixelWidth);
        var previewWidth = Math.Max(1, (int)Math.Round(source.PixelWidth * scale));
        var previewHeight = Math.Max(1, (int)Math.Round(source.PixelHeight * scale));
        var mask = RenderInpaintMask(document, requestWidth, requestHeight, isV4);
        var overlay = InpaintMaskRenderer.CreateColorOverlay(mask, Color.FromRgb(255, 55, 55), 0.47);
        var drawing = new DrawingVisual();

        using (var context = drawing.RenderOpen())
        {
            context.DrawImage(source, new Rect(0, 0, previewWidth, previewHeight));
            context.DrawImage(overlay, new Rect(0, 0, previewWidth, previewHeight));
        }

        var output = new RenderTargetBitmap(previewWidth, previewHeight, 96, 96, PixelFormats.Pbgra32);
        output.Render(drawing);
        output.Freeze();
        return output;
    }

    public static BitmapSource RenderInpaintMask(InpaintMaskDocument document, int width, int height, bool isV4)
    {
        return InpaintMaskRenderer.Render(
            document.Strokes,
            document.PixelWidth,
            document.PixelHeight,
            width,
            height,
            isV4,
            document.FeatherEnabled,
            document.FeatherSize);
    }

    public string EncodeCharacterReferenceFile(string filePath, out int canvasWidth, out int canvasHeight)
    {
        var source = LoadBitmap(filePath);
        (canvasWidth, canvasHeight) = ChooseCharacterReferenceCanvas(source.PixelWidth, source.PixelHeight);
        var scale = Math.Min((double)canvasWidth / source.PixelWidth, (double)canvasHeight / source.PixelHeight);
        var imageWidth = Math.Max(1, (int)(source.PixelWidth * scale));
        var imageHeight = Math.Max(1, (int)(source.PixelHeight * scale));

        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle(System.Windows.Media.Brushes.Transparent, null, new Rect(0, 0, canvasWidth, canvasHeight));
            context.DrawImage(
                ResizeBitmap(source, imageWidth, imageHeight, BitmapScalingMode.HighQuality),
                new Rect((canvasWidth - imageWidth) / 2d, (canvasHeight - imageHeight) / 2d, imageWidth, imageHeight));
        }

        var output = new RenderTargetBitmap(canvasWidth, canvasHeight, 96, 96, PixelFormats.Pbgra32);
        output.Render(drawing);
        output.Freeze();
        return EncodeBitmap(output);
    }

    public (int Width, int Height) GetImageSize(string filePath)
    {
        var bitmap = LoadBitmap(filePath);
        return (bitmap.PixelWidth, bitmap.PixelHeight);
    }

    private static BitmapSource ResizeTo(string filePath, int width, int height, BitmapScalingMode scalingMode)
    {
        return ResizeBitmap(LoadBitmap(filePath), width, height, scalingMode);
    }

    private static BitmapSource ResizeBitmap(BitmapSource source, int width, int height, BitmapScalingMode scalingMode)
    {
        var scaleX = (double)width / source.PixelWidth;
        var scaleY = (double)height / source.PixelHeight;
        var resized = new TransformedBitmap(source, new ScaleTransform(scaleX, scaleY));
        RenderOptions.SetBitmapScalingMode(resized, scalingMode);
        resized.Freeze();
        return resized;
    }

    private static BitmapFrame LoadBitmap(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    private static string EncodeBitmap(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return Convert.ToBase64String(stream.ToArray());
    }

    private static (int Width, int Height) ChooseCharacterReferenceCanvas(int width, int height)
    {
        var aspect = (double)width / height;
        return CharacterReferenceCanvases
            .OrderBy(size => Math.Abs(((double)size.Width / size.Height) - aspect))
            .First();
    }
}
