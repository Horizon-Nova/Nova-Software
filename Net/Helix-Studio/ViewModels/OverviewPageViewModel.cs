using System;
using System.Collections.ObjectModel;
using System.Linq;
using Helix_Studio.Model.Overview;
using Helix_Studio.Services;
using Helix_Studio.ViewModels.Base;

namespace Helix_Studio.ViewModels;

/// <summary>首頁概覽：系統、工作區與本工作區內已載入模組卡片。</summary>
public sealed class OverviewPageViewModel : ViewModelBase, IDisposable
{
    private readonly OverviewServices _service;
    private readonly ModuleRuntimeServices _moduleRuntime;

    private string _machineName = string.Empty;
    private string _subtitleText = string.Empty;

    private string _osInfoShort = "—";
    private string _osInfoFull = "—";
    private string _uptimeText = "Uptime: —";

    private string _workspaceName = "—";
    private string _workspaceDescription = "—";

    private string _moduleCountText = "0";
    private string _moduleStatusText = "No modules in this workspace";

    private string _buildVersionText = "—";
    private string _dataFolderPath = "—";

    /// <summary>使用 <see cref="AppServices.OverviewPage"/> 與模組執行時期服務的預設建構子。</summary>
    public OverviewPageViewModel()
        : this(AppServices.OverviewPage, AppServices.ModuleRuntime) { }

    /// <summary>以概覽與模組執行時期服務建立，並訂閱模組目錄變更以重新整理卡片。</summary>
    public OverviewPageViewModel(OverviewServices service, ModuleRuntimeServices moduleRuntime)
    {
        _service = service;
        _moduleRuntime = moduleRuntime;

        LoadedCommand = new RelayCommand(LoadSnapshot);

        _moduleRuntime.CatalogChanged += OnCatalogChanged;
    }

    /// <summary>初次顯示時載入系統、工作區與模組卡片快照。</summary>
    public RelayCommand LoadedCommand { get; }

    /// <summary>顯示用主機名稱。</summary>
    public string MachineName
    {
        get => _machineName;
        private set => SetProperty(ref _machineName, value);
    }

    /// <summary>首頁副標或情境說明文字。</summary>
    public string SubtitleText
    {
        get => _subtitleText;
        private set => SetProperty(ref _subtitleText, value);
    }

    /// <summary>作業系統描述之截斷顯示（供緊湊版型）。</summary>
    public string OsInfoShort
    {
        get => _osInfoShort;
        private set => SetProperty(ref _osInfoShort, value);
    }

    /// <summary>完整作業系統字串（工具提示或展開用）。</summary>
    public string OsInfoFull
    {
        get => _osInfoFull;
        private set => SetProperty(ref _osInfoFull, value);
    }

    /// <summary>應用程式執行時間的顯示字串。</summary>
    public string UptimeText
    {
        get => _uptimeText;
        private set => SetProperty(ref _uptimeText, value);
    }

    /// <summary>啟用中工作區名稱。</summary>
    public string WorkspaceName
    {
        get => _workspaceName;
        private set => SetProperty(ref _workspaceName, value);
    }

    /// <summary>工作區說明或建立時間敘述。</summary>
    public string WorkspaceDescription
    {
        get => _workspaceDescription;
        private set => SetProperty(ref _workspaceDescription, value);
    }

    /// <summary>以字串顯示的已載入模組數量。</summary>
    public string ModuleCountText
    {
        get => _moduleCountText;
        private set => SetProperty(ref _moduleCountText, value);
    }

    /// <summary>模組狀態列簡要說明（例如有無模組、數量敘述）。</summary>
    public string ModuleStatusText
    {
        get => _moduleStatusText;
        private set => SetProperty(ref _moduleStatusText, value);
    }

    /// <summary>應用程式組建版本字串（供 System Information 區塊）。</summary>
    public string BuildVersionText
    {
        get => _buildVersionText;
        private set => SetProperty(ref _buildVersionText, value);
    }

    /// <summary>本機應用程式資料目錄路徑（供 System Information 區塊）。</summary>
    public string DataFolderPath
    {
        get => _dataFolderPath;
        private set => SetProperty(ref _dataFolderPath, value);
    }

    /// <summary>本工作區內已載入模組的展示卡片集合。</summary>
    public ObservableCollection<OverviewModuleCard> Modules { get; } = new();

    /// <summary>是否至少有一個模組卡片可顯示。</summary>
    public bool HasModules => Modules.Count > 0;

    private void LoadSnapshot()
    {
        LoadSystemInfo();
        LoadWorkspaceInfo();
        LoadModules();
    }

    private void LoadSystemInfo()
    {
        var info = _service.QueryOverviewInfo();

        MachineName = info.DeviceName;
        SubtitleText = info.SubtitleText;
        OsInfoFull = info.OsDescription;
        OsInfoShort = info.OsDescription.Length > 40 ? info.OsDescription[..40] + "…" : info.OsDescription;
        UptimeText = info.UptimeText;
        BuildVersionText = info.BuildVersionText;
        DataFolderPath = info.DataPath;
    }

    private void LoadWorkspaceInfo()
    {
        var workspace = _service.QueryActiveWorkspace();
        WorkspaceName = workspace?.Name ?? "—";

        if (workspace is null)
        {
            WorkspaceDescription = "目前沒有啟用的工作區。";
            return;
        }

        WorkspaceDescription = string.IsNullOrEmpty(workspace.Description)
            ? $"建立時間：{workspace.CreatedAt}"
            : $"{workspace.Description}  ·  建立時間：{workspace.CreatedAt}";
    }

    private void LoadModules()
    {
        var manifests = _moduleRuntime.GetManifests();

        Modules.Clear();
        foreach (var m in manifests)
        {
            Modules.Add(new OverviewModuleCard(m.Id, m.DisplayName, m.Version, m.Description, m.IconPngBytes));
        }

        RaisePropertyChanged(nameof(HasModules));

        ModuleCountText = manifests.Count.ToString();
        ModuleStatusText = manifests.Count == 0
            ? "No modules in this workspace"
            : $"{manifests.Count} module{(manifests.Count > 1 ? "s" : "")} active";
    }

    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        LoadWorkspaceInfo();
        LoadModules();
    }

    /// <summary>取消訂閱模組目錄變更事件。</summary>
    public void Dispose()
    {
        _moduleRuntime.CatalogChanged -= OnCatalogChanged;
    }
}
