using System.Windows.Ink;
using System.Windows.Media.Imaging;

namespace ImageGen.Models;

public sealed class InpaintMaskDocument
{
    public InpaintMaskDocument(
        string sourceImagePath,
        int pixelWidth,
        int pixelHeight,
        StrokeCollection strokes,
        bool featherEnabled = true,
        double featherSize = 12,
        BitmapSource? preview = null)
    {
        SourceImagePath = sourceImagePath;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        Strokes = strokes.Clone();
        FeatherEnabled = featherEnabled;
        FeatherSize = Math.Clamp(featherSize, 1, 64);
        Preview = preview;
        RevisionId = Guid.NewGuid().ToString("N");
    }

    public string SourceImagePath { get; }
    public int PixelWidth { get; }
    public int PixelHeight { get; }
    public StrokeCollection Strokes { get; }
    public bool FeatherEnabled { get; }
    public double FeatherSize { get; }
    public BitmapSource? Preview { get; set; }
    public string RevisionId { get; }
    public bool HasMask => Strokes.Count > 0;
}
