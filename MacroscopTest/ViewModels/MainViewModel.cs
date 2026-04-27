using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MacroscopTest.Commands;
using MacroscopTest.Services;

namespace MacroscopTest.ViewModels;

/// <summary>
/// Coordinates image slots and manages batch loading and overall progress.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private const int DefaultSlotCount = 3;

    private readonly ObservableCollection<ImageSlotViewModel> _slots;

    private int _activeLoadsCount;
    private double _overallDownloadProgress;
    private bool _isDisposed;

    public MainViewModel()
        : this(new ImageDownloadService(), new FileLogger(), DefaultSlotCount)
    {
    }

    public MainViewModel(ImageDownloadService imageDownloadService, FileLogger logger)
        : this(imageDownloadService, logger, DefaultSlotCount)
    {
    }

    public MainViewModel(ImageDownloadService imageDownloadService, FileLogger logger, int slotCount)
    {
        ArgumentNullException.ThrowIfNull(imageDownloadService);
        ArgumentNullException.ThrowIfNull(logger);

        if (slotCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCount));
        }

        _slots = new ObservableCollection<ImageSlotViewModel>();
        Slots = new ReadOnlyObservableCollection<ImageSlotViewModel>(_slots);
        LoadAllCommand = new AsyncCommand(LoadAllAsync, CanLoadAll);

        CreateSlots(imageDownloadService, logger, slotCount);
        UpdateActiveLoadsCount();
        UpdateOverallDownloadProgress();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReadOnlyObservableCollection<ImageSlotViewModel> Slots { get; }

    public AsyncCommand LoadAllCommand { get; }

    public int SlotsCount => _slots.Count;

    public string ProgressSummary => $"{OverallDownloadProgress:0}% - Loading: {ActiveLoadsCount} of {SlotsCount}";

    public double OverallDownloadProgress
    {
        get => _overallDownloadProgress;
        private set
        {
            if (Math.Abs(_overallDownloadProgress - value) < 0.01)
            {
                return;
            }

            _overallDownloadProgress = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressSummary));
        }
    }

    public int ActiveLoadsCount
    {
        get => _activeLoadsCount;
        private set
        {
            if (_activeLoadsCount == value)
            {
                return;
            }

            _activeLoadsCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressSummary));
        }
    }

    private bool CanLoadAll()
    {
        return !_isDisposed && HasLoadableSlots();
    }

    private Task LoadAllAsync()
    {
        foreach (var slot in _slots)
        {
            if (!CanStartLoading(slot))
            {
                continue;
            }

            _ = slot.LoadCommand.ExecuteAsync();
        }

        LoadAllCommand.RaiseCanExecuteChanged();

        return Task.CompletedTask;
    }

    private void CreateSlots(ImageDownloadService imageDownloadService, FileLogger logger, int slotCount)
    {
        for (var index = 0; index < slotCount; index++)
        {
            var slot = new ImageSlotViewModel(imageDownloadService, logger);

            _slots.Add(slot);
            SubscribeSlot(slot);
        }
    }

    private bool HasLoadableSlots()
    {
        return _slots.Any(CanStartLoading);
    }

    private static bool CanStartLoading(ImageSlotViewModel slot)
    {
        return !slot.IsLoading && !string.IsNullOrWhiteSpace(slot.Url);
    }

    private void UpdateActiveLoadsCount()
    {
        var count = _slots.Count(slot => slot.IsLoading);

        ActiveLoadsCount = count;
    }

    private void UpdateOverallDownloadProgress()
    {
        double totalProgress = 0;
        var count = 0;

        foreach (var slot in _slots)
        {
            if (!IsProgressParticipant(slot))
            {
                continue;
            }

            totalProgress += slot.DownloadProgress;
            count++;
        }

        OverallDownloadProgress = count == 0 ? 0 : totalProgress / count;
    }

    private static bool IsProgressParticipant(ImageSlotViewModel slot)
    {
        return slot.IsLoading || slot.Image is not null;
    }

    private void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageSlotViewModel.IsLoading))
        {
            UpdateActiveLoadsCount();
        }

        if (e.PropertyName is nameof(ImageSlotViewModel.IsLoading) or
            nameof(ImageSlotViewModel.Image) or
            nameof(ImageSlotViewModel.DownloadProgress) or
            nameof(ImageSlotViewModel.Url))
        {
            UpdateOverallDownloadProgress();
        }

        if (e.PropertyName is nameof(ImageSlotViewModel.IsLoading) or nameof(ImageSlotViewModel.Url))
        {
            LoadAllCommand.RaiseCanExecuteChanged();
        }
    }

    private void SubscribeSlot(ImageSlotViewModel slot)
    {
        slot.PropertyChanged += OnSlotPropertyChanged;
    }

    private void UnsubscribeSlot(ImageSlotViewModel slot)
    {
        slot.PropertyChanged -= OnSlotPropertyChanged;
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

        foreach (var slot in _slots)
        {
            UnsubscribeSlot(slot);
            slot.Dispose();
        }

        _slots.Clear();
        ActiveLoadsCount = 0;
        OverallDownloadProgress = 0;
        OnPropertyChanged(nameof(SlotsCount));
        OnPropertyChanged(nameof(ProgressSummary));
        LoadAllCommand.RaiseCanExecuteChanged();
    }
}
