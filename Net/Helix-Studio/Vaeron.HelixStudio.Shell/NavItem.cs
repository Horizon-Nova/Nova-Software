namespace Vaeron.HelixStudio.Shell;

/// <summary>對外資料形狀：一個導覽項目。不可變。</summary>
/// <remarks>AppName 對照已安裝工具的名稱，決定該項目是否出現在圖示軌的工具區塊；非工具類項目留空字串，永遠對不到任何工具。</remarks>
public sealed record NavItem(string Id, string Icon, string Label, bool HasIcon, string AppName = "");

/// <summary>對外資料形狀：一個工作區（產線）。不可變。</summary>
public sealed record WorkspaceInfo(
    string Id,
    string WorkspaceName,
    string Description,
    string CpuPercent,
    string RamUsedMB,
    string RamTotalGB,
    int RunningCount,
    int EventCount,
    int EventBadge,
    string Version);

/// <summary>對外資料形狀：一則 AI 助理對話訊息。Who 的值域比照設計稿 chatSeed 的 who 欄位。</summary>
public sealed record ChatMessage(string Who, string Text);
