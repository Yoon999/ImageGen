using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.IO;
using ImageGen.Helpers;
using ImageGen.Models.Api;
using ImageGen.ViewModels;
using Button = System.Windows.Controls.Button;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;
using Clipboard = System.Windows.Clipboard;
using BitmapImage = System.Windows.Media.Imaging.BitmapImage;
using BitmapSource = System.Windows.Media.Imaging.BitmapSource;
using ClipboardDataObject = System.Windows.IDataObject;

namespace ImageGen.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;
    private TextBox? _lastFocusedTextBox;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel == null) return;

        if (e.Key == Key.Escape && ViewModel.IsImagePasteOverlayOpen)
        {
            ViewModel.DismissPastedImageCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.V
            || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return;
        }

        try
        {
            if (ViewModel.SelectedMainTabIndex == 3)
            {
                var dataObject = Clipboard.GetDataObject();
                string? imagePath = GetClipboardImagePath(dataObject);
                if (imagePath != null)
                {
                    ViewModel.LoadExifImage(imagePath);
                    e.Handled = true;
                    return;
                }

                byte[]? encodedImage = GetClipboardEncodedImage(dataObject);
                if (encodedImage != null)
                {
                    ViewModel.LoadExifImage(encodedImage);
                    e.Handled = true;
                    return;
                }

                BitmapSource? clipboardImage = GetClipboardImage();
                if (clipboardImage != null)
                {
                    ViewModel.LoadExifImage(clipboardImage);
                    e.Handled = true;
                }

                return;
            }

            if (ViewModel.SelectedMainTabIndex != 0) return;

            BitmapSource? image = GetClipboardImage();
            if (image == null) return;

            ViewModel.ShowPastedImage(image);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Failed to read clipboard image: {ex.Message}";
            Logger.LogError("Failed to read clipboard image", ex);
            e.Handled = true;
        }
    }

    private static BitmapSource? GetClipboardImage()
    {
        if (Clipboard.ContainsImage())
        {
            return Clipboard.GetImage();
        }

        if (!Clipboard.ContainsFileDropList()) return null;

        string? imagePath = Clipboard.GetFileDropList()
            .Cast<string>()
            .FirstOrDefault(IsSupportedImagePath);
        if (imagePath == null) return null;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(imagePath, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static string? GetClipboardImagePath(ClipboardDataObject? dataObject)
    {
        if (dataObject == null || !dataObject.GetDataPresent(DataFormats.FileDrop)) return null;

        return (dataObject.GetData(DataFormats.FileDrop) as string[])?
            .FirstOrDefault(IsSupportedImagePath);
    }

    private static byte[]? GetClipboardEncodedImage(ClipboardDataObject? dataObject)
    {
        if (dataObject == null) return null;

        foreach (string format in new[] { "PNG", "JFIF", "TIFF" })
        {
            if (!dataObject.GetDataPresent(format, false)) continue;

            object? data = dataObject.GetData(format, false);
            if (data is byte[] bytes && bytes.Length > 0)
            {
                return bytes;
            }

            if (data is Stream stream)
            {
                long originalPosition = stream.CanSeek ? stream.Position : 0;
                try
                {
                    if (stream.CanSeek) stream.Position = 0;
                    using var copy = new MemoryStream();
                    stream.CopyTo(copy);
                    if (copy.Length > 0) return copy.ToArray();
                }
                finally
                {
                    if (stream.CanSeek) stream.Position = originalPosition;
                }
            }
        }

        return null;
    }

    private static bool IsSupportedImagePath(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp";
    }

    private void ImagePasteBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.DismissPastedImageCommand.Execute(null);
        e.Handled = true;
    }

    private void PromptBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel == null) return;

        var textBox = sender as TextBox;
        if (textBox == null) return;

        string text = textBox.Text;
        int caretIndex = textBox.CaretIndex;

        if (caretIndex == 0)
        {
            ViewModel.SearchTags(string.Empty);
            return;
        }

        int start = text.LastIndexOfAny(new[] { ',', '\n' }, caretIndex - 1);
        if (start == -1)
        {
            start = 0;
        }
        else
        {
            start++;
        }

        string currentWord = text.Substring(start, caretIndex - start).Trim();
        
        ViewModel.SearchTags(currentWord);
    }

    private void PromptBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            _lastFocusedTextBox = textBox;
        }
    }
    
    private void PromptBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            if (ViewModel != null && ViewModel.GenerateCommand.CanExecute(null))
            {
                ViewModel.GenerateCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (e.Key == Key.Up)
            {
                if (sender is TextBox textBox)
                {
                    TextBoxHelper.AdjustWeight(textBox, 0.1);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Down)
            {
                if (sender is TextBox textBox)
                {
                    TextBoxHelper.AdjustWeight(textBox, -0.1);
                    e.Handled = true;
                }
            }
        }
    }
    
    public void SetLastFocusedTextBox(TextBox? textBox)
    {
        _lastFocusedTextBox = textBox;
    }

    // 태그 버튼 클릭 핸들러
    private void TagSuggestionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is TagSuggestion suggestion)
        {
            ApplyTag(suggestion);
        }
    }
    
    private void ApplyTag(TagSuggestion selectedTag)
    {
        if (ViewModel == null || _lastFocusedTextBox == null) return;

        TextBoxHelper.ApplyTag(_lastFocusedTextBox, selectedTag);
        
        ViewModel.TagSuggestions.Clear();
    }

    private void ExifViewer_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel == null) return;

        var filePath = GetDroppedImagePath(e);
        if (filePath != null)
        {
            ViewModel.LoadExifImage(filePath);
        }
    }

    private void SourceImage_Drop(object sender, DragEventArgs e)
    {
        var filePath = GetDroppedImagePath(e);
        if (filePath != null)
        {
            ViewModel?.LoadSourceImage(filePath);
        }
    }

    private void CharacterReference_Drop(object sender, DragEventArgs e)
    {
        var filePath = GetDroppedImagePath(e);
        if (filePath != null)
        {
            ViewModel?.LoadCharacterReference(filePath);
        }
    }

    private void DirectorInput_Drop(object sender, DragEventArgs e)
    {
        var filePath = GetDroppedImagePath(e);
        if (filePath != null)
        {
            ViewModel?.DirectorToolsViewModel.LoadInputImage(filePath);
        }
    }

    private static string? GetDroppedImagePath(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return null;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length == 0)
        {
            return null;
        }

        var filePath = files[0];
        var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".webp" ? filePath : null;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        bool canClose = ViewModel.ConfirmClose();
        e.Cancel = !canClose;

        if (canClose)
        {
            NodeGraphControlView.PrepareForClose();
        }
    }
}
