using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MacroscopTest.Commands;
using MacroscopTest.Services;

namespace MacroscopTest.ViewModels;

public sealed class ImageSlotViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ImageDownloadService _imageDownloadService;
    private readonly FileLogger _logger;
    private readonly DelegateCommand _cancelCommand;

    private string _url = string.Empty;
    private BitmapImage? _image;
    private bool _isLoading;
    private string? _statusText;
    private string? _errorText;
    private CancellationTokenSource? _currentCancellationTokenSource;
    private int _currentOperationId;
    private bool _isDisposed;

    public ImageSlotViewModel(ImageDownloadService imageDownloadService, FileLogger logger)
    {
        ArgumentNullException.ThrowIfNull(imageDownloadService);
        ArgumentNullException.ThrowIfNull(logger);

        _imageDownloadService = imageDownloadService;
        _logger = logger;

        LoadCommand = new AsyncCommand(LoadAsync, CanLoad);
        _cancelCommand = new DelegateCommand(Cancel, CanCancel);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Url
    {
        get => _url;
        set
        {
            if (string.Equals(_url, value, StringComparison.Ordinal))
            {
                return;
            }

            _url = value;
            OnPropertyChanged();

            ErrorText = null;

            if (string.IsNullOrWhiteSpace(_url))
            {
                Image = null;
                StatusText = null;
            }

            UpdateCommandStates();
        }
    }


    public BitmapImage? Image
    {
        get => _image;
        private set
        {
            if (ReferenceEquals(_image, value))
            {
                return;
            }

            _image = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
            {
                return;
            }

            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public string? StatusText
    {
        get => _statusText;
        private set
        {
            if (string.Equals(_statusText, value, StringComparison.Ordinal))
            {
                return;
            }

            _statusText = value;
            OnPropertyChanged();
        }
    }

    public string? ErrorText
    {
        get => _errorText;
        private set
        {
            if (string.Equals(_errorText, value, StringComparison.Ordinal))
            {
                return;
            }

            _errorText = value;
            OnPropertyChanged();
        }
    }

    public AsyncCommand LoadCommand { get; }

    public ICommand CancelCommand => _cancelCommand;


    private bool CanLoad()
    {
        return !_isDisposed && !IsLoading && !string.IsNullOrWhiteSpace(Url);
    }

    private bool CanCancel()
    {
        return !_isDisposed &&
               IsLoading &&
               _currentCancellationTokenSource is not null &&
               !_currentCancellationTokenSource.IsCancellationRequested;
    }

    private async Task LoadAsync()
    {
        var currentUrl = Url;
        var formattedUrl = FormatUrlForLog(currentUrl);

        if (!IsValidUrl(currentUrl))
        {
            ErrorText = "Invalid URL.";
            StatusText = "Error";
            _logger.LogError($"Invalid image URL: {formattedUrl}");
            UpdateCommandStates();

            return;
        }

        var operationId = unchecked(++_currentOperationId);
        var cancellationTokenSource = new CancellationTokenSource();

        _currentCancellationTokenSource = cancellationTokenSource;
        IsLoading = true;
        Image = null;
        ErrorText = null;
        StatusText = "Loading...";
        _logger.LogInfo($"Image download started. URL: {formattedUrl}");
        UpdateCommandStates();

        try
        {
            var downloadedImage = await _imageDownloadService.DownloadAsync(currentUrl, cancellationTokenSource.Token);

            if (cancellationTokenSource.IsCancellationRequested ||
                !IsCurrentOperation(operationId, cancellationTokenSource))
            {
                return;
            }

            Image = downloadedImage;
            StatusText = "Loaded";
            _logger.LogInfo($"Image downloaded successfully. URL: {formattedUrl}");
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            _logger.LogInfo($"Image download cancelled. URL: {formattedUrl}");
        }
        catch (Exception exception)
        {
            if (!IsCurrentOperation(operationId, cancellationTokenSource))
            {
                return;
            }

            ErrorText = "Failed to load the image. Please check the URL.";
            StatusText = "Error";
            _logger.LogError($"Image download failed. URL: {formattedUrl}", exception);
        }
        finally
        {
            if (IsCurrentOperation(operationId, cancellationTokenSource))
            {
                _currentCancellationTokenSource = null;
                IsLoading = false;
                UpdateCommandStates();
            }

            cancellationTokenSource.Dispose();
        }
    }

    private void Cancel()
    {
        var cancellationTokenSource = _currentCancellationTokenSource;

        if (cancellationTokenSource is null || cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        StatusText = "Cancelled";
        cancellationTokenSource.Cancel();
        UpdateCommandStates();
    }

    private bool IsCurrentOperation(int operationId, CancellationTokenSource cancellationTokenSource)
    {
        return operationId == _currentOperationId &&
               ReferenceEquals(_currentCancellationTokenSource, cancellationTokenSource);
    }

    private void UpdateCommandStates()
    {
        LoadCommand.RaiseCanExecuteChanged();
        _cancelCommand.RaiseCanExecuteChanged();
    }

    private static bool IsValidUrl(string? url)
    {
        return !string.IsNullOrWhiteSpace(url) &&
               Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static string FormatUrlForLog(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "<empty>";
        }

        var normalized = url
            .Replace(Environment.NewLine, " ")
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Trim();

        if (normalized.Length > 120)
        {
            return string.Concat(normalized.AsSpan(0, 117), "...");
        }

        return normalized;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        var cancellationTokenSource = _currentCancellationTokenSource;
        _currentCancellationTokenSource = null;

        if (cancellationTokenSource is null)
        {
            return;
        }

        try
        {
            cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            cancellationTokenSource.Dispose();
        }
    }
}
