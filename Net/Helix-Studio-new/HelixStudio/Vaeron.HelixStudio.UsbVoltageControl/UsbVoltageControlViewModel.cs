using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vaeron.HelixStudio.AppsManagement;
using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.HelixStudio.Internal.Runtime.AppRuntime;
using Vaeron.HelixStudio.Internal.Runtime.Components;
using Vaeron.HelixStudio.WorkspaceManagement;

namespace Vaeron.HelixStudio.UsbVoltageControl;

public sealed partial class UsbVoltageControlViewModel(
    IUsbVoltageControlFacade usbVoltageControl,
    IWorkspaceManagementFacade workspaceFacade,
    IAppsManagementFacade appsManagement,
    NotificationRuntime notificationRuntime) : ViewModelBase
{
    private const string ServiceAppName = "USB 防休眠控制";
    private readonly DispatcherTimer _autoCorrectTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private IReadOnlyList<UsbDeviceSnapshot> _lastDevices = [];

    public ObservableCollection<ExpandableRow<UsbDeviceSnapshot>> Devices { get; } = [];

    [ObservableProperty] private bool _isServiceRunning;
    [ObservableProperty] private bool _globalAutoCorrectEnabled;
    [ObservableProperty] private string _hardwareStatusMessage = "";
    [ObservableProperty] private string? _expandedDeviceId;
    [ObservableProperty] private double _manualTargetVoltage = 5.0;
    [ObservableProperty] private bool _devicesLoading = true;

    public string CorrectedSummary
    {
        get
        {
            var unsupported = Devices.Count(d => !d.Item.SupportsPowerControl);
            return unsupported > 0
                ? $"{Devices.Count} 個裝置，{unsupported} 個不支援電源管理"
                : $"{Devices.Count} 個裝置";
        }
    }

    public bool ShowRiskBanner => IsServiceRunning && !GlobalAutoCorrectEnabled &&
        Devices.Any(d => d.Item.Connected && d.Item.SupportsPowerControl && !d.Item.IsWithinTolerance);

    public string RiskBannerText
    {
        get
        {
            var riskCount = Devices.Count(d => d.Item.Connected && d.Item.SupportsPowerControl && !d.Item.IsWithinTolerance);
            return $"{riskCount} 台已連接裝置的量測電壓偏離目標，尚未套用修正";
        }
    }

    #region 生命週期

    public void ApplyViewLoaded()
    {
        usbVoltageControl.Changed += OnChanged;
        workspaceFacade.WorkspaceChanged += OnChanged;
        appsManagement.ToolsChanged += OnChanged;
        _autoCorrectTimer.Tick += OnAutoCorrectTick;
        _autoCorrectTimer.Start();
        _ = ApplySnapshotAsync();
    }

    public void ApplyViewUnloaded()
    {
        usbVoltageControl.Changed -= OnChanged;
        workspaceFacade.WorkspaceChanged -= OnChanged;
        appsManagement.ToolsChanged -= OnChanged;
        _autoCorrectTimer.Tick -= OnAutoCorrectTick;
        _autoCorrectTimer.Stop();
    }

    private async void OnChanged() => await ApplySnapshotAsync();

    private async void OnAutoCorrectTick(object? sender, EventArgs e)
    {
        if (!IsServiceRunning || !GlobalAutoCorrectEnabled) return;
        await usbVoltageControl.ApplyAutoCorrectScanAsync();
    }

    private async Task ApplySnapshotAsync()
    {
        var workspace = await workspaceFacade.LoadActiveWorkspaceAsync();
        IsServiceRunning = workspace is not null
            && await appsManagement.IsToolRunningByNameAsync(new IsToolRunningQuery(workspace.Id, ServiceAppName));
        GlobalAutoCorrectEnabled = await usbVoltageControl.LoadGlobalAutoCorrectAsync();

        var snapshot = await usbVoltageControl.LoadDeviceListAsync();
        HardwareStatusMessage = snapshot.HardwareStatusMessage;

        _lastDevices = snapshot.Devices;

        PostToUi(() =>
        {
            Devices.Clear();
            foreach (var row in ExpandableRows.Build(_lastDevices, ExpandedDeviceId, d => d.DeviceId))
                Devices.Add(row);
            DevicesLoading = false;
            OnPropertyChanged(nameof(CorrectedSummary));
            OnPropertyChanged(nameof(ShowRiskBanner));
            OnPropertyChanged(nameof(RiskBannerText));
        });
    }

    #endregion

    partial void OnGlobalAutoCorrectEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowRiskBanner));
        OnPropertyChanged(nameof(RiskBannerText));
    }

    partial void OnIsServiceRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowRiskBanner));
        OnPropertyChanged(nameof(RiskBannerText));
    }

    #region 使用者操作入口

    [RelayCommand]
    private async Task ApplyToggleGlobalAutoCorrectAsync()
    {
        var result = await usbVoltageControl.SetGlobalAutoCorrectAsync(!GlobalAutoCorrectEnabled);
        if (result.IsSuccess) notificationRuntime.ApplyShowSuccess(result.Message);
        else notificationRuntime.ApplyShowFailure(result.Message);
    }

    [RelayCommand]
    private void ApplyToggleExpand(UsbDeviceSnapshot device)
    {
        ExpandedDeviceId = ExpandableRows.Toggle(ExpandedDeviceId, device.DeviceId);
        Devices.Clear();
        foreach (var row in ExpandableRows.Build(_lastDevices, ExpandedDeviceId, d => d.DeviceId))
            Devices.Add(row);
    }

    /// <summary>套用裝置既有目標電壓</summary>
    [RelayCommand]
    private async Task ApplyQuickApplyAsync(UsbDeviceSnapshot device)
    {
        var result = await usbVoltageControl.SetTargetVoltageAsync(new ApplyVoltageRequest
        {
            DeviceId = device.DeviceId,
            TargetVoltage = device.Target,
        });
        if (result.IsSuccess) notificationRuntime.ApplyShowSuccess(result.Message);
        else notificationRuntime.ApplyShowFailure(result.Message);
    }

    [RelayCommand]
    private async Task ApplySetTargetVoltageAsync(UsbDeviceSnapshot device)
    {
        var result = await usbVoltageControl.SetTargetVoltageAsync(new ApplyVoltageRequest
        {
            DeviceId = device.DeviceId,
            TargetVoltage = ManualTargetVoltage,
        });
        if (result.IsSuccess) notificationRuntime.ApplyShowSuccess(result.Message);
        else notificationRuntime.ApplyShowFailure(result.Message);
    }

    [RelayCommand]
    private void ApplySetPreset(string presetText)
    {
        if (double.TryParse(presetText, out var preset))
            ManualTargetVoltage = preset;
    }

    #endregion
}
