using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Vaeron.Kernel.Guarding;

namespace Vaeron.VNCSCP.Internal.Wpf;

/// <summary>畫面繫結來源的基底：切回 UI 執行緒、以及沒有呼叫方可接收失敗時的例外出口。</summary>
public abstract class ViewModelBase : ObservableValidator
{
    /// <summary>非同步回呼逃出來的例外由此承接。根組合層在啟動時指定。</summary>
    public static Action<Exception>? UnhandledAsyncError { get; set; }

    protected static void PostToUi(Action action) => Application.Current.Dispatcher.Invoke(action);

    protected static void RunSafe(Func<Task> action)
        => _ = Guard.RunAsync(action, error => UnhandledAsyncError?.Invoke(error));
}
