using System;

namespace Helix_Studio.AppRuntime;

/// <summary>
/// 通知 Runtime（跨頁面 Toast 與外部動作請求）。
/// </summary>
public sealed class NotificationRuntime
{
    /// <summary>
    /// 通知請求事件。
    /// </summary>
    public event EventHandler<NotificationRequestedEventArgs>? NotificationRequested;

    /// <summary>
    /// 發出通知請求。
    /// </summary>
    public void Request(NotificationRequest request)
        => NotificationRequested?.Invoke(this, new NotificationRequestedEventArgs(request));
}

/// <summary>
/// 通知請求事件參數。
/// </summary>
public sealed class NotificationRequestedEventArgs : EventArgs
{
    /// <summary>
    /// 建立通知事件參數。
    /// </summary>
    public NotificationRequestedEventArgs(NotificationRequest request) => Request = request;

    /// <summary>
    /// 通知請求內容。
    /// </summary>
    public NotificationRequest Request { get; }
}

/// <summary>
/// 通知動作。
/// </summary>
public enum NotificationAction
{
    /// <summary>僅顯示通知。</summary>
    None,

    /// <summary>以檔案總管定位檔案。</summary>
    RevealInFolder,

    /// <summary>開啟資料夾。</summary>
    OpenFolder,
}

/// <summary>
/// 通知嚴重程度（對應 ToastSeverity）。
/// </summary>
public enum NotificationSeverity
{
    /// <summary>資訊。</summary>
    Information,

    /// <summary>警告。</summary>
    Warning,

    /// <summary>錯誤。</summary>
    Error,

    /// <summary>成功。</summary>
    Success,
}

/// <summary>
/// 通知請求資料。
/// </summary>
public sealed record NotificationRequest
{
    /// <summary>標題（可空）。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>本文。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>嚴重程度。</summary>
    public NotificationSeverity Severity { get; init; } = NotificationSeverity.Information;

    /// <summary>顯示時間（毫秒）。</summary>
    public int DisplayMilliseconds { get; init; } = 4500;

    /// <summary>額外動作。</summary>
    public NotificationAction Action { get; init; } = NotificationAction.None;

    /// <summary>動作目標路徑（檔案或資料夾）。</summary>
    public string ActionPath { get; init; } = string.Empty;
}
