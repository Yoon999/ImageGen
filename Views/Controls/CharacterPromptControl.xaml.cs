using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ImageGen.Helpers;
using ImageGen.Models;
using ImageGen.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;

namespace ImageGen.Views.Controls;

public partial class CharacterPromptControl : UserControl
{
    public CharacterPromptControl()
    {
        InitializeComponent();
        IsVisibleChanged += CharacterPromptControl_IsVisibleChanged;
    }
    
    private void CharacterPromptControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            if (DataContext is CharacterPromptViewModel vm)
            {
                vm.RefreshPresetsCommand.Execute(null);
            }
        }
    }

    private void PromptBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var window = Window.GetWindow(this) as MainWindow;

        // MainWindow의 기존 로직을 재사용하기 위해 
        // MainWindow.xaml.cs에 있는 로직과 동일한 동작을 수행하도록 유도
        if (window?.DataContext is not MainViewModel mainViewModel) return;
        
        var textBox = sender as TextBox;
        if (textBox == null) return;

        string text = textBox.Text;
        int caretIndex = textBox.CaretIndex;

        if (caretIndex == 0)
        {
            mainViewModel.SearchTags(string.Empty);
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
                
        mainViewModel.SearchTags(currentWord);
    }

    private void PromptBox_GotFocus(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this) as MainWindow;
        window?.SetLastFocusedTextBox(sender as TextBox);
    }

    private void PromptBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var window = Window.GetWindow(this) as MainWindow;
        if (window?.DataContext is not MainViewModel mainViewModel) return;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            if (e.Key == Key.Enter)
            {
                if (!mainViewModel.GenerateCommand.CanExecute(null)) return;
                mainViewModel.GenerateCommand.Execute(null);
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

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is CharacterPromptViewModel vm)
        {
            if (e.NewValue is CharacterPreset preset)
            {
                vm.SelectedPreset = preset;
            }
            // 컬렉션이 Clear() 될 때 e.NewValue가 null로 들어오는데,
            // 이때 SelectedPreset을 null로 초기화해버리면 방금 저장/로드한 선택 상태가 풀려버립니다.
            // Clear는 명시적인 Clear 버튼을 통해서만 동작하도록 변경합니다.
        }
    }
}
