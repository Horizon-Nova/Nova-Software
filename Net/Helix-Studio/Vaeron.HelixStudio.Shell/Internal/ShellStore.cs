namespace Vaeron.HelixStudio.Shell.Internal;

/// <summary>一個已安裝工具目前的執行狀態。狀態值域比照 seed/apps.js 的 state 欄位。</summary>
internal sealed record InstalledApp(string Name, string State);

/// <summary>資料存取邊界。只做讀寫，不含業務判斷。目前為記憶體內靜態種子資料，比照 seed/nav.js、seed/workspaces.js、seed/apps.js。</summary>
internal sealed class ShellStore
{
    public const string RunningState = "running";

    public IReadOnlyList<NavItem> MainNav { get; } =
    [
        new NavItem("assistant", "layout-grid", "系統助理", true),
        new NavItem("library", "grid-3x3", "工具庫", true),
        new NavItem("events", "bell", "事件日誌", true)
    ];

    public IReadOnlyList<NavItem> SettingsNav { get; } =
    [
        new NavItem("settings", "sliders-horizontal", "設定", true)
    ];

    public IReadOnlyList<NavItem> ToolsNav { get; } =
    [
        new NavItem("usb", "", "USB 防休眠控制", false, "USB 防休眠控制"),
        new NavItem("vnc", "", "遠端批次傳檔", false, "遠端批次傳檔")
    ];

    public IReadOnlyList<InstalledApp> InstalledApps { get; } =
    [
        new InstalledApp("USB 防休眠控制", "stopped"),
        new InstalledApp("條碼掃描服務", "running"),
        new InstalledApp("產線監控代理", "running"),
        new InstalledApp("資料同步工具", "stopped"),
        new InstalledApp("螢幕保護程式管理", "error"),
        new InstalledApp("遠端批次傳檔", "stopped")
    ];

    public IReadOnlyList<WorkspaceInfo> Workspaces { get; } =
    [
        new WorkspaceInfo("line-a", "生產線 A", "包裝線，兩班制", "33.6%", "4.33 GB", "8.00 GB", 5, 19, 1, "v1.0.0"),
        new WorkspaceInfo("line-b", "生產線 B", "組裝線", "12.8%", "420 MB", "8.00 GB", 4, 6, 2, "v1.0.0"),
        new WorkspaceInfo("line-c", "生產線 C", "備援產線，假日試產", "0.9%", "64 MB", "8.00 GB", 1, 0, 0, "v1.0.0")
    ];

    public IReadOnlyList<ChatMessage> ChatSeed { get; } =
    [
        new ChatMessage("user", "產線 A 的 USB 防休眠今天有斷過嗎？"),
        new ChatMessage("ai", "今天 09:14 有一次斷線，持續 42 秒後自動恢復，其餘時間都正常。"),
        new ChatMessage("user", "幫我看一下是哪一台。"),
        new ChatMessage("ai", "是 A-07（192.168.1.37）。同一台這週已經第三次，建議檢查線材或改用後方 USB 埠。")
    ];
}
