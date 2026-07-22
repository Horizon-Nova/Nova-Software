using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Vaeron.Platform;

/// <summary>ViewModel 共用基底：PostToUi／RunSafe</summary>
public abstract class ViewModelBase : ObservableValidator
{
    /// <summary>全域未處理例外掛鉤</summary>
    public static Action<Exception>? UnhandledAsyncError { get; set; }

    protected static void PostToUi(Action action)
        => Application.Current.Dispatcher.Invoke(action);

    /// <summary>事件回呼安全網：包住 async 讀取鏈，例外交給 UnhandledAsyncError</summary>
    protected static void RunSafe(Func<Task> action)
        => _ = RunSafeCore(action);

    private static async Task RunSafeCore(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { UnhandledAsyncError?.Invoke(ex); }
    }
}
