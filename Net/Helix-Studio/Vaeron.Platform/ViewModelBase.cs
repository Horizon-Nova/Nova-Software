using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Vaeron.Platform;

/// <summary>viewmodels.md「必須 繼承 ViewModelBase（提供 INotifyPropertyChanged 與 PostToUi）」的共用基底——
/// 抽出各 Feature ViewModel 原本各自重複的 PostToUi 樣板，單一定義處。繼承 ObservableValidator（而非
/// ObservableObject）讓需要表單驗證的 ViewModel（見 validation.md）不需要另外切換基底類別，
/// 不需要驗證的 ViewModel 只是不使用這個能力，不影響它們。</summary>
public abstract class ViewModelBase : ObservableValidator
{
    /// <summary>App 啟動時設定一次（接到 AuditLogging + NotificationRuntime），見 solution-structure.md。</summary>
    public static Action<Exception>? UnhandledAsyncError { get; set; }

    protected static void PostToUi(Action action)
        => Application.Current.Dispatcher.Invoke(action);

    /// <summary>事件回呼安全網（try-catch 攔截點白名單 #3，定義見 shared-kernel.md）：把 async 讀取鏈
    /// 包住，例外交給 UnhandledAsyncError 掛鉤，不讓裸 fire-and-forget（`_ = SomeAsync()`）或
    /// `async void` 事件處理常式在例外發生時無聲吞掉——裸 fire-and-forget 的例外只會停留在
    /// Task 物件上，直到某次 GC 觸發 TaskScheduler.UnobservedTaskException 才會被看到，
    /// 時間點跟真正失敗的當下完全脫勾，等同於使用者看不到任何錯誤反應。</summary>
    protected static void RunSafe(Func<Task> action)
        => _ = RunSafeCore(action);

    private static async Task RunSafeCore(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { UnhandledAsyncError?.Invoke(ex); }
    }
}
