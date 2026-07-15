using Vaeron.HelixStudio.Features.EventLog;
using Vaeron.HelixStudio.Features.EventLog.Contracts;
using Vaeron.HelixStudio.Features.UsbVoltageControl.Contracts;
using Vaeron.HelixStudio.Features.UsbVoltageControl.Data;
using Vaeron.HelixStudio.Features.WorkspaceManagement;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio.Features.UsbVoltageControl;

public sealed class UsbVoltageControlDomain(
    UsbVoltageControlRepository repo,
    UsbDeviceVoltageRepository deviceRepo,
    EventLogFacade eventLog,
    WorkspaceManagementFacade workspaceFacade)
{
    private const double MinTargetVolts = 3.0;
    private const double MaxTargetVolts = 24.0;

    #region 事件中繼

    public event Action? Changed;

    #endregion

    #region 統一的查詢方法

    /// <summary>回傳裝置清單＋硬體介面狀態訊息。裝置清單目前必為空（見
    /// Data\UsbDeviceVoltageRepository.cs 的待接說明），message 誠實告知原因，
    /// 不是回傳「找不到裝置」這種容易被誤解成裝置真的不存在的訊息。</summary>
    public async Task<(IReadOnlyList<UsbDeviceSnapshot> devices, string hardwareStatusMessage)> LoadDeviceListAsync()
    {
        var (_, message, connectedDevices) = await deviceRepo.ListConnectedDevicesAsync();
        var targets = await repo.QueryTargetVoltageListAsync();

        var result = new List<UsbDeviceSnapshot>();
        foreach (var device in connectedDevices)
        {
            var (_, _, voltage) = await deviceRepo.ReadVoltageAsync(device.DeviceId);
            var target = targets.FirstOrDefault(t => t.device_id == device.DeviceId)?.target_voltage ?? 5.0;

            result.Add(new UsbDeviceSnapshot
            {
                DeviceId = device.DeviceId,
                Name = device.Name,
                Kind = device.Kind,
                Connected = true,
                SupportsPowerControl = device.SupportsPowerControl,
                LastInterruption = device.LastInterruption,
                Voltage = voltage,
                Target = target,
            });
        }

        return (result, message);
    }

    public Task<bool> LoadGlobalAutoCorrectAsync() => repo.QueryGlobalAutoCorrectAsync();

    #endregion

    #region 基本 CRUD 操作

    public async Task<(bool success, string message)> SetTargetVoltageAsync(ApplyVoltageRequest request)
    {
        if (request.TargetVoltage < MinTargetVolts || request.TargetVoltage > MaxTargetVolts)
            return ResultContract.Fail($"目標電壓須介於 {MinTargetVolts}–{MaxTargetVolts}V 之間");

        await repo.InsertTargetVoltageAsync(request.DeviceId, request.TargetVoltage);
        Changed?.Invoke();

        var (applied, applyMessage) = await deviceRepo.ApplyVoltageAsync(request.DeviceId, request.TargetVoltage);
        return applied ? ResultContract.Ok("已套用修正") : (true, $"目標電壓已儲存，但尚未實際套用：{applyMessage}");
    }

    public async Task<(bool success, string message)> SetGlobalAutoCorrectAsync(bool enabled)
    {
        await repo.InsertGlobalAutoCorrectAsync(enabled);
        Changed?.Invoke();
        return ResultContract.Ok(enabled ? "已啟用自動修正" : "已停用自動修正");
    }

    /// <summary>對齊 blueprint 的 maybeAutoCorrect：全域自動修正開啟時，掃描已連線／支援電源
    /// 管理、且電壓偏離目標的裝置，直接套用既有目標電壓（不是重新決定目標值），逐筆記錄事件。
    /// 裝置清單目前恆為空（硬體介面未串接，見 UsbDeviceVoltageRepository），這裡先把邏輯骨架
    /// 寫完整，之後串接硬體介面時這段不需要再改，只是掃描得到的清單會開始有東西可掃。</summary>
    public async Task<int> ApplyAutoCorrectScanAsync()
    {
        if (!await LoadGlobalAutoCorrectAsync()) return 0;

        var (devices, _) = await LoadDeviceListAsync();
        var offTargetDevices = devices.Where(d => d.Connected && d.SupportsPowerControl && !d.IsWithinTolerance).ToList();

        var correctedCount = 0;
        foreach (var device in offTargetDevices)
        {
            var (applied, _) = await deviceRepo.ApplyVoltageAsync(device.DeviceId, device.Target);
            if (!applied) continue;

            correctedCount++;
            await LogEventAsync(EventSeverity.Info, $"已自動修正裝置「{device.Name}」電壓為 {device.Target:F2}V");
        }

        if (correctedCount > 0) Changed?.Invoke();
        return correctedCount;
    }

    #endregion

    private async Task LogEventAsync(EventSeverity severity, string message)
    {
        var active = await workspaceFacade.LoadActiveWorkspaceAsync();
        if (active is null) return;

        await eventLog.RecordEventAsync(new RecordEventRequest
        {
            WorkspaceId = active.Id,
            ToolName = "USB 防休眠控制",
            Severity = severity,
            Message = message,
        });
    }
}
