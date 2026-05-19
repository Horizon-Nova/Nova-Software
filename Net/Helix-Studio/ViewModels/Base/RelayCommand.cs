using System;
using System.Windows.Input;

namespace Helix_Studio.ViewModels.Base;

/// <summary>
/// 提供 <see cref="ICommand"/> 的通用封裝。
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool> _canExecute;

    /// <summary>
    /// 建立無參數命令。
    /// </summary>
    public RelayCommand(Action execute)
        : this(_ => execute(), _ => true)
    {
    }

    /// <summary>
    /// 建立具參數命令。
    /// </summary>
    public RelayCommand(Action<object?> execute)
        : this(execute, _ => true)
    {
    }

    /// <summary>
    /// 建立具參數與條件判斷的命令。
    /// </summary>
    public RelayCommand(Action<object?> execute, Func<object?, bool> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => _canExecute(parameter);

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>
    /// 通知 WPF 重新評估命令是否可執行。
    /// </summary>
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
