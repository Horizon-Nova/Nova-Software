using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Vaeron.HelixStudio.EventLog.Internal.Models;

internal sealed class EventLogDbContext(DbContextOptions<EventLogDbContext> options) : DbContext(options)
{
    public DbSet<EventEntryRow> events => Set<EventEntryRow>();
    public DbSet<LogSettingsRow> log_settings_rows => Set<LogSettingsRow>();
    public DbSet<ReadMarkerRow> read_markers => Set<ReadMarkerRow>();

    /// <summary>DateTimeOffset 可查詢的字串轉換器（可排序、與既有資料格式相容）</summary>
    private static readonly ValueConverter<DateTimeOffset, string> DateTimeOffsetToSortableString = new(
        v => v.ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz"),
        v => DateTimeOffset.Parse(v, System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>read_marker 主鍵設定</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReadMarkerRow>().HasKey(x => x.workspace_id);

        modelBuilder.Entity<EventEntryRow>().Property(x => x.timestamp).HasConversion(DateTimeOffsetToSortableString);
        modelBuilder.Entity<ReadMarkerRow>().Property(x => x.last_read_at).HasConversion(DateTimeOffsetToSortableString);
    }
}

internal sealed class EventEntryRow
{
    public int id { get; set; }
    public int workspace_id { get; set; }
    public string tool_name { get; set; } = "";
    public string severity { get; set; } = "Info";
    public string message { get; set; } = "";
    public DateTimeOffset timestamp { get; set; }
    public string? command { get; set; }
    public int? exit_code { get; set; }
    public string? output { get; set; }
}

/// <summary>單列表：全域日誌設定</summary>
internal sealed class LogSettingsRow
{
    public int id { get; set; }
    public string min_level { get; set; } = "Info";
    public int retention_days { get; set; } = 30;
    public string? export_path { get; set; }
}

/// <summary>每工作區一列：上次已讀時間</summary>
internal sealed class ReadMarkerRow
{
    public int workspace_id { get; set; }
    public DateTimeOffset last_read_at { get; set; }
}
