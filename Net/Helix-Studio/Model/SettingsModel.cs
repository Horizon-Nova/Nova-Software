namespace Helix_Studio.Model;

/// <summary>
/// 設定頁面的設定值快照。
/// </summary>
public sealed record SettingsModel
{
    /// <summary>
    /// 資料密度選單索引。
    /// </summary>
    public int DataDensityIndex { get; set; }

    /// <summary>
    /// 字型縮放選單索引。
    /// </summary>
    public int FontScaleIndex { get; set; }

    /// <summary>
    /// 非活躍逾時選單索引。
    /// </summary>
    public int InactivityIndex { get; set; }

    /// <summary>
    /// 語言選單索引。
    /// </summary>
    public int LanguageIndex { get; set; }

    /// <summary>
    /// 上次瀏覽頁面標籤。
    /// </summary>
    public string LastVisitedPageTag { get; set; } = string.Empty;

    /// <summary>
    /// 日誌匯出資料夾。
    /// </summary>
    public string LogExportFolder { get; set; } = string.Empty;

    /// <summary>
    /// 日誌等級選單索引。
    /// </summary>
    public int LogLevelIndex { get; set; }

    /// <summary>
    /// 日誌保留天數選單索引。
    /// </summary>
    public int LogRetentionIndex { get; set; }

    /// <summary>
    /// 起始頁面選單索引。
    /// </summary>
    public int StartPageIndex { get; set; }

    /// <summary>
    /// 佈景主題選單索引。
    /// </summary>
    public int ThemeIndex { get; set; }
}

/// <summary>
/// 設定頁面搜尋建議項目。
/// </summary>
public sealed record SettingsSearchSuggestionModel
{
    /// <summary>
    /// 區段標籤。
    /// </summary>
    public string SectionTag { get; init; } = string.Empty;

    /// <summary>
    /// 顯示文字。
    /// </summary>
    public string Display { get; init; } = string.Empty;
}

/// <summary>
/// 設定頁面工作區卡片資料。
/// </summary>
public sealed record SettingsWorkspaceItemModel
{
    /// <summary>
    /// 工作區名稱。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 工作區描述。
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 是否為目前作用中的工作區。
    /// </summary>
    public bool IsActive { get; init; }
}

/// <summary>
/// 設定頁面模組卡片資料。
/// </summary>
public sealed record SettingsModuleCardModel
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

    /// <summary>
    /// 模組設定內容。
    /// </summary>
    public object? SettingsContent { get; init; }

    /// <summary>
    /// 是否已有設定內容。
    /// </summary>
    public bool HasSettingsContent { get; init; }
}

/// <summary>
/// 設定頁面的關於資訊。
/// </summary>
public sealed record SettingsAboutModel
{
    /// <summary>
    /// 版本文字。
    /// </summary>
    public string VersionText { get; init; } = string.Empty;

    /// <summary>
    /// 建置日期文字。
    /// </summary>
    public string BuildDateText { get; init; } = string.Empty;

    /// <summary>
    /// 授權到期日文字。
    /// </summary>
    public string LicenseExpiryText { get; init; } = string.Empty;
}
