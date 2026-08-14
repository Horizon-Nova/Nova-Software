namespace Vaeron.HelixStudio.SystemAssistant.Internal;

/// <summary>業務規則集中處。規則本身在資料庫，這裡只把四份投影併成一次載入的快照。</summary>
internal sealed class SystemAssistantLogic(SystemAssistantStore store, ProcessControl control)
{
    public SystemAssistantSnapshot BuildSnapshot(SystemAssistantQuery query, string sampledAt)
        => new(
            store.Columns(query),
            store.Rows(query),
            store.Fields(query),
            store.Summary(query, sampledAt));

    public void SaveDraft(string softwareId, string fieldKey, string value)
        => store.SaveDraft(softwareId, fieldKey, value);

    public void Revert(string softwareId) => store.ClearDrafts(softwareId);

    public void Apply(string softwareId)
    {
        store.ApplyDrafts(softwareId);
        store.ClearDrafts(softwareId);
    }

    /// <summary>對 Windows 執行該列的動作。回傳 0 或 1 筆事件，沒有動作可執行時為空。</summary>
    public IReadOnlyList<StateChange> Toggle(string softwareId)
    {
        var targets = store.ToggleTargets(softwareId);

        foreach (var target in targets)
        {
            control.Run(target);
        }

        return targets
            .Select(target => new StateChange(target.Level, $"{target.Name}：手動{target.Label}"))
            .ToList();
    }
}
