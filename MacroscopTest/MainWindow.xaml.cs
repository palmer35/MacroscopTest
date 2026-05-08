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
        UpdateThemeControls();
        DataContext = new MainViewModel();
    }

    private void OnToggleThemeClick(object sender, RoutedEventArgs e)
    {
        ThemeService.ToggleTheme();
        UpdateThemeControls();
    }

    private void UpdateThemeControls()
    {
        var isDarkTheme = ThemeService.CurrentTheme == AppTheme.Dark;

        ThemeStatusText.Text = isDarkTheme ? "Dark" : "Light";
        ThemeToggleButton.Content = isDarkTheme ? "SWITCH TO LIGHT" : "SWITCH TO DARK";
    }
}
