using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using Helix_Studio.Models;
using Helix_Studio.Mvvm;
using Helix_Studio.Services;

namespace Helix_Studio.ViewModels;

public sealed class EventLogViewModel : ViewModelBase
{
    private readonly EventLogService _eventLog;
    private readonly AppSettingsService _appSettings;

    private string _searchText = string.Empty;
    private EventLevel? _levelFilter;
    private EventCategory? _categoryFilter;

    private static readonly Dictionary<string, EventLevel?> LevelKeys = new()
    {
        ["all"] = null,
        ["info"] = EventLevel.Info,
        ["warning"] = EventLevel.Warning,
        ["error"] = EventLevel.Error,
    };

    private static readonly Dictionary<string, EventCategory?> CategoryKeys = new()
    {
        ["all"] = null,
        ["system"] = EventCategory.System,
        ["install"] = EventCategory.Install,
        ["runtime"] = EventCategory.Runtime,
    };

    private static readonly Dictionary<EventLevel, string> LevelText = new()
    {
        [EventLevel.Info] = "資訊",
        [EventLevel.Warning] = "警告",
        [EventLevel.Error] = "錯誤",
    };

    private static readonly Dictionary<EventCategory, string> CategoryText = new()
    {
        [EventCategory.System] = "系統",
        [EventCategory.Install] = "安裝",
        [EventCategory.Runtime] = "執行",
    };

    public override string PageKey => "events";

    public ICollectionView EntriesView { get; }

    public int TotalCount => _eventLog.Entries.Count;
    public int ErrorCount => _eventLog.Entries.Count(e => e.Level == EventLevel.Error);
    public int AlertCount => _eventLog.Entries.Count(e => e.Level is EventLevel.Error or EventLevel.Warning);
    public bool HasAlerts => AlertCount > 0;

    /// <summary>未讀警告+錯誤數（lastReadAt 之後的事件）。供側欄 badge 綁定。</summary>
    public int UnreadCount => _eventLog.Entries.Count(e =>
        e.Level is EventLevel.Warning or EventLevel.Error &&
        e.Timestamp.ToUniversalTime() > _appSettings.Settings.LastReadAt);

    public bool HasUnread => UnreadCount > 0;

    public ICommand SetLevelCommand { get; }
    public ICommand SetCategoryCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand MarkAllReadCommand { get; }

    public EventLogViewModel(EventLogService eventLog, AppSettingsService appSettings)
    {
        _eventLog = eventLog;
        _appSettings = appSettings;

        EntriesView = CollectionViewSource.GetDefaultView(eventLog.Entries);
        EntriesView.Filter = FilterEntry;

        ((INotifyCollectionChanged)eventLog.Entries).CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(ErrorCount));
            OnPropertyChanged(nameof(AlertCount));
            OnPropertyChanged(nameof(HasAlerts));
            OnPropertyChanged(nameof(UnreadCount));
            OnPropertyChanged(nameof(HasUnread));
        };

        SetLevelCommand = new RelayCommand(p =>
        {
            if (p is string key && LevelKeys.TryGetValue(key, out var level))
                LevelFilter = level;
        });
        SetCategoryCommand = new RelayCommand(p =>
        {
            if (p is string key && CategoryKeys.TryGetValue(key, out var category))
                CategoryFilter = category;
        });
        CopyCommand = new RelayCommand(p =>
        {
            if (p is EventLogEntry entry)
                System.Windows.Clipboard.SetText(Format(entry));
        });
        ClearCommand = new RelayCommand(_ => _eventLog.Clear());
        ExportCommand = new RelayCommand(_ => Export());
        MarkAllReadCommand = new RelayCommand(_ => MarkAllRead());
    }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) EntriesView.Refresh(); }
    }

    public EventLevel? LevelFilter
    {
        get => _levelFilter;
        set { if (SetProperty(ref _levelFilter, value)) EntriesView.Refresh(); }
    }

    public EventCategory? CategoryFilter
    {
        get => _categoryFilter;
        set { if (SetProperty(ref _categoryFilter, value)) EntriesView.Refresh(); }
    }

    private bool FilterEntry(object obj)
    {
        if (obj is not EventLogEntry e) return false;
        if (_levelFilter is not null && e.Level != _levelFilter) return false;
        if (_categoryFilter is not null && e.Category != _categoryFilter) return false;
        if (!string.IsNullOrWhiteSpace(_searchText)
            && !e.Message.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            && !e.Source.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private void MarkAllRead()
    {
        _appSettings.Settings.LastReadAt = DateTime.UtcNow;
        _appSettings.Save();
        OnPropertyChanged(nameof(UnreadCount));
        OnPropertyChanged(nameof(HasUnread));
    }

    private void Export()
    {
        var dialog = new SaveFileDialog
        {
            Title = "匯出事件日誌",
            Filter = "文字檔 (*.txt)|*.txt",
            FileName = "helix-eventlog.txt",
        };
        if (dialog.ShowDialog() != true) return;

        var lines = _eventLog.Entries.Reverse().Select(Format);
        File.WriteAllLines(dialog.FileName, lines, Encoding.UTF8);
    }

    private static string Format(EventLogEntry e)
    {
        var level = LevelText.GetValueOrDefault(e.Level, e.Level.ToString());
        var category = CategoryText.GetValueOrDefault(e.Category, e.Category.ToString());
        return $"[{e.Timestamp:yyyy-MM-dd HH:mm:ss}] [{level}] [{category}] {e.Source}: {e.Message}";
    }
}
