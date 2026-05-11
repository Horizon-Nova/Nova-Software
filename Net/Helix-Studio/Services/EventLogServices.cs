using System;
using System.Collections.Generic;
using Helix_Studio.Domain;
using Helix_Studio.Model;

namespace Helix_Studio.Services;

/// <summary>
/// 事件日誌頁面的零邏輯轉發服務。
/// </summary>
public sealed class EventLogServices(EventLogDomain domain)
{
    /// <summary>
    /// 事件日誌變更事件。
    /// </summary>
    public event EventHandler? Changed
    {
        add => domain.Changed += value;
        remove => domain.Changed -= value;
    }

    /// <summary>
    /// 記錄未處理例外。
    /// </summary>
    /// <param name="source">來源。</param>
    /// <param name="exception">例外。</param>
    public void RecordUnhandledException(string source, Exception exception) => domain.RecordUnhandledException(source, exception);

    /// <summary>
    /// 查詢事件日誌項目。
    /// </summary>
    public IReadOnlyList<EventLogEntryModel> QueryEntries() => domain.QueryEntries();

    /// <summary>
    /// 匯出日誌壓縮檔。
    /// </summary>
    public string ExportZip() => domain.ExportZip();
}

