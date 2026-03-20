using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ImageGen.Models;

namespace ImageGen.Views;

public partial class PresetSaveWindow : Window
{
    public string SavePath { get; private set; } = string.Empty;

    public PresetSaveWindow(List<CharacterPreset> presets, string initialPath)
    {
        InitializeComponent();
        PresetTreeView.ItemsSource = presets;
        SavePathTextBox.Text = initialPath;
    }

    private void PresetTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is CharacterPreset preset)
        {
            if (preset.IsFolder)
            {
                // If a folder is selected, append a slash to indicate saving inside it
                SavePathTextBox.Text = preset.FullPath + "/";
            }
            else
            {
                // If a file is selected, pre-fill its full path (for overwriting)
                SavePathTextBox.Text = preset.FullPath;
            }
            // Move cursor to the end
            SavePathTextBox.CaretIndex = SavePathTextBox.Text.Length;
        }
    }

    private void SavePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(SavePathTextBox.Text) && 
                               !SavePathTextBox.Text.EndsWith("/");
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SavePath = SavePathTextBox.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
