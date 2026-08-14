namespace Vaeron.HelixStudio.SystemAssistant;

/// <summary>跨功能單元往來的唯一入口。其他上層組件僅能經此存取本功能。</summary>
public interface ISystemAssistantFacade
{
    /// <summary>依查詢條件取得畫面所需的全部投影。</summary>
    public SystemAssistantSnapshot Load(SystemAssistantQuery query, string sampledAt);

    /// <summary>把一個欄位的編輯結果寫進草稿。</summary>
    public void SaveDraft(string softwareId, string fieldKey, string value);

    /// <summary>丟掉該軟體的全部草稿。</summary>
    public void Revert(string softwareId);

    /// <summary>把該軟體的草稿寫回設定值並清空草稿。</summary>
    public void Apply(string softwareId);

    /// <summary>對該軟體執行操作鈕綁定的動作，回傳 0 或 1 筆要記錄的事件。</summary>
    public IReadOnlyList<StateChange> Toggle(string softwareId);
}
