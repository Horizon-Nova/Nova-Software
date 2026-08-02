using Vaeron.Kernel;

namespace Vaeron.HelixStudio.Settings;

/// <summary>供 Shell 在啟動時讀取初始主題/側邊欄狀態、反應設定變更的唯一入口；
/// 本 Feature 的 ViewModel 也經此存取（同 EventLog 的判準：ViewModel 是公開型別，
/// 建構子參數不能小於它的存取範圍，CS0051）。</summary>
public interface ISettingsFacade
{
    public event Action? SettingsChanged;

    public Task<AppSettingsSnapshot> LoadSettingsAsync();

    public Task<Result> UpdateSettingsAsync(AppSettingsSnapshot settings);

    public Task<Result> UpdateSidebarCollapsedDefaultAsync(bool collapsed);

    public Task<Result> ApplyPowerModeAsync(string mode);

    public Task<Result> ApplySleepTimeoutsAsync(SleepTimeoutsRequest request);

    public Task<Result> ApplyScreenTimeoutAsync(int minutes);

    public Task<Result> ApplyHibernateAsync(bool enabled);

    public Task<Result> ApplyFastStartupAsync(bool enabled);

    public Task<Result> ApplyPowerButtonActionAsync(string action);

    public Task<Result> ApplyLidCloseActionAsync(string action);

    public Task ApplyReconcileStartupRegistrationAsync();

    public Task<Result> ApplyExecuteSleepNowAsync();

    public Task<Result> ApplyExecuteHibernateNowAsync();

    public Task<Result> ApplyExecuteShutdownNowAsync();
}
