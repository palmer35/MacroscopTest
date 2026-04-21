using System.Windows;
using System.Windows.Input;

namespace MacroscopTest.Commands;

/// <summary>
/// Runs a simple synchronous action as ICommand and supports CanExecute updates.
/// </summary>
public sealed class DelegateCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public DelegateCommand(Action execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);

        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _execute();
    }

    public void RaiseCanExecuteChanged()
    {
        var handler = CanExecuteChanged;

        if (handler is null)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() =>
            {
                handler(this, EventArgs.Empty);
            });

            return;
        }

        handler(this, EventArgs.Empty);
    }
}

