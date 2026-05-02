namespace Helix_Studio.Model.Overview;

/// <summary>首頁／概覽區塊顯示之系統與路徑摘要。</summary>
public sealed record OverviewInfo(
    string DeviceName,
    string OsDescription,
    string BuildVersionText,
    string UptimeText,
    string DataPath,
    string SubtitleText);

/// <summary>概覽頁模組卡片之顯示資料。</summary>
public sealed record OverviewModuleCard(
    string Id,
    string DisplayName,
    string Version,
    string Description,
    object? Icon)
{
    /// <summary>是否具備圖示資料。</summary>
    public bool HasIcon => Icon is not null;
}
