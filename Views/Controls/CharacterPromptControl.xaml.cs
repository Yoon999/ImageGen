using System.Windows;
using System.Windows.Controls;
using ImageGen.ViewModels;
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
}
