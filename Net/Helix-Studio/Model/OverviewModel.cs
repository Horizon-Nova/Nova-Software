namespace Helix_Studio.Model;

/// <summary>
/// 概覽頁面的資料快照。
/// </summary>
public sealed record OverviewModel
{
    /// <summary>
    /// 機器名稱。
    /// </summary>
    public string MachineName { get; init; } = string.Empty;

    /// <summary>
    /// 副標題文字。
    /// </summary>
    public string SubtitleText { get; init; } = string.Empty;

    /// <summary>
    /// 作業系統短資訊。
    /// </summary>
    public string OsInfoShort { get; init; } = string.Empty;

    /// <summary>
    /// 作業系統完整資訊。
    /// </summary>
    public string OsInfoFull { get; init; } = string.Empty;

    /// <summary>
    /// 開機時間文字。
    /// </summary>
    public string UptimeText { get; init; } = string.Empty;

    /// <summary>
    /// 目前工作區名稱。
    /// </summary>
    public string WorkspaceName { get; init; } = string.Empty;

    /// <summary>
    /// 目前工作區描述。
    /// </summary>
    public string WorkspaceDescription { get; init; } = string.Empty;

    /// <summary>
    /// 模組數量文字。
    /// </summary>
    public string ModuleCountText { get; init; } = string.Empty;

    /// <summary>
    /// 模組狀態文字。
    /// </summary>
    public string ModuleStatusText { get; init; } = string.Empty;

    /// <summary>
    /// 建置版本文字。
    /// </summary>
    public string BuildVersionText { get; init; } = string.Empty;

    /// <summary>
    /// 資料資料夾路徑。
    /// </summary>
    public string DataFolderPath { get; init; } = string.Empty;

    /// <summary>
    /// 模組清單。
    /// </summary>
    public IReadOnlyList<OverviewModuleItemModel> Modules { get; init; } = [];
}

/// <summary>
/// 概覽頁面的模組項目。
/// </summary>
public sealed record OverviewModuleItemModel
{
    /// <summary>
    /// 模組識別值。
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 模組顯示名稱。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 模組版本文字。
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// 模組描述。
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 模組圖示位元組。
    /// </summary>
    public byte[]? Icon { get; init; }

    /// <summary>
    /// 是否有圖示可顯示。
    /// </summary>
    public bool HasIcon { get; init; }
}

