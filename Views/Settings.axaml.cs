using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OB;

public partial class Settings : UserControl
{
    public Settings()
    {
        InitializeComponent();
        DataContext= new ViewModels.SettingsViewModel();
    }
}