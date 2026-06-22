namespace Helix_Studio.Models;

public sealed class AppSettings
{
    // FL-05 N-008: 事件日誌已讀時間戳（用於計算未讀 Warning/Error 徽章數）
    public DateTime LastReadAt { get; set; } = DateTime.MinValue;

    // FL-08 N-005: 日誌設定
    public string LogOutputPath { get; set; } = string.Empty;
    public string LogTimeFormat { get; set; } = "local";
    public bool LogMilliseconds { get; set; } = false;
    public string LogFormat { get; set; } = "jsonl";
    public bool LogCodesEnabled { get; set; } = false;
    public int LogRetentionDays { get; set; } = 30;
    public string LogEncoding { get; set; } = "utf8";

    // FL-03 N-007: 工具清單排序規則（custom / name-asc / name-desc / installed-at）
    public string ToolSortRule { get; set; } = "custom";

    // FL-08 N-002: 應用程式啟動設定
    public string DefaultWorkspaceId { get; set; } = string.Empty;
    public string ToolStartupBehavior { get; set; } = "none";
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    // FL-08 N-002 / FL-09 N-001: 關閉視窗時最小化至系統匣（false = 直接退出）
    public bool CloseToTray { get; set; } = true;
    // 最後執行中的工具 ID，以工作區 ID 為鍵（FL-08 N-002 還原上次狀態用）
    public Dictionary<string, List<string>> LastRunningToolIds { get; set; } = new();

    // FL-06 N-004 / FL-08 N-006: 資源使用警報
    public bool CpuAlertEnabled { get; set; } = false;
    public int CpuAlertThreshold { get; set; } = 85;
    public bool MemoryAlertEnabled { get; set; } = false;
    public int MemoryAlertThreshold { get; set; } = 90;
    public bool ToolLevelAlertsEnabled { get; set; } = false;

    // FL-08 N-004 / FL-02 N-003: 工作區切換行為（StopAll / StopNone / Ask）
    public string WorkspaceSwitchBehavior { get; set; } = "StopAll";

    // FL-10: 側欄展開狀態（持久化）
    public bool SidebarExpanded { get; set; } = true;

    // FL-08 N-003: 介面主題（dark / light / system）
    public string Theme { get; set; } = "dark";

    // FL-08 N-003: 字體縮放（1.0 = 100%）
    public double FontScale { get; set; } = 1.0;

    // FL-08 N-001: 介面語言（zh-TW / en-US / zh-CN）
    public string Language { get; set; } = "zh-TW";
}
