namespace Helix_Studio.Model.Settings;

/// <summary>使用者可調整之應用程式設定（持久化模型）。</summary>
public sealed class AppSettings
{
    /// <summary>語言選項索引。</summary>
    public int LanguageIndex { get; set; } = 0;

    /// <summary>啟動頁面選項索引。</summary>
    public int StartPageIndex { get; set; } = 0;

    /// <summary>上次造訪之頁面標籤。</summary>
    public string LastVisitedPageTag { get; set; } = "Home";

    /// <summary>閒置鎖定選項索引。</summary>
    public int InactivityIndex { get; set; } = 2;

    /// <summary>主題選項索引。</summary>
    public int ThemeIndex { get; set; } = 0;

    /// <summary>字型縮放選項索引。</summary>
    public int FontScaleIndex { get; set; } = 1;

    /// <summary>資料密度選項索引。</summary>
    public int DataDensityIndex { get; set; } = 0;

    /// <summary>日誌層級選項索引。</summary>
    public int LogLevelIndex { get; set; } = 0;

    /// <summary>日誌保留天數選項索引。</summary>
    public int LogRetentionIndex { get; set; } = 1;

    /// <summary>日誌匯出預設資料夾路徑。</summary>
    public string LogExportFolder { get; set; } = string.Empty;
}

/// <summary>關於對話框顯示之版本與授權到期文字。</summary>
public sealed record AboutInfo(string VersionText, string BuildDateText, string LicenseExpiryText);

/// <summary>日誌匯出操作結果。</summary>
public sealed record LogExportResult(bool IsSuccess, string? ArchivePath, string? ErrorMessage)
{
    /// <summary>建立成功結果。</summary>
    public static LogExportResult Success(string archivePath) => new(true, archivePath, null);

    /// <summary>建立失敗結果。</summary>
    public static LogExportResult Failure(string errorMessage) => new(false, null, errorMessage);
}

/// <summary>法律文件開啟嘗試之結果。</summary>
public sealed record LegalNoticeResult(bool IsFound, string? Message)
{
    /// <summary>已成功開啟文件。</summary>
    public static LegalNoticeResult Opened() => new(true, null);

    /// <summary>找不到文件時之結果。</summary>
    public static LegalNoticeResult NotFound(string message) => new(false, message);
}

/// <summary>設定頁搜尋建議之一筆。</summary>
public sealed record SettingsSearchSuggestion(string Tag, string Display);
