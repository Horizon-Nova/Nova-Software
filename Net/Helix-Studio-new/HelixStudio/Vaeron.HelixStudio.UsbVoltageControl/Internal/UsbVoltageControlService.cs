using Vaeron.HelixStudio.EventLog;
using Vaeron.HelixStudio.WorkspaceManagement;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.UsbVoltageControl.Internal;

internal sealed class UsbVoltageControlService(
    UsbVoltageControlStore store,
    UsbDeviceVoltageStore deviceStore,
    IEventLogFacade eventLog,
    IWorkspaceManagementFacade workspaceFacade)
{
    private const double MinTargetVolts = 3.0;
    private const double MaxTargetVolts = 24.0;

    public event Action? Changed;

    /// <summary>裝置清單＋硬體介面狀態訊息</summary>
    public async Task<DeviceListSnapshot> LoadDeviceListAsync()
    {
        var listResult = await deviceStore.ListConnectedDevicesAsync();
        var connectedDevices = listResult.Value?.Devices ?? [];
        var targets = await store.QueryTargetVoltageListAsync();

        var result = new List<UsbDeviceSnapshot>();
        foreach (var device in connectedDevices)
        {
            var voltageResult = await deviceStore.ReadVoltageAsync(device.DeviceId);
            var target = targets.FirstOrDefault(t => t.device_id == device.DeviceId)?.target_voltage ?? 5.0;

            result.Add(new UsbDeviceSnapshot
            {
                DeviceId = device.DeviceId,
                Name = device.Name,
                Kind = device.Kind,
                Connected = true,
                SupportsPowerControl = device.SupportsPowerControl,
                LastInterruption = device.LastInterruption,
                Voltage = voltageResult.Value,
                Target = target,
            });
        }

        return new DeviceListSnapshot(result, listResult.Message);
    }

    public Task<bool> LoadGlobalAutoCorrectAsync() => store.QueryGlobalAutoCorrectAsync();

    public async Task<Result> SetTargetVoltageAsync(ApplyVoltageRequest request)
    {
        if (request.TargetVoltage < MinTargetVolts || request.TargetVoltage > MaxTargetVolts)
            return Result.Fail(ResultKind.ValidationFailed, $"目標電壓須介於 {MinTargetVolts}–{MaxTargetVolts}V 之間");

        await store.InsertTargetVoltageAsync(new SetTargetVoltageCommand(request.DeviceId, request.TargetVoltage));
        Changed?.Invoke();

        var applied = await deviceStore.ApplyVoltageAsync(request);
        return applied.IsSuccess
            ? Ok("已套用修正")
            : new Result { Kind = ResultKind.Success, Message = $"目標電壓已儲存，但尚未實際套用：{applied.Message}" };
    }

    public async Task<Result> SetGlobalAutoCorrectAsync(bool enabled)
    {
        await store.InsertGlobalAutoCorrectAsync(enabled);
        Changed?.Invoke();
        return Ok(enabled ? "已啟用自動修正" : "已停用自動修正");
    }

    /// <summary>掃描並修正電壓偏離目標的裝置</summary>
    public async Task<int> ApplyAutoCorrectScanAsync()
    {
        if (!await LoadGlobalAutoCorrectAsync()) return 0;

        var snapshot = await LoadDeviceListAsync();
        var offTargetDevices = snapshot.Devices.Where(d => d.Connected && d.SupportsPowerControl && !d.IsWithinTolerance).ToList();

        var correctedCount = 0;
        foreach (var device in offTargetDevices)
        {
            var applied = await deviceStore.ApplyVoltageAsync(new ApplyVoltageRequest { DeviceId = device.DeviceId, TargetVoltage = device.Target });
            if (!applied.IsSuccess) continue;

            correctedCount++;
            await LogEventAsync(EventSeverity.Info, $"已自動修正裝置「{device.Name}」電壓為 {device.Target:F2}V");
        }

        if (correctedCount > 0) Changed?.Invoke();
        return correctedCount;
    }

    private static Result Ok(string message) => new() { Kind = ResultKind.Success, Message = message };

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
