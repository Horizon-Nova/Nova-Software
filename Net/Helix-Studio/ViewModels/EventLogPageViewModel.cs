using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Helix_Studio.Model.EventLog;
using Helix_Studio.Services;
using Helix_Studio.ViewModels.Base;

namespace Helix_Studio.ViewModels;

/// <summary>
/// 事件日誌頁面 ViewModel，負責提供篩選、查詢與清除操作的 UI 狀態與命令。
/// </summary>
public sealed class EventLogPageViewModel : ViewModelBase, IDisposable
{
    private static readonly IReadOnlyDictionary<string, LogLevel> LevelByTag =
        new Dictionary<string, LogLevel>(StringComparer.OrdinalIgnoreCase)
        {
            ["Info"] = LogLevel.Info,
            ["Warning"] = LogLevel.Warning,
            ["Error"] = LogLevel.Error,
            ["Debug"] = LogLevel.Debug,
        };

    private readonly EventLogServices _eventLogService;
    private LogLevel? _activeLevel;
    private string _keyword = string.Empty;
    private bool _isRefreshing;

    /// <summary>以指定之事件日誌服務建立 ViewModel 並訂閱新項目事件。</summary>
    public EventLogPageViewModel(EventLogServices eventLogService)
    {
        _eventLogService = eventLogService;
        _eventLogService.EntryAdded += OnEntryAdded;

        RefreshCommand = new RelayCommand(Refresh);
        ClearCommand = new RelayCommand(Clear);
        SetLevelFilterCommand = new RelayCommand(SetLevelFilterByParameter);
    }

    /// <summary>使用 <see cref="EventLogServices.Instance"/> 的預設建構子。</summary>
    public EventLogPageViewModel()
        : this(EventLogServices.Instance) { }

    /// <summary>目前畫面顯示的日誌條目集合（已篩選）。</summary>
    public ObservableCollection<LogEntry> Entries { get; } = [];

    /// <summary>是否有任何日誌條目可供顯示。</summary>
    public bool HasEntries => Entries.Count > 0;

    /// <summary>重新整理列表（重查服務層篩選結果）。</summary>
    public RelayCommand RefreshCommand { get; }

    /// <summary>清除內部日誌緩衝並寫入一筆提示日誌。</summary>
    public RelayCommand ClearCommand { get; }

    /// <summary>依字串參數（層級標籤）更新作用中層級篩選。</summary>
    public RelayCommand SetLevelFilterCommand { get; }

    /// <summary>關鍵字篩選；變更時自動重新整理條目。</summary>
    public string Keyword
    {
        get => _keyword;
        set
        {
            value ??= string.Empty;
            if (!SetProperty(ref _keyword, value))
                return;

            Refresh();
        }
    }

    private void SetLevelFilterByParameter(object? parameter)
    {
        var tag = parameter as string;
        _activeLevel = tag is not null && LevelByTag.TryGetValue(tag, out var level)
            ? level
            : null;
        Refresh();
    }

    private void Refresh()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        var entries = _eventLogService.GetFiltered(_activeLevel, _keyword);
        Entries.Clear();
        foreach (var entry in entries)
        {
            Entries.Add(entry);
        }

        RaisePropertyChanged(nameof(HasEntries));
        _isRefreshing = false;
    }

    private void Clear()
    {
        _eventLogService.Clear();
        EventLogServices.Info("已由使用者清除事件日誌。", "Shell");
        Refresh();
    }

    private void OnEntryAdded()
    {
        Refresh();
    }

    /// <summary>取消訂閱日誌服務事件。</summary>
    public void Dispose()
    {
        _eventLogService.EntryAdded -= OnEntryAdded;
    }
}
