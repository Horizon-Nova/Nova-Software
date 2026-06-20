using Helix_Studio.Mvvm;

namespace Helix_Studio.Models;

/// <summary>工作區（生產線）資訊。名稱可重新命名，故可通知。</summary>
public sealed class WorkspaceInfo : ObservableObject
{
    private string _name = string.Empty;
    private bool _isActive;

    public string Id { get; set; } = string.Empty;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>是否為目前使用中的工作區，由 Shell 維護。</summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}
