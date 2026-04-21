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
    private readonly ObservableCollection<ImageSlotViewModel> _slots;

    private int _activeLoadsCount;
    private bool _isDisposed;

    public MainViewModel()
        : this(new ImageDownloadService(), new FileLogger())
    {
    }

    public MainViewModel(ImageDownloadService imageDownloadService, FileLogger logger)
    {
        ArgumentNullException.ThrowIfNull(imageDownloadService);
        ArgumentNullException.ThrowIfNull(logger);

        _slots = new ObservableCollection<ImageSlotViewModel>();
        Slots = new ReadOnlyObservableCollection<ImageSlotViewModel>(_slots);
        LoadAllCommand = new AsyncCommand(LoadAllAsync, CanLoadAll);

        CreateSlots(imageDownloadService, logger);
        UpdateActiveLoadsCount();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReadOnlyObservableCollection<ImageSlotViewModel> Slots { get; }

    public AsyncCommand LoadAllCommand { get; }

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
        }
    }

    private bool CanLoadAll()
    {
        return !_isDisposed && HasLoadableSlots();
    }

    private async Task LoadAllAsync()
    {
        var tasks = (from slot in _slots where slot.LoadCommand.CanExecute(null) select slot.LoadCommand.ExecuteAsync()).ToList();

        if (tasks.Count == 0)
        {
            return;
        }

        await Task.WhenAll(tasks);
    }

    private void CreateSlots(ImageDownloadService imageDownloadService, FileLogger logger)
    {
        for (var index = 0; index < 3; index++)
        {
            var slot = new ImageSlotViewModel(imageDownloadService, logger);

            _slots.Add(slot);
            SubscribeSlot(slot);
        }
    }

    private bool HasLoadableSlots()
    {
        return _slots.Any(slot => slot.LoadCommand.CanExecute(null));
    }

    private void UpdateActiveLoadsCount()
    {
        var count = _slots.Count(slot => slot.IsLoading);

        ActiveLoadsCount = count;
    }

    private void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageSlotViewModel.IsLoading))
        {
            UpdateActiveLoadsCount();
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
        LoadAllCommand.RaiseCanExecuteChanged();
    }
}
