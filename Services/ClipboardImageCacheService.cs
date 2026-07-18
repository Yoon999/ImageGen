using System.IO;
using System.Windows.Media.Imaging;
using ImageGen.Helpers;

namespace ImageGen.Services;

public class ClipboardImageCacheService
{
    private static readonly TimeSpan OrphanRetention = TimeSpan.FromDays(7);
    private readonly string _cacheDirectory;

    public ClipboardImageCacheService()
    {
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ImageGen",
            "ClipboardImages");
    }

    public string Save(BitmapSource image)
    {
        Directory.CreateDirectory(_cacheDirectory);

        string fileName = $"clipboard-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.png";
        string filePath = Path.Combine(_cacheDirectory, fileName);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        encoder.Save(stream);

        return filePath;
    }

    public bool IsManagedPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;

        try
        {
            string cacheRoot = Path.GetFullPath(_cacheDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(filePath);
            return candidate.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void DeleteIfManaged(string? filePath)
    {
        if (!IsManagedPath(filePath) || !File.Exists(filePath)) return;

        try
        {
            File.Delete(filePath!);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to delete cached clipboard image: {filePath}", ex);
        }
    }

    public void CleanupOrphans(IEnumerable<string?> referencedPaths)
    {
        if (!Directory.Exists(_cacheDirectory)) return;

        try
        {
            var referenced = referencedPaths
                .Where(path => IsManagedPath(path))
                .Select(path => Path.GetFullPath(path!))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            DateTime cutoff = DateTime.UtcNow - OrphanRetention;

            foreach (string filePath in Directory.EnumerateFiles(_cacheDirectory, "*.png"))
            {
                if (!referenced.Contains(Path.GetFullPath(filePath)) && File.GetLastWriteTimeUtc(filePath) < cutoff)
                {
                    DeleteIfManaged(filePath);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to clean clipboard image cache", ex);
        }
    }
}
