using System.Windows;
using MacroscopTest.Services;
using MacroscopTest.ViewModels;

namespace MacroscopTest;

// ReSharper disable once UnusedMember.Global
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        ThemeService.ApplyTheme(AppTheme.Light);
        DataContext = new MainViewModel();
    }

    private void OnToggleThemeClick(object sender, RoutedEventArgs e)
    {
        ThemeService.ToggleTheme();
    }
}
