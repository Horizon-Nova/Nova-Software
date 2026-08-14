namespace Vaeron.HelixStudio.SystemAssistant.Internal;

/// <summary>只委派，不含邏輯。</summary>
internal sealed class SystemAssistantFacade(SystemAssistantLogic logic) : ISystemAssistantFacade
{
    public SystemAssistantSnapshot Load(SystemAssistantQuery query, string sampledAt)
        => logic.BuildSnapshot(query, sampledAt);

    public void SaveDraft(string softwareId, string fieldKey, string value)
        => logic.SaveDraft(softwareId, fieldKey, value);

    public void Revert(string softwareId) => logic.Revert(softwareId);

    public void Apply(string softwareId) => logic.Apply(softwareId);

    public IReadOnlyList<StateChange> Toggle(string softwareId) => logic.Toggle(softwareId);
}
