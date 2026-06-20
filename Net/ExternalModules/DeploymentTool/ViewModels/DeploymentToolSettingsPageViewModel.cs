using ExternalModules.DeploymentTool.Infrastructure.Settings;
using ExternalModules.DeploymentTool.ViewModels.Base;
using HorizonNova.HelixStudio.ModuleSdk;

namespace ExternalModules.DeploymentTool.ViewModels;

/// <summary>
/// DeploymentTool 設定頁的 UI 狀態機，管理設定讀寫與狀態回饋。
/// </summary>
internal sealed class DeploymentToolSettingsPageViewModel : ViewModelBase
{
    private IModuleSettings? _settings;
    private bool _isLoading;
    private string _statusText = string.Empty;
    private string _selectedLinuxTemplate = "Ubuntu 24.04 + Docker Base";
    private string _selectedDefaultCpu = "4";
    private string _selectedDefaultMemory = "8192";
    private string _selectedDockerMode = "docker compose";
    private string _workspaceDirectory = string.Empty;
    private bool _isAutoStartEnabled = true;

    /// <summary>Linux 模板選項清單。</summary>
    public IReadOnlyList<string> LinuxTemplateOptions { get; } = DeploymentSettingsAccessor.QueryLinuxTemplateOptions();

    /// <summary>預設 vCPU 選項清單。</summary>
    public IReadOnlyList<string> DefaultCpuOptions { get; } = DeploymentSettingsAccessor.QueryDefaultCpuOptions();

    /// <summary>預設 RAM 選項清單。</summary>
    public IReadOnlyList<string> DefaultMemoryOptions { get; } = DeploymentSettingsAccessor.QueryDefaultMemoryOptions();

    /// <summary>Docker 模式選項清單。</summary>
    public IReadOnlyList<string> DockerModeOptions { get; } = DeploymentSettingsAccessor.QueryDockerModeOptions();

    /// <summary>設定頁操作狀態文字。</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value ?? string.Empty);
    }

    /// <summary>目前選取的 Linux 模板；值變更時自動儲存。</summary>
    public string SelectedLinuxTemplate
    {
        get => _selectedLinuxTemplate;
        set
        {
            if (!SetProperty(ref _selectedLinuxTemplate, value ?? string.Empty) || _isLoading) return;
            SaveString("linuxTemplate", _selectedLinuxTemplate, "已更新 Linux 模板。");
        }
    }

    /// <summary>目前選取的預設 vCPU；值變更時自動儲存。</summary>
    public string SelectedDefaultCpu
    {
        get => _selectedDefaultCpu;
        set
        {
            if (!SetProperty(ref _selectedDefaultCpu, value ?? string.Empty) || _isLoading) return;
            SaveString("defaultCpuCores", _selectedDefaultCpu, "已更新預設 vCPU。");
        }
    }

    /// <summary>目前選取的預設 RAM；值變更時自動儲存。</summary>
    public string SelectedDefaultMemory
    {
        get => _selectedDefaultMemory;
        set
        {
            if (!SetProperty(ref _selectedDefaultMemory, value ?? string.Empty) || _isLoading) return;
            SaveString("defaultMemoryMb", _selectedDefaultMemory, "已更新預設 RAM。");
        }
    }

    /// <summary>目前選取的 Docker 模式；值變更時自動儲存。</summary>
    public string SelectedDockerMode
    {
        get => _selectedDockerMode;
        set
        {
            if (!SetProperty(ref _selectedDockerMode, value ?? string.Empty) || _isLoading) return;
            SaveString("dockerMode", _selectedDockerMode, "已更新 Docker 模式。");
        }
    }

    /// <summary>專案工作目錄；值變更時自動儲存。</summary>
    public string WorkspaceDirectory
    {
        get => _workspaceDirectory;
        set
        {
            if (!SetProperty(ref _workspaceDirectory, value ?? string.Empty) || _isLoading) return;
            SaveString("workspaceDirectory", _workspaceDirectory, "已更新專案工作目錄。");
        }
    }

    /// <summary>建立後自動啟動開關；值變更時自動儲存。</summary>
    public bool IsAutoStartEnabled
    {
        get => _isAutoStartEnabled;
        set
        {
            if (!SetProperty(ref _isAutoStartEnabled, value) || _isLoading) return;
            SaveBoolean("autoStartAfterCreate", _isAutoStartEnabled, "已更新建立後自動啟動設定。");
        }
    }

    /// <summary>
    /// 套用宿主設定來源，讀取並還原目前值至各屬性。
    /// </summary>
    /// <param name="settings">宿主提供的設定存取介面。</param>
    internal void ApplySettings(IModuleSettings settings)
    {
        _settings = settings;
        _isLoading = true;
        try
        {
            _selectedLinuxTemplate = DeploymentSettingsAccessor.QuerySavedValue(settings, "linuxTemplate", "Ubuntu 24.04 + Docker Base");
            _selectedDefaultCpu = DeploymentSettingsAccessor.QuerySavedValue(settings, "defaultCpuCores", "4");
            _selectedDefaultMemory = DeploymentSettingsAccessor.QuerySavedValue(settings, "defaultMemoryMb", "8192");
            _selectedDockerMode = DeploymentSettingsAccessor.QuerySavedValue(settings, "dockerMode", "docker compose");
            _workspaceDirectory = DeploymentSettingsAccessor.QuerySavedValue(settings, "workspaceDirectory");
            _isAutoStartEnabled = DeploymentSettingsAccessor.QuerySavedBoolean(settings, "autoStartAfterCreate", true);

            RaisePropertyChanged(nameof(SelectedLinuxTemplate));
            RaisePropertyChanged(nameof(SelectedDefaultCpu));
            RaisePropertyChanged(nameof(SelectedDefaultMemory));
            RaisePropertyChanged(nameof(SelectedDockerMode));
            RaisePropertyChanged(nameof(WorkspaceDirectory));
            RaisePropertyChanged(nameof(IsAutoStartEnabled));

            StatusText = "部署工具設定已載入。";
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// 清除宿主設定來源，重置狀態文字。
    /// </summary>
    internal void ClearSettings()
    {
        _settings = null;
        StatusText = string.Empty;
    }

    private void SaveString(string key, string? rawValue, string successMessage)
    {
        if (_settings is null) return;
        var result = DeploymentSettingsAccessor.SaveString(_settings, key, rawValue);
        StatusText = result.IsSuccess ? successMessage : $"儲存失敗：{result.DetailText}";
    }

    private void SaveBoolean(string key, bool value, string successMessage)
    {
        if (_settings is null) return;
        var result = DeploymentSettingsAccessor.SaveBoolean(_settings, key, value);
        StatusText = result.IsSuccess ? successMessage : $"儲存失敗：{result.DetailText}";
    }
}
