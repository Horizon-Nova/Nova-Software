using System.Windows.Input;

namespace Helix_Studio.ViewModels.Base;

/// <summary>以委派實作 <see cref="ICommand"/> 的萬用命令，可選擇性提供可執行條件。</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    /// <summary>以帶參數的執行與可執行委派建立命令。</summary>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    /// <summary>以無參數的執行與可執行委派建立命令（內部轉成 <see cref="object"/> 參數形式）。</summary>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute()) { }

    /// <summary>當 <see cref="CommandManager.RequerySuggested"/> 建議重查可執行狀態時觸發。</summary>
    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>若未提供可執行委派則永遠為 <c>true</c>，否則以 <paramref name="parameter"/> 呼叫委派。</summary>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <summary>以 <paramref name="parameter"/> 執行命令本體。</summary>
    public void Execute(object? parameter)    => _execute(parameter);

    /// <summary>要求 WPF 重新評估所有命令的 <see cref="CanExecute(object)"/>。</summary>
    public static void InvalidateAll() => CommandManager.InvalidateRequerySuggested();
}

