using Vaeron.Kernel;

namespace Vaeron.HelixStudio.UsbVoltageControl;

/// <summary>無跨 Feature 呼叫方，此介面存在的理由同 AppLibrary：ViewModel 是公開型別，
/// 建構子參數不能小於它的存取範圍（CS0051），需要一層公開入口包住 internal 的 Service／Store。</summary>
public interface IUsbVoltageControlFacade
{
    public event Action? Changed;

    public Task<DeviceListSnapshot> LoadDeviceListAsync();

    public Task<bool> LoadGlobalAutoCorrectAsync();

    public Task<Result> SetTargetVoltageAsync(ApplyVoltageRequest request);

    public Task<Result> SetGlobalAutoCorrectAsync(bool enabled);

    /// <summary>掃描並修正電壓偏離目標的裝置，回傳修正筆數</summary>
    public Task<int> ApplyAutoCorrectScanAsync();
}
