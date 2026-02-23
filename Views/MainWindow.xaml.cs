using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ImageGen.Helpers;
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
