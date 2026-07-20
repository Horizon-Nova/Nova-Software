namespace Vaeron.HelixStudio.Features.UsbVoltageControl.Data;

public sealed record ConnectedUsbDevice(string DeviceId, string Name, string Kind, bool SupportsPowerControl, string LastInterruption);

/// <summary>USB 裝置硬體通訊介面（尚未串接真實硬體）</summary>
public sealed class UsbDeviceVoltageRepository
{
    private const string NotWiredMessage = "尚未串接硬體驅動介面（USB 電壓量測/修正的實際廠牌與 SDK 尚未決定）";

    public Task<(bool success, string message, IReadOnlyList<ConnectedUsbDevice> devices)> ListConnectedDevicesAsync()
        => Task.FromResult<(bool, string, IReadOnlyList<ConnectedUsbDevice>)>((false, NotWiredMessage, []));

    public Task<(bool success, string message, double? voltage)> ReadVoltageAsync(string deviceId)
        => Task.FromResult<(bool, string, double?)>((false, NotWiredMessage, null));

    public Task<(bool success, string message)> ApplyVoltageAsync(string deviceId, double targetVoltage)
        => Task.FromResult((false, NotWiredMessage));
}
