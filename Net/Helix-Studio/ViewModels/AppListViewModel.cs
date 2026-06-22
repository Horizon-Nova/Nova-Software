using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;
using System.Windows.Input;
using Helix_Studio.Data;
using Helix_Studio.Infrastructure;
using Helix_Studio.Models;
using Helix_Studio.Mvvm;
using Helix_Studio.Services;
using Helix_Studio.Services.Tools;
using EventLogEntry = Helix_Studio.Models.EventLogEntry;

namespace Helix_Studio.ViewModels;

public sealed class AppListViewModel : ViewModelBase
{
    private readonly ToolCatalogService _catalog;
    private readonly InstalledToolsRepository _repository;
    private readonly ToolInstallService _install;
    private readonly AppSettingsService _appSettings;
    private readonly EventLogService _eventLog;
    private string _workspaceId;

    private string _searchText = string.Empty;
    private bool _showOnlyRunning;
    private InstalledTool? _detailTool;
    private string _argsText = string.Empty;

    public override string PageKey => "apps";

    public ObservableCollection<InstalledTool> Apps { get; } = new();
    public ICollectionView AppsView { get; }
    public SystemStats Stats { get; }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand BulkStartCommand { get; }
    public ICommand BulkStopCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand ShowDetailCommand { get; }
    public ICommand CloseDetailCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand UninstallCommand { get; }

    /// <summary>由 Shell 處理實際的程序啟動/停止與嵌入。</summary>
    public event Action<InstalledTool>? OpenRequested;
    public event Action<InstalledTool>? StopRequested;

    /// <summary>解除安裝後通知 Shell 重新整理應用程式庫的已安裝標記。</summary>
    public event Action? InstalledChanged;

    public AppListViewModel(ToolCatalogService catalog, InstalledToolsRepository repository,
        ToolInstallService install, AppSettingsService appSettings, SystemStats stats,
        string workspaceId, EventLogService eventLog)
    {
        _catalog = catalog;
        _repository = repository;
        _install = install;
        _appSettings = appSettings;
        _eventLog = eventLog;
        _workspaceId = workspaceId;
        Stats = stats;

        AppsView = CollectionViewSource.GetDefaultView(Apps);
        AppsView.Filter = FilterApp;

        StartCommand = new RelayCommand(p => Raise(OpenRequested, p));
        StopCommand = new RelayCommand(p => Raise(StopRequested, p));
        BulkStartCommand = new RelayCommand(() => RaiseForSelected(OpenRequested));
        BulkStopCommand = new RelayCommand(() => RaiseForSelected(StopRequested));
        ClearSelectionCommand = new RelayCommand(ClearSelection);
        ShowDetailCommand = new RelayCommand(p => { if (p is InstalledTool t) DetailTool = t; });
        CloseDetailCommand = new RelayCommand(_ => DetailTool = null);
        SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        OpenFolderCommand = new RelayCommand(_ => OpenFolder());
        UninstallCommand = new RelayCommand(_ => UninstallDetail());

        Reload();
    }

    /// <summary>目前開啟詳細/設定面板的工具（null 表示未開啟）。</summary>
    public InstalledTool? DetailTool
    {
        get => _detailTool;
        set
        {
            if (!SetProperty(ref _detailTool, value)) return;
            ArgsText = value is null ? string.Empty : string.Join(Environment.NewLine, value.Settings.Args);
            OnPropertyChanged(nameof(HasDetail));
            OnPropertyChanged(nameof(DetailToolDir));
            OnPropertyChanged(nameof(DetailToolRecentEvents));
            OnPropertyChanged(nameof(HasDetailToolRecentEvents));
        }
    }

    public bool HasDetail => _detailTool is not null;

    /// <summary>詳細面板工具的安裝資料夾絕對路徑。</summary>
    public string DetailToolDir
        => _detailTool is null ? string.Empty : AppPathProvider.ToolDir(_workspaceId, _detailTool.Id);

    /// <summary>詳細面板工具的近期 3 筆事件（依時間降冪）。</summary>
    public IReadOnlyList<EventLogEntry> DetailToolRecentEvents
        => _detailTool is null
            ? []
            : [.. _eventLog.Entries
                .Where(e => e.Source == _detailTool.Name)
                .OrderByDescending(e => e.Timestamp)
                .Take(3)];

    public bool HasDetailToolRecentEvents => DetailToolRecentEvents.Count > 0;

