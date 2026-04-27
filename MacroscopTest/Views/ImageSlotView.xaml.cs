using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MacroscopTest.ViewModels;

namespace MacroscopTest.Views;

public partial class ImageSlotView
{
    public ImageSlotView()
    {
        InitializeComponent();
        SlotImage.MouseLeftButtonDown += OnImageMouseLeftButtonDown;
    }

    private void OnOpenPreviewClick(object sender, RoutedEventArgs e)
    {
        TryOpenPreview();
    }

    private void OnImageMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2)
        {
            return;
        }

        TryOpenPreview();
    }

    private void OnContainerMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source ||
            IsInsideInteractiveElement(source))
        {
            return;
        }

        var ownerWindow = Window.GetWindow(this);
        ownerWindow?.Focus();
        Keyboard.ClearFocus();
    }

    private void TryOpenPreview()
    {
        if (DataContext is not ImageSlotViewModel { ImageBytes.Length: > 0 } viewModel)
        {
            return;
        }

        var previewWindow = new ImagePreviewWindow(viewModel.ImageBytes, viewModel.Url)
        {
            Owner = Window.GetWindow(this)
        };

        previewWindow.Show();
    }

    private static bool IsInsideInteractiveElement(DependencyObject source)
    {
        var current = source;

        while (current is not null)
        {
            if (current is TextBox or Button or ComboBox)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
