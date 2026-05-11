using System;
using System.Windows.Input;

namespace Helix_Studio.ViewModels.Base;

/// <summary>
/// 提供 `ICommand` 的通用封裝。
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool> _canExecute;

    /// <summary>
    /// 建立無參數命令。
    /// </summary>
    /// <param name="execute">命令執行委派。</param>
    public RelayCommand(Action execute)
        : this(_ => execute(), _ => true)
    {
    }

    /// <summary>
    /// 建立具參數命令。
    /// </summary>
    /// <param name="execute">命令執行委派。</param>
    public RelayCommand(Action<object?> execute)
        : this(execute, _ => true)
    {
    }

    /// <summary>
    /// 建立具參數與條件判斷的命令。
    /// </summary>
    /// <param name="execute">命令執行委派。</param>
    /// <param name="canExecute">命令是否可執行的判斷委派。</param>
    public RelayCommand(Action<object?> execute, Func<object?, bool> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>
    /// 查詢命令目前是否可執行。
    /// </summary>
    /// <param name="parameter">命令參數。</param>
    public bool CanExecute(object? parameter) => _canExecute(parameter);

    /// <summary>
    /// 命令可執行狀態變更事件。
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// 執行命令。
    /// </summary>
    /// <param name="parameter">命令參數。</param>
    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>
    /// 通知 WPF 重新評估命令是否可執行。
    /// </summary>
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
