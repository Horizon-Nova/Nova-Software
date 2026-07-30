using Vaeron.Kernel;

namespace Vaeron.HelixStudio.UsbVoltageControl.Internal;

internal sealed record ConnectedUsbDevice(string DeviceId, string Name, string Kind, bool SupportsPowerControl, string LastInterruption);

internal sealed record ConnectedDevicesResult(IReadOnlyList<ConnectedUsbDevice> Devices);

/// <summary>USB 裝置硬體通訊介面（尚未串接真實硬體）</summary>
internal sealed class UsbDeviceVoltageStore
{
    private const string NotWiredMessage = "尚未串接硬體驅動介面（USB 電壓量測/修正的實際廠牌與 SDK 尚未決定）";

    public Task<Result<ConnectedDevicesResult>> ListConnectedDevicesAsync()
        => Task.FromResult(Result<ConnectedDevicesResult>.Fail(ResultKind.Unavailable, NotWiredMessage));

    public Task<Result<double?>> ReadVoltageAsync(string deviceId)
        => Task.FromResult(Result<double?>.Fail(ResultKind.Unavailable, NotWiredMessage));

    public Task<Result> ApplyVoltageAsync(ApplyVoltageRequest request)
        => Task.FromResult(Result.Fail(ResultKind.Unavailable, NotWiredMessage));
}
