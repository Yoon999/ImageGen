using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ImageGen.Models.Api;
using ImageGen.ViewModels;
using Button = System.Windows.Controls.Button;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

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

        var textBox = _lastFocusedTextBox;
        string text = textBox.Text;
        int caretIndex = textBox.CaretIndex;

        // 커서 위치가 텍스트 범위를 벗어나는 경우 보정
        if (caretIndex > text.Length) caretIndex = text.Length;
        if (caretIndex < 0) caretIndex = 0;

        // 구분자 위치 찾기
        int delimiterIndex = -1;
        if (text.Length > 0)
        {
            delimiterIndex = text.LastIndexOfAny(new[] { ',', '\n' }, Math.Min(Math.Max(0, caretIndex - 1), text.Length - 1));
        }

        int start;
        string prefix = "";

        if (delimiterIndex == -1)
        {
            start = 0;
        }
        else
        {
            start = delimiterIndex + 1;
            // 구분자가 쉼표인 경우에만 공백 추가
            if (text[delimiterIndex] == ',')
            {
                prefix = " ";
            }
        }

        int end = text.IndexOfAny(new[] { ',', '\n' }, caretIndex);
        if (end == -1)
        {
            end = text.Length;
        }

        // 기존 단어 교체
        string newText = text.Substring(0, start) + prefix + selectedTag.Tag + ", " + text.Substring(end);
        int newCaretIndex = start + prefix.Length + selectedTag.Tag.Length + 2;
        
        if (textBox.Name == "PromptBox") // Positive Prompt
        {
            ViewModel.Prompt = newText;
        }
        else 
        {
            // TextBox의 Text 속성을 직접 변경하고, 바인딩을 업데이트
            textBox.Text = newText;
            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();
        }

        // 커서 위치 복원 및 포커스
        textBox.CaretIndex = newCaretIndex;
        textBox.Focus();
        
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
                if (ext is ".png" or ".jpg" or ".jpeg" or ".webp")
                {
                    ViewModel.LoadExifImage(filePath);
                }
            }
        }
    }
}
