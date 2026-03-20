using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ImageGen.Models;

namespace ImageGen.Views;

public partial class PresetSelectionWindow : Window
{
    public CharacterPreset? SelectedPreset { get; private set; }

    public PresetSelectionWindow(List<CharacterPreset> presets)
    {
        InitializeComponent();
        PresetTreeView.ItemsSource = presets;
    }

    private void PresetTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is CharacterPreset preset)
        {
            SelectedPreset = preset;
            SelectedPathText.Text = preset.FullPath;
            // 폴더가 아닐 때만 Load 버튼 활성화
            LoadButton.IsEnabled = !preset.IsFolder;
        }
        else
        {
            SelectedPreset = null;
            SelectedPathText.Text = "";
            LoadButton.IsEnabled = false;
        }
    }

    private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem item && item.DataContext is CharacterPreset preset)
        {
            // 폴더 더블클릭시에는 확장/축소만 하고 창을 닫지 않음
            if (!preset.IsFolder)
            {
                SelectedPreset = preset;
                DialogResult = true;
                Close();
            }
        }
        // 이벤트 전파 중단
        e.Handled = true;
    }

    private void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPreset != null && !SelectedPreset.IsFolder)
        {
            DialogResult = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
