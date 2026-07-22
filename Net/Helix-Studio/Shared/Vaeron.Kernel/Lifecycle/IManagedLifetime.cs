namespace Vaeron.Kernel.Lifecycle;

/// <summary>
/// 常駐、有狀態、可能失敗的資源。
/// 補的是平台空缺：背景服務失敗時框架不監控不重啟,預設行為會停掉整個進程;
/// 釋放介面只管釋放,不管是否重試。中間的處置策略由本契約承載。
/// 刻意最小：只有狀態與恢復。
/// </summary>
public interface IManagedLifetime
{
    HealthState Health { get; }

    ValueTask<Result> TryRecoverAsync(CancellationToken cancellationToken);
}
