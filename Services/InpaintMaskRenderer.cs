using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Image = System.Windows.Controls.Image;
using Size = System.Windows.Size;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;

namespace ImageGen.Services;

public static class InpaintMaskRenderer
{
    public static BitmapSource Render(
        StrokeCollection strokes,
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight,
        bool isV4,
        bool featherEnabled,
        double featherSize)
    {
        var rawMask = RenderStrokes(strokes, sourceWidth, sourceHeight, targetWidth, targetHeight);
        var binaryMask = CreateComfyRgbaMask(rawMask, useAlphaAsIntensity: false);
        BitmapSource coverageMask = binaryMask;
        if (featherEnabled)
        {
            double scale = ((double)targetWidth / sourceWidth + (double)targetHeight / sourceHeight) / 2;
            coverageMask = ApplyBlur(binaryMask, Math.Max(0.1, featherSize * scale));
        }

        // V4 transmits a full-size mask whose values are quantized to NovelAI's 1/8 latent grid.
        int latentWidth = (int)Math.Ceiling(targetWidth / 64d) * 8;
        int latentHeight = (int)Math.Ceiling(targetHeight / 64d) * 8;
        byte[] coverage = ExtractAlpha(coverageMask);
        byte[] latentCoverage = ResizeNearestExact(
            coverage,
            targetWidth,
            targetHeight,
            latentWidth,
            latentHeight);

        if (!isV4)
        {
            return CreateComfyRgbaMask(latentCoverage, latentWidth, latentHeight);
        }

        int v4Width = latentWidth * 8;
        int v4Height = latentHeight * 8;
        byte[] v4Coverage = ResizeNearestExact(
            latentCoverage,
            latentWidth,
            latentHeight,
            v4Width,
            v4Height);
        return CreateComfyRgbaMask(v4Coverage, v4Width, v4Height);
    }

    public static BitmapSource CreateColorOverlay(BitmapSource mask, Color color, double opacity)
    {
        var converted = new FormatConvertedBitmap(mask, PixelFormats.Bgra32, null, 0);
        int stride = converted.PixelWidth * 4;
        var sourcePixels = new byte[stride * converted.PixelHeight];
        var overlayPixels = new byte[sourcePixels.Length];
        converted.CopyPixels(sourcePixels, stride, 0);
        double clampedOpacity = Math.Clamp(opacity, 0, 1);

        for (int i = 0; i < sourcePixels.Length; i += 4)
        {
            byte intensity = Math.Max(sourcePixels[i], Math.Max(sourcePixels[i + 1], sourcePixels[i + 2]));
            overlayPixels[i] = color.B;
            overlayPixels[i + 1] = color.G;
            overlayPixels[i + 2] = color.R;
            overlayPixels[i + 3] = (byte)Math.Round(intensity * clampedOpacity);
        }

        return CreateBitmap(converted.PixelWidth, converted.PixelHeight, overlayPixels, stride);
    }

    private static BitmapSource RenderStrokes(
        StrokeCollection strokes,
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight)
    {
        var drawing = new DrawingVisual();
        using (DrawingContext context = drawing.RenderOpen())
        {
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, targetWidth, targetHeight));
            context.PushTransform(new ScaleTransform(
                (double)targetWidth / sourceWidth,
                (double)targetHeight / sourceHeight));

            foreach (Stroke stroke in strokes)
            {
                var attributes = stroke.DrawingAttributes.Clone();
                attributes.Color = Colors.White;
                attributes.IsHighlighter = false;
                attributes.IgnorePressure = true;
                stroke.Draw(context, attributes);
            }

            context.Pop();
        }

        var output = new RenderTargetBitmap(targetWidth, targetHeight, 96, 96, PixelFormats.Pbgra32);
        output.Render(drawing);
        output.Freeze();
        return output;
    }

    private static BitmapSource ApplyBlur(BitmapSource source, double radius)
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

    private static BitmapSource CreateComfyRgbaMask(BitmapSource source, bool useAlphaAsIntensity)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = converted.PixelWidth * 4;
        var sourcePixels = new byte[stride * converted.PixelHeight];
        var maskPixels = new byte[sourcePixels.Length];
        converted.CopyPixels(sourcePixels, stride, 0);

        for (int i = 0; i < sourcePixels.Length; i += 4)
        {
            byte sourceAlpha = sourcePixels[i + 3];
            byte intensity = useAlphaAsIntensity
                ? sourceAlpha
                : sourceAlpha >= 128 ? (byte)255 : (byte)0;
            byte alpha = intensity > 0 ? (byte)255 : (byte)0;

            maskPixels[i] = intensity;
            maskPixels[i + 1] = intensity;
            maskPixels[i + 2] = intensity;
            maskPixels[i + 3] = alpha;
        }

        return CreateBitmap(converted.PixelWidth, converted.PixelHeight, maskPixels, stride);
    }

    private static BitmapSource CreateComfyRgbaMask(byte[] coverage, int width, int height)
    {
        int stride = width * 4;
        var pixels = new byte[stride * height];
        for (int pixelIndex = 0, coverageIndex = 0; coverageIndex < coverage.Length; pixelIndex += 4, coverageIndex++)
        {
            byte intensity = coverage[coverageIndex];
            pixels[pixelIndex] = intensity;
            pixels[pixelIndex + 1] = intensity;
            pixels[pixelIndex + 2] = intensity;
            pixels[pixelIndex + 3] = intensity > 0 ? (byte)255 : (byte)0;
        }

        return CreateBitmap(width, height, pixels, stride);
    }

    private static byte[] ExtractAlpha(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        var alpha = new byte[converted.PixelWidth * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        for (int sourceIndex = 3, alphaIndex = 0; sourceIndex < pixels.Length; sourceIndex += 4, alphaIndex++)
        {
            alpha[alphaIndex] = pixels[sourceIndex];
        }

        return alpha;
    }

    private static byte[] ResizeNearestExact(
        byte[] source,
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight)
    {
        var output = new byte[targetWidth * targetHeight];
        for (int y = 0; y < targetHeight; y++)
        {
            int sourceY = Math.Min(sourceHeight - 1, (int)Math.Floor((y + 0.5) * sourceHeight / targetHeight));
            int sourceRow = sourceY * sourceWidth;
            int targetRow = y * targetWidth;

            for (int x = 0; x < targetWidth; x++)
            {
                int sourceX = Math.Min(sourceWidth - 1, (int)Math.Floor((x + 0.5) * sourceWidth / targetWidth));
                output[targetRow + x] = source[sourceRow + sourceX];
            }
        }

        return output;
    }

    private static BitmapSource CreateBitmap(int width, int height, byte[] pixels, int stride)
    {
        var output = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        output.Freeze();
        return output;
    }
}
