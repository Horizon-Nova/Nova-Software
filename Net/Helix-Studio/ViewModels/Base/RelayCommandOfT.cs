using System;
using System.Windows.Input;

namespace Helix_Studio.ViewModels.Base;

/// <summary>
/// 具參數的命令實作。
/// </summary>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    /// <summary>
    /// 建立命令。
    /// </summary>
    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter)
        => parameter is T typed ? _canExecute?.Invoke(typed) ?? true : _canExecute?.Invoke(default) ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        if (parameter is T typed)
        {
            _execute(typed);
            return;
        }

        _execute(default);
    }
}
