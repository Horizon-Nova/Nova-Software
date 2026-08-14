namespace Vaeron.HelixStudio.SystemAssistant;

/// <summary>畫面目前的查詢條件。全部交給資料庫，過濾排序不在專案端進行。</summary>
public sealed record SystemAssistantQuery(
    string FilterKey,
    string SortKey,
    string SortDirection,
    string Search,
    string ActiveId);

/// <summary>表頭的一欄，來自 sort_column 與 sort_direction。</summary>
/// <remarks>NextDirection 是再點這一欄之後的方向，由資料庫算好；Fills 為真者填滿剩餘寬度、不吃 Width。</remarks>
public sealed record ColumnView(
    string Key,
    string Label,
    string Alignment,
    double Width,
    bool Fills,
    bool IsSorted,
    string Arrow,
    string NextDirection);

/// <summary>清單的一列，來自 v_software_row。字樣與等級都是資料庫給的值。</summary>
public sealed record SoftwareRowView(
    string Id,
    string Name,
    string Publisher,
    string State,
    string StateLabel,
    string Cpu,
    string CpuLevel,
    string Ram,
    string Net,
    string NetLevel,
    string Disk,
    string ToggleLabel,
    string ToggleTitle,
    bool ToggleEnabled,
    int ChangedCount,
    bool HasChanges,
    bool IsActive);

/// <summary>配額工作面的一列，來自 v_software_field。EditState 的值域為 locked／adjustable／modified。</summary>
public sealed record FieldRowView(
    string SoftwareId,
    string FieldKey,
    string Label,
    string Note,
    string Unit,
    string DisplayValue,
    string Placeholder,
    bool ReadOnly,
    string EditState,
    string IconTitle);

/// <summary>工具列與配額工作面標題列的內容。</summary>
public sealed record SummaryView(
    string Text,
    string FilterLabel,
    string NextFilterKey,
    bool FilterOn,
    string ActiveName,
    string ActiveMeta,
    string ActiveToggleLabel,
    string ActiveToggleIcon,
    bool HasActive,
    int ChangedCount,
    bool HasChanges);

/// <summary>按下操作鈕要對 Windows 做的事，來自 v_toggle_action。</summary>
/// <remarks>不可按的組合與沒有安裝路徑的軟體查不到列，該次按下沒有東西可執行。</remarks>
public sealed record ToggleTarget(
    string SoftwareId,
    string Action,
    string Name,
    string Level,
    string Label,
    string ExePath);

/// <summary>執行動作後要記錄的事件。</summary>
public sealed record StateChange(string Level, string Text);

/// <summary>畫面一次載入所需的全部投影。</summary>
public sealed record SystemAssistantSnapshot(
    IReadOnlyList<ColumnView> Columns,
    IReadOnlyList<SoftwareRowView> Rows,
    IReadOnlyList<FieldRowView> Fields,
    SummaryView Summary);
