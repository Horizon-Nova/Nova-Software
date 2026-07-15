using Microsoft.EntityFrameworkCore;

namespace Vaeron.HelixStudio.Features.EventLog.Data;

public sealed class EventLogDbContext(DbContextOptions<EventLogDbContext> options) : DbContext(options)
{
    public DbSet<event_entry> events => Set<event_entry>();
    public DbSet<log_settings> log_settings_rows => Set<log_settings>();
    public DbSet<read_marker> read_markers => Set<read_marker>();

    /// <summary>read_marker 沒有 EF 慣例能自動辨識的 "id" 欄位（自然鍵是 workspace_id 本身，
    /// 一個工作區一列）——EF Core 的預設鍵值慣例只認得 "Id" 或 "{類別名}Id" 命名，兩者都不符，
    /// 不明確指定會在 EnsureCreatedAsync 時丟 ModelValidator 例外，故這裡明確指定主鍵。</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<read_marker>().HasKey(x => x.workspace_id);
}

public sealed class event_entry
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

/// <summary>單列表，id 永遠是 1——全域日誌設定，見 configuration.md 的 SQLite 判準。</summary>
public sealed class log_settings
{
    public int id { get; set; }
    public string min_level { get; set; } = "Info";
    public int retention_days { get; set; } = 30;
}

/// <summary>每個工作區一列——上次已讀時間，用來算未讀徽章數，不用整數加減。</summary>
public sealed class read_marker
{
    public int workspace_id { get; set; }
    public DateTimeOffset last_read_at { get; set; }
}
