using Vaeron.HelixStudio.Features.Settings.Contracts;

namespace Vaeron.HelixStudio.Features.Settings;

/// <summary>供 MainShell 在啟動時讀取初始主題/側邊欄狀態、反應設定變更的唯一入口。</summary>
public sealed class SettingsFacade(SettingsDomain domain)
{
    public event Action? SettingsChanged
    {
        add => domain.Changed += value;
        remove => domain.Changed -= value;
    }

    public Task<AppSettingsSnapshot> LoadSettingsAsync() => domain.LoadSettingsAsync();

    public Task<(bool success, string message)> UpdateSidebarCollapsedDefaultAsync(bool collapsed)
        => domain.UpdateSidebarCollapsedDefaultAsync(collapsed);
}
