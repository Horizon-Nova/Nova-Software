using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using Helix_Studio.Model;
using Helix_Studio.Services;
using Helix_Studio.ViewModels.Base;

namespace Helix_Studio.ViewModels;

/// <summary>
/// 概覽頁面的 UI 狀態機。
/// </summary>
public sealed class OverviewPageViewModel(OverviewServices services) : ViewModelBase, IDisposable
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
    /// 機器名稱。
    /// </summary>
    public string MachineName
    {
        get => _machineName;
        private set => SetField(ref _machineName, value);
    }

    /// <summary>
    /// 副標題文字。
    /// </summary>
    public string SubtitleText
    {
        get => _subtitleText;
        private set => SetField(ref _subtitleText, value);
    }

    /// <summary>
    /// 作業系統短資訊。
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
    /// 開機時間文字。
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
    /// 模組狀態文字。
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
    /// 資料資料夾路徑。
    /// </summary>
    public string DataFolderPath
    {
        get => _dataFolderPath;
        private set => SetField(ref _dataFolderPath, value);
    }

    /// <summary>
    /// 模組清單。
    /// </summary>
    public ObservableCollection<OverviewModuleItemModel> Modules { get; } = [];

    /// <summary>
    /// 是否有模組可顯示。
    /// </summary>
    public bool HasModules => Modules.Count > 0;

    /// <summary>
    /// 初始化概覽頁面的 I/O 訂閱與初始快照。
    /// </summary>
    internal void ApplyViewLoaded()
    {
        if (_isInitialized)
        {
            return;
        }

        _synchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();
        services.Changed += OnChanged;
        Modules.CollectionChanged += OnModulesChanged;
        ApplySnapshot(services.QueryOverview());
        _isInitialized = true;
    }

    /// <summary>
    /// 釋放概覽頁面的事件訂閱。
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_isInitialized)
        {
            services.Changed -= OnChanged;
            Modules.CollectionChanged -= OnModulesChanged;
        }

        _isDisposed = true;
    }

    private void OnChanged(object? sender, EventArgs e)
        => PostToUi(() => ApplySnapshot(services.QueryOverview()));

    private void OnModulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => NotifyPropertyChanged(nameof(HasModules));

    private void PostToUi(Action action)
        => _synchronizationContext.Post(_ => action(), null);

    private void ApplySnapshot(OverviewModel snapshot)
    {
        MachineName = snapshot.MachineName;
        SubtitleText = snapshot.SubtitleText;
        OsInfoShort = snapshot.OsInfoShort;
        OsInfoFull = snapshot.OsInfoFull;
        UptimeText = snapshot.UptimeText;
        WorkspaceName = snapshot.WorkspaceName;
        WorkspaceDescription = snapshot.WorkspaceDescription;
        ModuleCountText = snapshot.ModuleCountText;
        ModuleStatusText = snapshot.ModuleStatusText;
        BuildVersionText = snapshot.BuildVersionText;
        DataFolderPath = snapshot.DataFolderPath;

        Modules.Clear();
        foreach (var module in snapshot.Modules)
        {
            Modules.Add(module);
        }
    }
}

