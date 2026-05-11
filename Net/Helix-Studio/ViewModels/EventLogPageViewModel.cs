using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using Helix_Studio.Model;
using Helix_Studio.Services;
using Helix_Studio.ViewModels.Base;

namespace Helix_Studio.ViewModels;

/// <summary>
/// 事件日誌頁面的 UI 狀態機。
/// </summary>
public sealed class EventLogPageViewModel(EventLogServices services) : ViewModelBase, IDisposable
{
    private SynchronizationContext _synchronizationContext = new();
    private RelayCommand? _setLevelFilterCommand;
    private RelayCommand? _refreshCommand;
    private RelayCommand? _exportZipCommand;
    private bool _isInitialized;
    private bool _isDisposed;
    private string _keyword = string.Empty;
    private string _levelFilter = "All";

    /// <summary>
    /// 日誌搜尋關鍵字。
    /// </summary>
    public string Keyword
    {
        get => _keyword;
        set
        {
            if (!SetField(ref _keyword, value))
            {
                return;
            }

            ApplySnapshot();
        }
    }

    /// <summary>
    /// 日誌項目清單。
    /// </summary>
    public ObservableCollection<EntryRow> Entries { get; } = [];

    /// <summary>
    /// 是否存在日誌項目。
    /// </summary>
    public bool HasEntries => Entries.Count > 0;

    /// <summary>
    /// 設定等級篩選。
    /// </summary>
    public ICommand SetLevelFilterCommand
        => _setLevelFilterCommand ??= new RelayCommand(parameter => ApplySetLevelFilter(parameter?.ToString() ?? "All"));

    /// <summary>
    /// 重新整理。
    /// </summary>
    public ICommand RefreshCommand
        => _refreshCommand ??= new RelayCommand(_ => ApplySnapshot());

    /// <summary>
    /// 下載日誌壓縮檔。
    /// </summary>
    public ICommand ExportZipCommand
        => _exportZipCommand ??= new RelayCommand(_ => ApplyExportZip());

    /// <summary>
    /// 初始化事件日誌頁面的 I/O 訂閱與初始快照。
    /// </summary>
    internal void ApplyViewLoaded()
    {
        if (_isInitialized)
        {
            return;
        }

        _synchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();
        services.Changed += OnChanged;
        Entries.CollectionChanged += OnEntriesChanged;
        ApplySnapshot();
        _isInitialized = true;
    }

    /// <summary>
    /// 釋放事件訂閱。
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
            Entries.CollectionChanged -= OnEntriesChanged;
        }

        _isDisposed = true;
    }

    private void OnChanged(object? sender, EventArgs e)
        => PostToUi(ApplySnapshot);

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => NotifyPropertyChanged(nameof(HasEntries));

    private void PostToUi(Action action)
        => _synchronizationContext.Post(_ => action(), null);

    private void ApplySetLevelFilter(string level)
    {
        _levelFilter = level;
        ApplySnapshot();
    }

    private void ApplySnapshot()
    {
        var keyword = Keyword?.Trim() ?? string.Empty;
        var filter = _levelFilter;

        var entries = services.QueryEntries()
            .Where(entry => filter == "All" || string.Equals(entry.Level.ToString(), filter, StringComparison.OrdinalIgnoreCase))
            .Where(entry => string.IsNullOrEmpty(keyword)
                || entry.Message.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || entry.Source.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Select(static entry => new EntryRow(entry))
            .ToList();

        Entries.Clear();
        foreach (var row in entries)
        {
            Entries.Add(row);
        }
    }

    private void ApplyExportZip()
    {
        _ = services.ExportZip();
    }

    /// <summary>
    /// 事件日誌資料列。
    /// </summary>
    public sealed class EntryRow
    {
        /// <summary>
        /// 建立資料列。
        /// </summary>
        /// <param name="entry">事件日誌項目。</param>
        public EntryRow(EventLogEntryModel entry)
        {
            Level = entry.Level.ToString();
            LevelText = entry.Level.ToString().ToUpperInvariant();
            TimestampText = entry.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            Source = entry.Source;
            Message = entry.Message;
        }

        /// <summary>
        /// 時間文字。
        /// </summary>
        public string TimestampText { get; }

        /// <summary>
        /// 等級。
        /// </summary>
        public string Level { get; }

        /// <summary>
        /// 等級顯示文字。
        /// </summary>
        public string LevelText { get; }

        /// <summary>
        /// 來源。
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// 訊息。
        /// </summary>
        public string Message { get; }
    }
}

