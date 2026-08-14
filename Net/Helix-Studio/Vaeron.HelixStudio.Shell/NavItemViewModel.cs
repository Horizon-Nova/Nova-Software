using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vaeron.HelixStudio.Shell;

/// <summary>繫結用投影：一個導覽項目連同它目前是否被選中。每次選中項目改變時整批重新投影，不在既有實例上就地修改。</summary>
public sealed record NavItemViewModel(string Id, string Icon, string Label, bool HasIcon, bool IsActive);

/// <summary>一個可收合的版面區塊，同時是「自訂版面配置」下拉選單的一列。</summary>
public sealed partial class PanelToggle(string label, string hint, bool open) : ObservableObject
{
    public string Label { get; } = label;

    public string Hint { get; } = hint;

    [ObservableProperty]
    private bool isOpen = open;

    [RelayCommand]
    private void Toggle() => IsOpen = !IsOpen;
}
