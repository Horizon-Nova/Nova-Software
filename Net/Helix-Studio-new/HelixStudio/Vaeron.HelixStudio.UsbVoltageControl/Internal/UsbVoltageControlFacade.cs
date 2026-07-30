using Vaeron.Kernel;

namespace Vaeron.HelixStudio.UsbVoltageControl.Internal;

internal sealed class UsbVoltageControlFacade(UsbVoltageControlService service) : IUsbVoltageControlFacade
{
    public event Action? Changed
    {
        add => service.Changed += value;
        remove => service.Changed -= value;
    }

    public Task<DeviceListSnapshot> LoadDeviceListAsync() => service.LoadDeviceListAsync();

    public Task<bool> LoadGlobalAutoCorrectAsync() => service.LoadGlobalAutoCorrectAsync();

    public Task<Result> SetTargetVoltageAsync(ApplyVoltageRequest request) => service.SetTargetVoltageAsync(request);

    public Task<Result> SetGlobalAutoCorrectAsync(bool enabled) => service.SetGlobalAutoCorrectAsync(enabled);

    public Task<int> ApplyAutoCorrectScanAsync() => service.ApplyAutoCorrectScanAsync();
}
