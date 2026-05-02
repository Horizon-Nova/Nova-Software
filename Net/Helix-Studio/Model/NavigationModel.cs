namespace Helix_Studio.Model.Navigation;

/// <summary>側邊欄或導覽列中單一模組項目之顯示資料。</summary>
public sealed class ModuleNavItem
{
    /// <summary>導覽標籤（例如 Module:xxx）。</summary>
    public string Tag { get; init; } = string.Empty;

    /// <summary>顯示名稱。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>圖示承載物件（例如影像來源）。</summary>
    public object? Icon { get; init; }
}

/// <summary>主視窗狀態列或標題區使用之裝置與工作區摘要。</summary>
public sealed record MainStatusInfo(string DeviceName, string VersionText, string WorkspaceName);

/// <summary>全域搜尋建議之一筆。</summary>
public sealed record MainSearchSuggestion(string Tag, string Display);
