using System.Windows;
using System.Windows.Controls;
using ImageGen.Models.Api;
using ImageGen.ViewModels;
using Button = System.Windows.Controls.Button;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace ImageGen.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();
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
        if (ViewModel == null) return;

        string text = PromptBox.Text;
        int caretIndex = PromptBox.CaretIndex;

        int start = text.LastIndexOfAny(new[] { ',', '\n' }, caretIndex - 1);
        if (start == -1)
        {
            start = 0;
        }
        else
        {
            start++;
        }

        int end = text.IndexOfAny(new[] { ',', '\n' }, caretIndex);
        if (end == -1)
        {
            end = text.Length;
        }

        string newText = text.Substring(0, start) + selectedTag.Tag + ", " + text.Substring(end);
        int newCaretIndex = start + selectedTag.Tag.Length + 2;

        ViewModel.Prompt = newText;
        PromptBox.CaretIndex = newCaretIndex;
        PromptBox.Focus(); // 입력창으로 포커스 복귀
        
        ViewModel.TagSuggestions.Clear();
    }

    private void ExifViewer_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel == null) return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                string filePath = files[0];
                string ext = System.IO.Path.GetExtension(filePath).ToLower();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp")
                {
                    ViewModel.LoadExifImage(filePath);
                }
            }
        }
    }
}
