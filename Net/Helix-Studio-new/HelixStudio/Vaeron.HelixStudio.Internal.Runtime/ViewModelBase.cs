using CommunityToolkit.Mvvm.ComponentModel;
using Vaeron.Kernel.Guarding;

namespace Vaeron.HelixStudio.Internal.Runtime;

/// <summary>ViewModel 共用基底：PostToUi／RunSafe</summary>
public abstract class ViewModelBase : ObservableValidator
{
    /// <summary>全域未處理例外掛鉤</summary>
    public static Action<Exception>? UnhandledAsyncError { get; set; }

    protected static void PostToUi(Action action)
        => System.Windows.Application.Current.Dispatcher.Invoke(action);

    /// <summary>事件回呼安全網：包住 async 讀取鏈，例外交給 UnhandledAsyncError</summary>
    protected static void RunSafe(Func<Task> action)
        => _ = Guard.RunAsync(action, ex => UnhandledAsyncError?.Invoke(ex));
}
