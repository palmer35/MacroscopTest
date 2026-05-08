using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MacroscopTest.Services;
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

    private void OnCopyImageClick(object sender, RoutedEventArgs e)
    {
        if (!TryGetImageData(out var viewModel, out var imageBytes))
        {
            return;
        }

        try
        {
            var imageSource = viewModel.Image ?? CreateImageFromBytes(imageBytes);
            Clipboard.SetImage(imageSource);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Failed to copy image.{Environment.NewLine}{exception.Message}",
                "Copy Image",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnSaveImageClick(object sender, RoutedEventArgs e)
    {
        if (!TryGetImageData(out var viewModel, out var imageBytes))
        {
            return;
        }

        var extension = GetSuggestedExtension(viewModel.Url);
        var dialog = new SaveFileDialog
        {
            Title = "Save image",
            FileName = $"image{extension}",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.tiff|All files|*.*",
            DefaultExt = extension,
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        try
        {
            File.WriteAllBytes(dialog.FileName, imageBytes);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Failed to save image.{Environment.NewLine}{exception.Message}",
                "Save Image",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
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

        private void OnUrlTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ImageSlotViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            _ = viewModel.LoadCommand.ExecuteAsync();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape)
        {
            return;
        }

        if (viewModel.CancelCommand.CanExecute(null))
        {
            viewModel.CancelCommand.Execute(null);
        }

        e.Handled = true;
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

    private bool TryGetImageData(out ImageSlotViewModel viewModel, out byte[] imageBytes)
    {
        if (DataContext is ImageSlotViewModel { ImageBytes.Length: > 0, IsLoading: false } slotViewModel)
        {
            viewModel = slotViewModel;
            imageBytes = slotViewModel.ImageBytes;

            return true;
        }

        viewModel = null!;
        imageBytes = Array.Empty<byte>();

        return false;
    }

    private static BitmapSource CreateImageFromBytes(byte[] imageBytes)
    {
        return ImageDownloadService.CreateBitmapImage(imageBytes);
    }

    private static string GetSuggestedExtension(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return ".png";
        }

        var extension = Path.GetExtension(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".png";
        }

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp" or ".tif" or ".tiff" => extension,
            _ => ".png"
        };
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
