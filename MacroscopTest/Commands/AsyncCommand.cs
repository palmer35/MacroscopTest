using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace MacroscopTest.Commands;

/// <summary>
/// Executes an asynchronous action and prevents reentrancy while it is running.
/// </summary>
public sealed class AsyncCommand : ICommand, INotifyPropertyChanged
{
    private readonly Func<object?, Task> _executeAsync;
    private readonly Func<object?, bool>? _canExecute;

    private bool _isExecuting;

    public AsyncCommand(
        Func<Task> executeAsync,
        Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);

        _executeAsync = _ => executeAsync();
        _canExecute = canExecute is null ? null : _ => canExecute();
    }

    public AsyncCommand(
        Func<object?, Task> executeAsync,
        Func<object?, bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);

        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (_isExecuting == value)
            {
                return;
            }

            _isExecuting = value;
            OnPropertyChanged();
        } 
    }    

    public bool CanExecute(object? parameter)
    {
        return !IsExecuting && (_canExecute?.Invoke(parameter) ?? true);
    }

    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _ = ExecuteAsync(parameter);
    }

    public Task ExecuteAsync()
    {
        return ExecuteAsync(null);
    }

    public Task ExecuteAsync(object? parameter)
    {
        return !CanExecute(parameter)
            ? Task.CompletedTask
            : ExecuteInternalAsync(parameter);
    }

    public void RaiseCanExecuteChanged()
    {
        var handler = CanExecuteChanged;

        if (handler is null)
        {
            return;
        }

        InvokeOnUiThread(() => handler.Invoke(this, EventArgs.Empty));
    }

    private async Task ExecuteInternalAsync(object? parameter)
    {
        IsExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await _executeAsync(parameter);
        }
        catch
        {
            // Exceptions are handled at the ViewModel level.
        }
        finally
        {
            IsExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        var handler = PropertyChanged;

        if (handler is null)
        {
            return;
        }

        InvokeOnUiThread(() => handler.Invoke(this, new PropertyChangedEventArgs(propertyName)));
    }

    private static void InvokeOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(action);

            return;
        }

        action();
    }
}


