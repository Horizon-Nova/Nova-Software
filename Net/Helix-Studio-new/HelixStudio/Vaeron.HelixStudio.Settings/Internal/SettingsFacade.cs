using Vaeron.Kernel;

namespace Vaeron.HelixStudio.Settings.Internal;

internal sealed class SettingsFacade(SettingsService service) : ISettingsFacade
{
    public event Action? SettingsChanged
    {
        add => service.Changed += value;
        remove => service.Changed -= value;
    }

    public Task<AppSettingsSnapshot> LoadSettingsAsync() => service.LoadSettingsAsync();

    public Task<Result> UpdateSettingsAsync(AppSettingsSnapshot settings) => service.UpdateSettingsAsync(settings);

    public Task<Result> UpdateSidebarCollapsedDefaultAsync(bool collapsed) => service.UpdateSidebarCollapsedDefaultAsync(collapsed);

    public Task<Result> ApplyPowerModeAsync(string mode) => service.ApplyPowerModeAsync(mode);

    public Task<Result> ApplySleepTimeoutsAsync(SleepTimeoutsRequest request) => service.ApplySleepTimeoutsAsync(request);

    public Task<Result> ApplyScreenTimeoutAsync(int minutes) => service.ApplyScreenTimeoutAsync(minutes);

    public Task<Result> ApplyHibernateAsync(bool enabled) => service.ApplyHibernateAsync(enabled);

    public Task<Result> ApplyFastStartupAsync(bool enabled) => service.ApplyFastStartupAsync(enabled);

    public Task<Result> ApplyPowerButtonActionAsync(string action) => service.ApplyPowerButtonActionAsync(action);

    public Task<Result> ApplyLidCloseActionAsync(string action) => service.ApplyLidCloseActionAsync(action);

    public Task ApplyReconcileStartupRegistrationAsync() => service.ReconcileStartupRegistrationAsync();

    public Task ApplyReconcilePowerSettingsAsync() => service.ReconcilePowerSettingsAsync();

    public Task<Result> ApplyExecuteSleepNowAsync() => service.ExecuteSleepNowAsync();

    public Task<Result> ApplyExecuteHibernateNowAsync() => service.ExecuteHibernateNowAsync();

    public Task<Result> ApplyExecuteShutdownNowAsync() => service.ExecuteShutdownNowAsync();
}
