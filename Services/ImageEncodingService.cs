using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using ImageGen.Models;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Image = System.Windows.Controls.Image;
using Size = System.Windows.Size;

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
        var maskWidth = isV4 ? width : (int)Math.Ceiling(width / 64d) * 8;
        var maskHeight = isV4 ? height : (int)Math.Ceiling(height / 64d) * 8;
        return EncodeBitmap(RenderInpaintMask(document, maskWidth, maskHeight));
    }

    public BitmapSource CreateInpaintMaskPreview(InpaintMaskDocument document, int maxPixelWidth = 360)
    {
        var source = LoadBitmap(document.SourceImagePath);
        var scale = Math.Min(1d, (double)maxPixelWidth / source.PixelWidth);
        var previewWidth = Math.Max(1, (int)Math.Round(source.PixelWidth * scale));
        var previewHeight = Math.Max(1, (int)Math.Round(source.PixelHeight * scale));
        var mask = RenderInpaintMask(document, previewWidth, previewHeight);
        var drawing = new DrawingVisual();

        using (var context = drawing.RenderOpen())
        {
            context.DrawImage(source, new Rect(0, 0, previewWidth, previewHeight));
            context.PushOpacity(0.47);
            context.PushOpacityMask(new ImageBrush(mask) { Stretch = Stretch.Fill });
            context.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(255, 55, 55)),
                null,
                new Rect(0, 0, previewWidth, previewHeight));
            context.Pop();
            context.Pop();
        }

        var output = new RenderTargetBitmap(previewWidth, previewHeight, 96, 96, PixelFormats.Pbgra32);
        output.Render(drawing);
        output.Freeze();
        return output;
    }

    private static BitmapSource RenderInpaintMask(InpaintMaskDocument document, int width, int height)
    {
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
            context.PushTransform(new ScaleTransform(
                (double)width / document.PixelWidth,
                (double)height / document.PixelHeight));

            foreach (var stroke in document.Strokes)
            {
                var attributes = stroke.DrawingAttributes.Clone();
                attributes.Color = Colors.White;
                attributes.IsHighlighter = false;
                attributes.IgnorePressure = true;
                stroke.Draw(context, attributes);
            }

            context.Pop();
        }

        var output = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        output.Render(drawing);
        output.Freeze();
        var binaryMask = BinarizeMask(output);
        if (!document.FeatherEnabled)
        {
            return binaryMask;
        }

        var scale = ((double)width / document.PixelWidth + (double)height / document.PixelHeight) / 2;
        return ApplyFeather(binaryMask, Math.Max(0.1, document.FeatherSize * scale));
    }

    private static BitmapSource ApplyFeather(BitmapSource source, double radius)
    {
        var image = new Image
        {
            Source = source,
            Width = source.PixelWidth,
            Height = source.PixelHeight,
            Stretch = Stretch.Fill,
            Effect = new BlurEffect
            {
                Radius = radius,
                KernelType = KernelType.Gaussian,
                RenderingBias = RenderingBias.Quality
            }
        };
        var size = new Size(source.PixelWidth, source.PixelHeight);
        image.Measure(size);
        image.Arrange(new Rect(size));
        image.UpdateLayout();

        var output = new RenderTargetBitmap(
            source.PixelWidth,
            source.PixelHeight,
            96,
            96,
            PixelFormats.Pbgra32);
        output.Render(image);
        output.Freeze();
        return output;
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

    private static BitmapSource BinarizeMask(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        for (var i = 0; i < pixels.Length; i += 4)
        {
            var selected = pixels[i + 3] > 0;
            pixels[i] = selected ? (byte)255 : (byte)0;
            pixels[i + 1] = selected ? (byte)255 : (byte)0;
            pixels[i + 2] = selected ? (byte)255 : (byte)0;
            pixels[i + 3] = selected ? (byte)255 : (byte)0;
        }

        var output = BitmapSource.Create(
            converted.PixelWidth,
            converted.PixelHeight,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        output.Freeze();
        return output;
    }

    private static (int Width, int Height) ChooseCharacterReferenceCanvas(int width, int height)
    {
        var aspect = (double)width / height;
        return CharacterReferenceCanvases
            .OrderBy(size => Math.Abs(((double)size.Width / size.Height) - aspect))
            .First();
    }
}
