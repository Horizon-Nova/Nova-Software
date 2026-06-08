using System;

namespace Helix_Studio.AppRuntime;

/// <summary>
/// Shell 內容區導覽請求。
/// </summary>
public sealed class NavigationRequestEventArgs : EventArgs
{
    /// <summary>
    /// 建立導覽請求參數。
    /// </summary>
    public NavigationRequestEventArgs(string pageId, string? moduleId)
    {
        PageId = pageId;
        ModuleId = moduleId;
    }

    /// <summary>
    /// 目標頁面識別（overview、eventlog、modules、settings、module）。
    /// </summary>
    public string PageId { get; }

    /// <summary>
    /// 模組頁面時的模組識別碼。
    /// </summary>
    public string? ModuleId { get; }
}

/// <summary>
/// 主視窗內容區導覽 Runtime。
/// </summary>
public sealed class NavigationRuntime
{
    /// <summary>
    /// 內容區應切換至指定頁面時觸發。
    /// </summary>
    public event EventHandler<NavigationRequestEventArgs>? NavigationRequested;

    /// <summary>
    /// 請求切換內容頁。
    /// </summary>
    public void RequestNavigation(string pageId, string? moduleId = null)
        => NavigationRequested?.Invoke(this, new NavigationRequestEventArgs(pageId, moduleId));
}
