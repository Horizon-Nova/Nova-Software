namespace Helix_Studio.Models;

public sealed class AppSettings
{
    // FL-10
    public DateTime LastReadAt { get; set; } = DateTime.MinValue;

    // FL-13: 日誌設定
    public string LogOutputPath { get; set; } = string.Empty;
    public string LogTimeFormat { get; set; } = "local";
    public bool LogMilliseconds { get; set; } = false;
    public string LogFormat { get; set; } = "jsonl";
    public bool LogCodesEnabled { get; set; } = false;
    public int LogRetentionDays { get; set; } = 30;

    // FL-15: 工具清單排序
    public string ToolSortRule { get; set; } = "custom";

    // FL-16: 應用程式啟動設定
    public string DefaultWorkspaceId { get; set; } = string.Empty;
    public string ToolStartupBehavior { get; set; } = "none";
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public Dictionary<string, List<string>> LastRunningToolIds { get; set; } = new();

    // FL-19: 資源使用警報
    public bool CpuAlertEnabled { get; set; } = false;
    public int CpuAlertThreshold { get; set; } = 85;
    public bool MemoryAlertEnabled { get; set; } = false;
    public int MemoryAlertThreshold { get; set; } = 90;
    public bool ToolLevelAlertsEnabled { get; set; } = false;

    // FL-20: 工作區切換行為
    public string WorkspaceSwitchBehavior { get; set; } = "StopAll";

    // FL-21: 側欄展開狀態
    public bool SidebarExpanded { get; set; } = true;

    // FL-22: 介面主題
    public string Theme { get; set; } = "dark";

    // FL-23: 字體縮放
    public double FontScale { get; set; } = 1.0;

    // FL-24: 介面語言
    public string Language { get; set; } = "zh-TW";

    // FL-25: 日誌檔案編碼
    public string LogEncoding { get; set; } = "utf8";
}
