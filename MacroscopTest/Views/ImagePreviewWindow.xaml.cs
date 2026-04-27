using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MacroscopTest.Services;

namespace MacroscopTest.Views;

public partial class ImagePreviewWindow
{
    private const double FitDecodeScale = 2.0;
    private const double ZoomStep = 1.15;
    private const double MinZoom = 0.02;
    private const double MaxZoom = 32.0;

    private readonly byte[] _imageBytes;

    private int _decodeVersion;
    private bool _isActualSize;
    private bool _isFullImageLoaded;
    private bool _isFullImageLoading;
    private double _zoom = 1.0;
    private bool _isPanning;
    private Point _panStartPoint;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;

    public ImagePreviewWindow(byte[] imageBytes, string title)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);

        _imageBytes = imageBytes;

        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RunPreviewAction(ShowFitImageAsync);
    }

    private void OnFitClick(object sender, RoutedEventArgs e)
    {
        RunPreviewAction(ShowFitImageAsync);
    }

    private void OnActualSizeClick(object sender, RoutedEventArgs e)
    {
        RunPreviewAction(ShowActualSizeImageAsync);
    }

    private void OnZoomInClick(object sender, RoutedEventArgs e)
    {
        RunPreviewAction(() => ShowZoomImageAsync(zoomIn: true, GetViewportCenter()));
    }

    private void OnZoomOutClick(object sender, RoutedEventArgs e)
    {
        RunPreviewAction(() => ShowZoomImageAsync(zoomIn: false, GetViewportCenter()));
    }

    private void OnImageScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return;
        }

        e.Handled = true;

        if (_isFullImageLoading)
        {
            return;
        }

        var viewportPoint = e.GetPosition(ImageScrollViewer);
        RunPreviewAction(() => ShowZoomImageAsync(e.Delta > 0, viewportPoint));
    }

    private void RunPreviewAction(Func<Task> action)
    {
        _ = RunPreviewActionAsync(action);
    }

    private async Task RunPreviewActionAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch
        {
            StatusTextBlock.Text = "Failed to open image.";
        }
    }

    private void OnImageScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isActualSize)
        {
            UpdateFitLayout();
        }
    }

    private void OnImageScrollViewerPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isActualSize || PreviewImage.Source is null)
        {
            return;
        }

        _isPanning = true;
        _panStartPoint = e.GetPosition(ImageScrollViewer);
        _panStartHorizontalOffset = ImageScrollViewer.HorizontalOffset;
        _panStartVerticalOffset = ImageScrollViewer.VerticalOffset;
        ImageScrollViewer.CaptureMouse();
        Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void OnImageScrollViewerPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        StopPanning();
        e.Handled = true;
    }

    private void OnImageScrollViewerPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        var currentPoint = e.GetPosition(ImageScrollViewer);
        var delta = currentPoint - _panStartPoint;

        ImageScrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - delta.X);
        ImageScrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - delta.Y);
    }

    private async Task ShowFitImageAsync()
    {
        _isActualSize = false;
        _zoom = 1.0;
        ImageScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        ImageScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        PreviewImage.Stretch = Stretch.Uniform;
        PreviewImage.LayoutTransform = null;
        UpdateFitLayout();
        StopPanning();

        var decodeWidth = Math.Max(1, (int)Math.Ceiling(GetViewportWidth() * FitDecodeScale * GetDpiScaleX()));

        await SetImageAsync(decodeWidth, "Fit to screen", false);
    }

    private async Task ShowActualSizeImageAsync()
    {
        _isActualSize = true;
        _zoom = 1.0;
        ImageScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        ImageScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        PreviewImage.Stretch = Stretch.None;
        PreviewImage.LayoutTransform = null;
        PreviewImage.Width = double.NaN;
        PreviewImage.Height = double.NaN;
        StopPanning();

        await SetImageAsync(null, "Actual size", true);
    }

    private async Task ShowZoomImageAsync(bool zoomIn, Point viewportPoint)
    {
        var wasFitMode = !_isActualSize;

        if (!_isFullImageLoaded)
        {
            _isFullImageLoading = true;

            try
            {
                await ShowActualSizeImageAsync();
            }
            finally
            {
                _isFullImageLoading = false;
            }
        }

        if (!_isFullImageLoaded)
        {
            return;
        }

        var currentZoom = wasFitMode ? GetFitZoom() : _zoom;
        var targetZoom = zoomIn ? currentZoom * ZoomStep : currentZoom / ZoomStep;

        ApplyZoom(ClampZoom(targetZoom), viewportPoint);
    }

    private async Task SetImageAsync(int? decodePixelWidth, string status, bool isFullImage)
    {
        var decodeVersion = unchecked(++_decodeVersion);
        StatusTextBlock.Text = "Loading...";

        try
        {
            var image = await Task.Run(() => ImageDownloadService.CreateBitmapImage(_imageBytes, decodePixelWidth));

            if (decodeVersion != _decodeVersion)
            {
                return;
            }

            PreviewImage.Source = image;
            _isFullImageLoaded = isFullImage;
            StatusTextBlock.Text = status;
        }
        catch
        {
            if (decodeVersion == _decodeVersion)
            {
                StatusTextBlock.Text = "Failed to open image.";
            }
        }
    }

    private void UpdateFitLayout()
    {
        PreviewImage.Width = Math.Max(1, GetViewportWidth());
        PreviewImage.Height = Math.Max(1, GetViewportHeight());
    }

    private void ApplyZoom(double zoom, Point viewportPoint)
    {
        if (PreviewImage.Source is null)
        {
            return;
        }

        var previousZoom = _zoom <= 0 ? 1.0 : _zoom;
        var anchorX = (ImageScrollViewer.HorizontalOffset + viewportPoint.X) / previousZoom;
        var anchorY = (ImageScrollViewer.VerticalOffset + viewportPoint.Y) / previousZoom;

        _isActualSize = true;
        _zoom = zoom;

        ImageScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        ImageScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        PreviewImage.Stretch = Stretch.None;
        PreviewImage.Width = double.NaN;
        PreviewImage.Height = double.NaN;
        PreviewImage.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        ImageScrollViewer.UpdateLayout();

        var targetHorizontalOffset = Math.Max(0, anchorX * _zoom - viewportPoint.X);
        var targetVerticalOffset = Math.Max(0, anchorY * _zoom - viewportPoint.Y);

        ImageScrollViewer.ScrollToHorizontalOffset(targetHorizontalOffset);
        ImageScrollViewer.ScrollToVerticalOffset(targetVerticalOffset);

        StatusTextBlock.Text = $"{Math.Round(_zoom * 100)}%";
    }

    private double GetFitZoom()
    {
        if (PreviewImage.Source is null)
        {
            return 1.0;
        }

        var widthZoom = GetViewportWidth() / PreviewImage.Source.Width;
        var heightZoom = GetViewportHeight() / PreviewImage.Source.Height;

        return ClampZoom(Math.Min(widthZoom, heightZoom));
    }

    private static double ClampZoom(double zoom)
    {
        return Math.Clamp(zoom, MinZoom, MaxZoom);
    }

    private Point GetViewportCenter()
    {
        return new Point(GetViewportWidth() / 2, GetViewportHeight() / 2);
    }

    private double GetDpiScaleX()
    {
        var source = PresentationSource.FromVisual(this);

        return source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
    }

    private void StopPanning()
    {
        _isPanning = false;
        ImageScrollViewer.ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
    }

    private double GetViewportWidth()
    {
        if (ImageScrollViewer.ViewportWidth > 1)
        {
            return ImageScrollViewer.ViewportWidth;
        }

        return ImageScrollViewer.ActualWidth > 1
            ? ImageScrollViewer.ActualWidth
            : 1200;
    }

    private double GetViewportHeight()
    {
        if (ImageScrollViewer.ViewportHeight > 1)
        {
            return ImageScrollViewer.ViewportHeight;
        }

        return ImageScrollViewer.ActualHeight > 1
            ? ImageScrollViewer.ActualHeight
            : 800;
    }
}
