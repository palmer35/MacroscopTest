using MacroscopTest.ViewModels;

namespace MacroscopTest;

// ReSharper disable once UnusedMember.Global
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
