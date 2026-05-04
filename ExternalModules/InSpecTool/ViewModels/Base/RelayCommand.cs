using System.Windows.Input;

namespace ExternalModules.InSpecTool.ViewModels.Base;

/// <summary>
/// 以委派包裝 WPF <see cref="ICommand"/> 的通用命令類別。
/// </summary>
internal sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _applyCommand;
    private readonly Func<object?, bool>? _validateCanApply;

    /// <summary>
    /// 初始化帶參數的命令。
    /// </summary>
    /// <param name="applyCommand">命令執行邏輯。</param>
    /// <param name="validateCanApply">命令是否可執行的判斷。</param>
    public RelayCommand(Action<object?> applyCommand, Func<object?, bool>? validateCanApply = null)
    {
        _applyCommand = applyCommand;
        _validateCanApply = validateCanApply;
    }

    /// <summary>
    /// 初始化無參數的命令。
    /// </summary>
    /// <param name="applyCommand">命令執行邏輯。</param>
    /// <param name="validateCanApply">命令是否可執行的判斷。</param>
    public RelayCommand(Action applyCommand, Func<bool>? validateCanApply = null)
        : this(_ => applyCommand(), validateCanApply is null ? null : _ => validateCanApply())
    {
    }

    /// <summary>
    /// 當 WPF 要求重新查詢命令是否可執行時引發。
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>
    /// 判斷命令目前是否可執行。
    /// </summary>
    /// <param name="parameter">命令參數。</param>
    /// <returns>若可執行則回傳 <c>true</c>。</returns>
    public bool CanExecute(object? parameter) => _validateCanApply?.Invoke(parameter) ?? true;

    /// <summary>
    /// 執行命令。
    /// </summary>
    /// <param name="parameter">命令參數。</param>
    public void Execute(object? parameter) => _applyCommand(parameter);

    /// <summary>
    /// 要求 WPF 重新評估所有命令的可執行狀態。
    /// </summary>
    public static void ApplyInvalidate() => CommandManager.InvalidateRequerySuggested();
}