    /// <summary>啟動參數編輯緩衝：一行一個參數。</summary>
    public string ArgsText
    {
        get => _argsText;
        set => SetProperty(ref _argsText, value);
    }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) AppsView.Refresh(); }
    }

    public bool ShowOnlyRunning
    {
        get => _showOnlyRunning;
        set { if (SetProperty(ref _showOnlyRunning, value)) AppsView.Refresh(); }
    }

    public int SelectedCount => Apps.Count(a => a.IsSelected);
    public bool HasSelection => Apps.Any(a => a.IsSelected);
    public bool IsEmpty => Apps.Count == 0;
    public bool HasApps => Apps.Count > 0;
    public int RunningCount => Apps.Count(a => a.IsRunning);

    /// <summary>FL-08 N-001: 語言切換後重整清單與詳細面板，使轉換器產生的文字（狀態/相對時間）重新取值。</summary>
    public void RefreshLocalizedText()
    {
        AppsView.Refresh();
        OnPropertyChanged(nameof(DetailTool));
        OnPropertyChanged(nameof(DetailToolRecentEvents));
    }

    /// <summary>切換工作區：重新指向新工作區並重建清單。</summary>
    public void SwitchWorkspace(string workspaceId)
    {
        _workspaceId = workspaceId;
        DetailTool = null;
        Reload();
    }

    /// <summary>從已安裝索引重建清單（安裝/解除安裝後由 Shell 呼叫）。</summary>
    public void Reload()
    {
        foreach (var app in Apps)
            app.PropertyChanged -= OnAppPropertyChanged;
        Apps.Clear();

        var manifests = _catalog.LoadCatalog().ToDictionary(m => m.Id);
        var records = _repository.Load(_workspaceId);
        var sorted = SortRecords(records);
        foreach (var record in sorted)
        {
            if (!manifests.TryGetValue(record.ToolId, out var manifest)) continue;
            var tool = new InstalledTool
            {
                Manifest = manifest,
                InstalledAt = record.InstalledAt,
                Settings = record.Settings,
            };
            tool.PropertyChanged += OnAppPropertyChanged;
            Apps.Add(tool);
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasApps));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(RunningCount));
        AppsView.Refresh();
    }

    private bool FilterApp(object obj)
    {
        if (obj is not InstalledTool app) return false;
        if (_showOnlyRunning && !app.IsRunning) return false;
        if (!string.IsNullOrWhiteSpace(_searchText)
            && !app.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstalledTool.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
        }
        else if (e.PropertyName == nameof(InstalledTool.Status))
        {
            OnPropertyChanged(nameof(RunningCount));
        }
    }

    private static void Raise(Action<InstalledTool>? handler, object? param)
    {
        if (param is InstalledTool tool) handler?.Invoke(tool);
    }

    private void RaiseForSelected(Action<InstalledTool>? handler)
    {
        foreach (var tool in Apps.Where(a => a.IsSelected).ToList())
            handler?.Invoke(tool);
    }

    private void ClearSelection()
    {
        foreach (var app in Apps)
            app.IsSelected = false;
    }

    private IEnumerable<InstalledToolRecord> SortRecords(IReadOnlyList<InstalledToolRecord> records)
    {
        return _appSettings.Settings.ToolSortRule switch
        {
            "name" => records.OrderBy(r => r.ToolId),
            "installedAt" => records.OrderBy(r => r.InstalledAt),
            _ => records.OrderBy(r => r.Settings.SortIndex == int.MaxValue ? records.Count : r.Settings.SortIndex),
        };
    }

    /// <summary>FL-03 N-006: 傳回目前工作區中設定了 AutoStart 的工具清單。</summary>
    public IReadOnlyList<InstalledTool> GetAutoStartTools()
        => Apps.Where(a => a.Settings.AutoStart).ToList();

    /// <summary>FL-03 N-007: 儲存拖曳後的新排序到 installed.json。</summary>
    public void SaveCustomOrder()
    {
        var records = _repository.Load(_workspaceId).ToList();
        for (int i = 0; i < Apps.Count; i++)
        {
            var idx = records.FindIndex(r => r.ToolId == Apps[i].Id);
            if (idx < 0) continue;
            var r = records[idx];
            records[idx] = new InstalledToolRecord
            {
                ToolId = r.ToolId,
                InstalledAt = r.InstalledAt,
                Settings = new ToolSettings
                {
                    Args = r.Settings.Args,
                    AutoStart = r.Settings.AutoStart,
                    SortIndex = i,
                },
            };
        }
        _repository.Save(_workspaceId, records);
    }

    private void SaveSettings()
    {
        if (_detailTool is null) return;
        var args = _argsText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var records = _repository.Load(_workspaceId).ToList();
        var idx = records.FindIndex(r => r.ToolId == _detailTool.Id);
        var existing = idx >= 0 ? records[idx] : null;
        var settings = new ToolSettings
        {
            Args = args,
            AutoStart = _detailTool.Settings.AutoStart,
            SortIndex = existing?.Settings.SortIndex ?? int.MaxValue,
        };
        _detailTool.Settings = settings;
        _install.UpdateSettings(_workspaceId, _detailTool.Id, settings);
    }

    private void OpenFolder()
    {
        if (_detailTool is null) return;
        Process.Start(new ProcessStartInfo { FileName = DetailToolDir, UseShellExecute = true });
    }

    private void UninstallDetail()
    {
        if (_detailTool is null) return;
        var tool = _detailTool;
        if (tool.IsRunning)
            StopRequested?.Invoke(tool);   // 先停止，解除程序對工具檔案的鎖定
        _install.Uninstall(_workspaceId, tool.Id);
        DetailTool = null;
        Reload();
        InstalledChanged?.Invoke();
    }
}
