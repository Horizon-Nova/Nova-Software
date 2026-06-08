using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using Helix_Studio.AppRuntime;
using Helix_Studio.Domain;
using Helix_Studio.Model;
using Helix_Studio.ViewModels.Base;

namespace Helix_Studio.ViewModels;

/// <summary>
/// 概覽頁 ViewModel。
/// </summary>
public sealed class OverviewPageViewModel(
    OverviewDomain domain,
    LocalizationRuntime localizationRuntime) : ViewModelBase, IDisposable
{
    private SynchronizationContext _synchronizationContext = new();
    private bool _isInitialized;
    private bool _isDisposed;
    private string _machineName = string.Empty;
    private string _subtitleText = string.Empty;
    private string _osInfoShort = string.Empty;
    private string _osInfoFull = string.Empty;
    private string _uptimeText = string.Empty;
    private string _workspaceName = string.Empty;
    private string _workspaceDescription = string.Empty;
    private string _moduleCountText = string.Empty;
    private string _moduleStatusText = string.Empty;
    private string _buildVersionText = string.Empty;
    private string _dataFolderPath = string.Empty;

    /// <summary>
    /// 目前裝置名稱。
    /// </summary>
    public string MachineName
    {
        get => _machineName;
        private set => SetField(ref _machineName, value);
    }

    /// <summary>
    /// 概覽副標題文字。
    /// </summary>
    public string SubtitleText
    {
        get => _subtitleText;
        private set => SetField(ref _subtitleText, value);
    }

    /// <summary>
    /// 作業系統簡短資訊。
    /// </summary>
    public string OsInfoShort
    {
        get => _osInfoShort;
        private set => SetField(ref _osInfoShort, value);
    }

    /// <summary>
    /// 作業系統完整資訊。
    /// </summary>
    public string OsInfoFull
    {
        get => _osInfoFull;
        private set => SetField(ref _osInfoFull, value);
    }

    /// <summary>
    /// 系統運作時間文字。
    /// </summary>
    public string UptimeText
    {
        get => _uptimeText;
        private set => SetField(ref _uptimeText, value);
    }

    /// <summary>
    /// 目前工作區名稱。
    /// </summary>
    public string WorkspaceName
    {
        get => _workspaceName;
        private set => SetField(ref _workspaceName, value);
    }

    /// <summary>
    /// 目前工作區描述。
    /// </summary>
    public string WorkspaceDescription
    {
        get => _workspaceDescription;
        private set => SetField(ref _workspaceDescription, value);
    }

    /// <summary>
    /// 模組數量文字。
    /// </summary>
    public string ModuleCountText
    {
        get => _moduleCountText;
        private set => SetField(ref _moduleCountText, value);
    }

    /// <summary>
    /// 模組狀態摘要文字。
    /// </summary>
    public string ModuleStatusText
    {
        get => _moduleStatusText;
        private set => SetField(ref _moduleStatusText, value);
    }

    /// <summary>
    /// 建置版本文字。
    /// </summary>
    public string BuildVersionText
    {
        get => _buildVersionText;
        private set => SetField(ref _buildVersionText, value);
    }

    /// <summary>
    /// App Home Root 路徑文字。
    /// </summary>
    public string DataFolderPath
    {
        get => _dataFolderPath;
        private set => SetField(ref _dataFolderPath, value);
    }

    /// <summary>
    /// 概覽頁模組清單。
    /// </summary>
    public ObservableCollection<OverviewModuleItemModel> Modules { get; } = [];

    /// <summary>
    /// 是否存在可顯示的模組。
    /// </summary>
    public bool HasModules => Modules.Count > 0;

    internal void ApplyViewLoaded()
    {
        if (_isInitialized)
        {
            return;
        }

        _synchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();
        domain.Changed += OnChanged;
        Modules.CollectionChanged += OnModulesChanged;

        SubtitleText = localizationRuntime.QueryString("Loc_Overview_Subtitle");
        ApplySnapshot(domain.QueryOverview());
        _isInitialized = true;
    }

    /// <summary>
    /// 釋放事件訂閱與 ViewModel 資源。
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_isInitialized)
        {
            domain.Changed -= OnChanged;
            Modules.CollectionChanged -= OnModulesChanged;
        }

        _isDisposed = true;
    }

    private void OnChanged(object? sender, EventArgs e)
        => PostToUi(() => ApplySnapshot(domain.QueryOverview()));

    private void OnModulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => NotifyPropertyChanged(nameof(HasModules));

    private void PostToUi(Action action)
        => _synchronizationContext.Post(_ => action(), null);

    private void ApplySnapshot(OverviewModel snapshot)
    {
        MachineName = snapshot.MachineName;
        OsInfoShort = snapshot.OsInfoShort;
        OsInfoFull = snapshot.OsInfoFull;
        UptimeText = localizationRuntime.Format("Loc_Overview_Uptime", snapshot.UptimeText);

        WorkspaceName = string.IsNullOrWhiteSpace(snapshot.WorkspaceName)
            ? localizationRuntime.QueryString("Loc_Common_Unknown")
            : snapshot.WorkspaceName;

        WorkspaceDescription = string.IsNullOrWhiteSpace(snapshot.WorkspaceDescription)
            ? localizationRuntime.QueryString("Loc_Overview_WorkspaceNotSelected")
            : snapshot.WorkspaceDescription;

        ModuleCountText = snapshot.ModuleCountText;
        ModuleStatusText = snapshot.Modules.Count > 0
            ? localizationRuntime.QueryString("Loc_Overview_ModulesLoaded")
            : localizationRuntime.QueryString("Loc_Overview_ModulesEmpty");

        BuildVersionText = string.IsNullOrWhiteSpace(snapshot.BuildVersionText)
            ? localizationRuntime.QueryString("Loc_Common_Unknown")
            : snapshot.BuildVersionText;

        DataFolderPath = snapshot.DataFolderPath;

        Modules.Clear();
        foreach (var module in snapshot.Modules)
        {
            Modules.Add(module);
        }

        NotifyPropertyChanged(nameof(HasModules));
    }
}
