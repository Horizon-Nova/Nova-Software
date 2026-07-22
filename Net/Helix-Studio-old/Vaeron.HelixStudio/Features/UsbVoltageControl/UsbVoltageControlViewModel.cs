using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vaeron.HelixStudio.Features.AppsManagement;
using Vaeron.HelixStudio.Features.UsbVoltageControl.Contracts;
using Vaeron.HelixStudio.Features.WorkspaceManagement;
using Vaeron.Platform;
using Vaeron.Platform.AppRuntime;

namespace Vaeron.HelixStudio.Features.UsbVoltageControl;

public sealed partial class UsbVoltageControlViewModel(
    UsbVoltageControlDomain domain,
    WorkspaceManagementFacade workspaceFacade,
    AppsManagementFacade appsManagement,
    NotificationRuntime notificationRuntime) : ViewModelBase
{
    private const string ServiceAppName = "USB 防休眠控制";
    private readonly DispatcherTimer _autoCorrectTimer = new() { Interval = TimeSpan.FromSeconds(5) };

    public ObservableCollection<UsbDeviceSnapshot> Devices { get; } = [];

    [ObservableProperty] private bool _isServiceRunning;
    [ObservableProperty] private bool _globalAutoCorrectEnabled;
    [ObservableProperty] private string _hardwareStatusMessage = "";
    [ObservableProperty] private UsbDeviceSnapshot? _expandedDevice;
    [ObservableProperty] private double _manualTargetVoltage = 5.0;
    [ObservableProperty] private bool _devicesLoading = true;

    public string CorrectedSummary
    {
        get
        {
            var unsupported = Devices.Count(d => !d.SupportsPowerControl);
            return unsupported > 0
                ? $"{Devices.Count} 個裝置，{unsupported} 個不支援電源管理"
                : $"{Devices.Count} 個裝置";
        }
    }

    public bool ShowRiskBanner => IsServiceRunning && !GlobalAutoCorrectEnabled &&
        Devices.Any(d => d.Connected && d.SupportsPowerControl && !d.IsWithinTolerance);

    public string RiskBannerText
    {
        get
        {
            var riskCount = Devices.Count(d => d.Connected && d.SupportsPowerControl && !d.IsWithinTolerance);
            return $"{riskCount} 台已連接裝置的量測電壓偏離目標，尚未套用修正";
        }
    }

    #region 生命週期

    public void ApplyViewLoaded()
    {
        domain.Changed += OnChanged;
        workspaceFacade.WorkspaceChanged += OnChanged;
        appsManagement.ToolsChanged += OnChanged;
        _autoCorrectTimer.Tick += OnAutoCorrectTick;
        _autoCorrectTimer.Start();
        _ = ApplySnapshotAsync();
    }

    public void ApplyViewUnloaded()
    {
        domain.Changed -= OnChanged;
        workspaceFacade.WorkspaceChanged -= OnChanged;
        appsManagement.ToolsChanged -= OnChanged;
        _autoCorrectTimer.Tick -= OnAutoCorrectTick;
        _autoCorrectTimer.Stop();
    }

    private async void OnChanged() => await ApplySnapshotAsync();

    private async void OnAutoCorrectTick(object? sender, EventArgs e)
    {
        if (!IsServiceRunning || !GlobalAutoCorrectEnabled) return;
        await domain.ApplyAutoCorrectScanAsync();
    }

    private async Task ApplySnapshotAsync()
    {
        var workspace = await workspaceFacade.LoadActiveWorkspaceAsync();
        IsServiceRunning = workspace is not null && await appsManagement.IsToolRunningByNameAsync(workspace.Id, ServiceAppName);
        GlobalAutoCorrectEnabled = await domain.LoadGlobalAutoCorrectAsync();

        var (devices, message) = await domain.LoadDeviceListAsync();
        HardwareStatusMessage = message;

        PostToUi(() =>
        {
            Devices.Clear();
            foreach (var d in devices)
                Devices.Add(d);
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
        var (success, message) = await domain.SetGlobalAutoCorrectAsync(!GlobalAutoCorrectEnabled);
        notificationRuntime.ApplyShowResult(success, message);
    }

    [RelayCommand]
    private void ApplyToggleExpand(UsbDeviceSnapshot device)
        => ExpandedDevice = ReferenceEquals(ExpandedDevice, device) ? null : device;

    /// <summary>套用裝置既有目標電壓</summary>
    [RelayCommand]
    private async Task ApplyQuickApplyAsync(UsbDeviceSnapshot device)
    {
        var (success, message) = await domain.SetTargetVoltageAsync(new ApplyVoltageRequest
        {
            DeviceId = device.DeviceId,
            TargetVoltage = device.Target,
        });
        notificationRuntime.ApplyShowResult(success, message);
    }

    [RelayCommand]
    private async Task ApplySetTargetVoltageAsync(UsbDeviceSnapshot device)
    {
        var (success, message) = await domain.SetTargetVoltageAsync(new ApplyVoltageRequest
        {
            DeviceId = device.DeviceId,
            TargetVoltage = ManualTargetVoltage,
        });
        notificationRuntime.ApplyShowResult(success, message);
    }

    [RelayCommand]
    private void ApplySetPreset(string presetText)
    {
        if (double.TryParse(presetText, out var preset))
            ManualTargetVoltage = preset;
    }

    #endregion
}
