using CommunityToolkit.Mvvm.ComponentModel;

namespace Vaeron.VNCSCP.Dashboard;

/// <summary>群組管理面板的一列。群組不是獨立實體，名稱本身就是識別鍵。</summary>
public sealed partial class GroupRow : ObservableObject
{
    public required string Name { get; init; }

    [ObservableProperty]
    private string _editName = "";

    [ObservableProperty]
    private bool _isEditing;
}

