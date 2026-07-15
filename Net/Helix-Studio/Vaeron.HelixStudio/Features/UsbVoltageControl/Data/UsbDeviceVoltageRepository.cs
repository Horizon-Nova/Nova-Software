namespace Vaeron.HelixStudio.Features.UsbVoltageControl.Data;

public sealed record ConnectedUsbDevice(string DeviceId, string Name, string Kind, bool SupportsPowerControl, string LastInterruption);

/// <summary>USB 裝置實際通訊的介面點——依與使用者的確認（見 UsbVoltageControl.md 藍圖），
/// 硬體/驅動介面尚未指定廠牌/SDK，本類別的方法簽名已定稿，本體誠實回傳「尚未串接」，
/// 不用假資料冒充已連接裝置清單。之後串接真實硬體時，只需要改寫這個類別內部，
/// Domain／ViewModel／View 不需要跟著改。</summary>
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
