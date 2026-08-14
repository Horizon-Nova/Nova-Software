namespace Vaeron.HelixStudio.Shell;

/// <summary>對外資料形狀：殼層初始載入後的完整快照。不可變。</summary>
public sealed record ShellSnapshot(
    IReadOnlyList<NavItem> MainNav,
    IReadOnlyList<NavItem> ToolsNav,
    IReadOnlyList<NavItem> SettingsNav,
    WorkspaceInfo ActiveWorkspace,
    IReadOnlyList<ChatMessage> ChatMessages);
