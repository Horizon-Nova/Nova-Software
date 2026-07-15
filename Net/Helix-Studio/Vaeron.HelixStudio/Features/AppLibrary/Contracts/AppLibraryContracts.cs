namespace Vaeron.HelixStudio.Features.AppLibrary.Contracts;

public sealed record LibraryCategorySnapshot
{
    public required string Id { get; init; }
    public required string Label { get; init; }
}

public sealed record LibraryAppSnapshot
{
    public required string CatalogId { get; init; }
    public required string Name { get; init; }
    public required string CategoryId { get; init; }
    public required string Description { get; init; }
    public required string Version { get; init; }
    public required string Developer { get; init; }
    public required string FileSize { get; init; }
    public required string Requirements { get; init; }
    public required DateTimeOffset LastUpdated { get; init; }
    public string? Readme { get; init; }
    public required bool IsBuiltIn { get; init; }
    public required bool IsInstalledInActiveWorkspace { get; init; }
    public required string? InstalledVersion { get; init; }
    public bool IsUpdateAvailable => InstalledVersion is not null && InstalledVersion != Version;
    /// <summary>「已加入」按鈕只在「已安裝且沒有更新可用」時顯示——有更新可用時只顯示
    /// 「更新」按鈕，兩者互斥，不是各自獨立判斷 IsInstalledInActiveWorkspace 就好
    /// （blueprint 的 isInstalled 本來就已經把 hasUpdate 排除在外）。</summary>
    public bool IsInstalledWithoutUpdate => IsInstalledInActiveWorkspace && !IsUpdateAvailable;
    public string UpdateBadgeText => $"有更新可用（已裝 {InstalledVersion}）";
}

public sealed record ImportToolRequest
{
    public required string FilePath { get; init; }
    /// <summary>目前作用中的工作區——重新匯入同名工具、且該工具已裝在這個工作區時，
    /// 會直接同步套用新版本，不需要使用者再多點一次「更新」。null 代表沒有作用中工作區
    /// （理論上不會發生，畫面一定有工作區才會顯示），這時就不做自動同步。</summary>
    public int? WorkspaceId { get; init; }
}
