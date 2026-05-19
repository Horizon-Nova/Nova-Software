using System;
using System.Windows;
using System.Windows.Threading;

namespace Helix_Studio.AppHost;

/// <summary>
/// Application Runtime 管理。
/// </summary>
public sealed class ApplicationRuntime
{
    /// <summary>
    /// Runtime 初始化完成事件。
    /// </summary>
    public event EventHandler? Initialized;

    /// <summary>
    /// 初始化 Runtime。
    /// </summary>
    public void Initialize()
        => Initialized?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 在 UI 執行緒執行指定動作。
    /// </summary>
    public void InvokeOnUiThread(Action action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _ = dispatcher.InvokeAsync(action, DispatcherPriority.DataBind);
    }
}
