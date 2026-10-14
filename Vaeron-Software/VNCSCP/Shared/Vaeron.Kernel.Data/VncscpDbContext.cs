using Microsoft.EntityFrameworkCore;
using Vaeron.Kernel.Data.Models;

namespace Vaeron.Kernel.Data;

/// <summary>app.db 的唯一資料容器。表名欄名一律明確指定，不依賴慣例推導。</summary>
public sealed class VncscpDbContext(DbContextOptions<VncscpDbContext> options) : DbContext(options)
{
    public DbSet<ConnectionEntity> Connections => Set<ConnectionEntity>();
    public DbSet<EventLogEntity> EventLogs => Set<EventLogEntity>();
    public DbSet<DeploymentHistoryEntity> DeploymentHistories => Set<DeploymentHistoryEntity>();

    public DbSet<ConnectionRow> ConnectionView => Set<ConnectionRow>();
    public DbSet<ConnectionGroupRow> ConnectionGroupView => Set<ConnectionGroupRow>();
    public DbSet<EventLogRow> EventLogView => Set<EventLogRow>();
    public DbSet<ConnectionRecentEventRow> ConnectionRecentEventView => Set<ConnectionRecentEventRow>();
    public DbSet<DeploymentHistoryRow> DeploymentHistoryView => Set<DeploymentHistoryRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConnectionEntity>(entity =>
        {
            entity.ToTable("connection");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.Ip).HasColumnName("ip");
            entity.Property(x => x.Port).HasColumnName("port");
            entity.Property(x => x.PasswordCipher).HasColumnName("password_cipher");
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.LastTestedAt).HasColumnName("last_tested_at");
            entity.Property(x => x.LastTestMessage).HasColumnName("last_test_message");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            entity.Property(x => x.GroupName).HasColumnName("group_name");
        });

        modelBuilder.Entity<EventLogEntity>(entity =>
        {
            entity.ToTable("event_log");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OccurredUtcTicks).HasColumnName("occurred_utc_ticks");
            entity.Property(x => x.Level).HasColumnName("level");
            entity.Property(x => x.Source).HasColumnName("source");
            entity.Property(x => x.Message).HasColumnName("message");
            entity.Property(x => x.Detail).HasColumnName("detail");
            entity.Property(x => x.RelatedConnectionId).HasColumnName("related_connection_id");
        });

        modelBuilder.Entity<DeploymentHistoryEntity>(entity =>
        {
            entity.ToTable("deployment_history");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.DeployedTicks).HasColumnName("deployed_ticks");
            entity.Property(x => x.ConnectionId).HasColumnName("connection_id");
            entity.Property(x => x.TargetName).HasColumnName("target_name");
            entity.Property(x => x.TargetIp).HasColumnName("target_ip");
            entity.Property(x => x.LocalPath).HasColumnName("local_path");
            entity.Property(x => x.RemotePath).HasColumnName("remote_path");
            entity.Property(x => x.Success).HasColumnName("success");
            entity.Property(x => x.Message).HasColumnName("message");
            entity.Property(x => x.OperatedBy).HasColumnName("operated_by");
        });

        modelBuilder.Entity<ConnectionRow>(entity =>
        {
            entity.ToView("Vw_Connection");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.Ip).HasColumnName("ip");
            entity.Property(x => x.Port).HasColumnName("port");
            entity.Property(x => x.PasswordCipher).HasColumnName("password_cipher");
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.StatusText).HasColumnName("status_text");
            entity.Property(x => x.IsAbnormal).HasColumnName("is_abnormal");
            entity.Property(x => x.LastTestedAt).HasColumnName("last_tested_at");
            entity.Property(x => x.LastTestedDisplay).HasColumnName("last_tested_display");
            entity.Property(x => x.LastTestMessage).HasColumnName("last_test_message");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.GroupName).HasColumnName("group_name");
            entity.Property(x => x.GroupDisplayName).HasColumnName("group_display_name");
        });

        modelBuilder.Entity<ConnectionGroupRow>(entity =>
        {
            entity.ToView("Vw_ConnectionGroup");
            entity.Property(x => x.Name).HasColumnName("name");
        });

        modelBuilder.Entity<EventLogRow>(entity =>
        {
            entity.ToView("Vw_EventLog");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OccurredUtcTicks).HasColumnName("occurred_utc_ticks");
            entity.Property(x => x.Level).HasColumnName("level");
            entity.Property(x => x.LevelText).HasColumnName("level_text");
            entity.Property(x => x.OccurredDisplay).HasColumnName("occurred_display");
            entity.Property(x => x.Source).HasColumnName("source");
            entity.Property(x => x.Message).HasColumnName("message");
            entity.Property(x => x.Detail).HasColumnName("detail");
            entity.Property(x => x.HasDetail).HasColumnName("has_detail");
            entity.Property(x => x.RelatedConnectionId).HasColumnName("related_connection_id");
        });

        modelBuilder.Entity<ConnectionRecentEventRow>(entity =>
        {
            entity.ToView("Vw_ConnectionRecentEvent");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OccurredUtcTicks).HasColumnName("occurred_utc_ticks");
            entity.Property(x => x.Level).HasColumnName("level");
            entity.Property(x => x.LevelText).HasColumnName("level_text");
            entity.Property(x => x.OccurredDisplay).HasColumnName("occurred_display");
            entity.Property(x => x.Source).HasColumnName("source");
            entity.Property(x => x.Message).HasColumnName("message");
            entity.Property(x => x.Detail).HasColumnName("detail");
            entity.Property(x => x.HasDetail).HasColumnName("has_detail");
            entity.Property(x => x.RelatedConnectionId).HasColumnName("related_connection_id");
        });

        modelBuilder.Entity<DeploymentHistoryRow>(entity =>
        {
            entity.ToView("Vw_DeploymentHistory");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.DeployedTicks).HasColumnName("deployed_ticks");
            entity.Property(x => x.DeployedDisplay).HasColumnName("deployed_display");
            entity.Property(x => x.ConnectionId).HasColumnName("connection_id");
            entity.Property(x => x.TargetName).HasColumnName("target_name");
            entity.Property(x => x.TargetIp).HasColumnName("target_ip");
            entity.Property(x => x.LocalPath).HasColumnName("local_path");
            entity.Property(x => x.RemotePath).HasColumnName("remote_path");
            entity.Property(x => x.Success).HasColumnName("success");
            entity.Property(x => x.SuccessText).HasColumnName("success_text");
            entity.Property(x => x.Message).HasColumnName("message");
            entity.Property(x => x.OperatedBy).HasColumnName("operated_by");
        });
    }
}
