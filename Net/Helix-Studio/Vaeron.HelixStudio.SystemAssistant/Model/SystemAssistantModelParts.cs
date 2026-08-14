using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.Data;

namespace Vaeron.HelixStudio.SystemAssistant.Model;

/// <summary>系統助理用到的 view。</summary>
/// <remarks>內容由映射工具自資料庫產生，不手寫欄位對應。</remarks>
internal sealed class SystemAssistantModelParts : IModelParts
{
    public void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VMachineSummary>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_machine_summary");

            entity.Property(e => e.CpuText).HasColumnName("cpu_text");
            entity.Property(e => e.NetText).HasColumnName("net_text");
            entity.Property(e => e.RamText).HasColumnName("ram_text");
        });

        modelBuilder.Entity<VSoftwareField>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_software_field");

            entity.Property(e => e.DisplayValue).HasColumnName("display_value");
            entity.Property(e => e.DraftValue).HasColumnName("draft_value");
            entity.Property(e => e.EditState).HasColumnName("edit_state");
            entity.Property(e => e.Editable).HasColumnName("editable");
            entity.Property(e => e.FieldKey).HasColumnName("field_key");
            entity.Property(e => e.IconTitle).HasColumnName("icon_title");
            entity.Property(e => e.Label).HasColumnName("label");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.Placeholder).HasColumnName("placeholder");
            entity.Property(e => e.Position).HasColumnName("position");
            entity.Property(e => e.SoftwareId).HasColumnName("software_id");
            entity.Property(e => e.StoredValue).HasColumnName("stored_value");
            entity.Property(e => e.Unit).HasColumnName("unit");
        });

        modelBuilder.Entity<VSoftwareLabel>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_software_label");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Publisher).HasColumnName("publisher");
        });

        modelBuilder.Entity<VSoftwareRow>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_software_row");

            entity.Property(e => e.ChangedCount)
                .HasColumnType("INT")
                .HasColumnName("changed_count");
            entity.Property(e => e.CpuLevel).HasColumnName("cpu_level");
            entity.Property(e => e.CpuPct).HasColumnName("cpu_pct");
            entity.Property(e => e.CpuText).HasColumnName("cpu_text");
            entity.Property(e => e.DiskMbs).HasColumnName("disk_mbs");
            entity.Property(e => e.DiskText).HasColumnName("disk_text");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.LogLevel).HasColumnName("log_level");
            entity.Property(e => e.MetaText).HasColumnName("meta_text");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.NetLevel).HasColumnName("net_level");
            entity.Property(e => e.NetMbps).HasColumnName("net_mbps");
            entity.Property(e => e.NetText).HasColumnName("net_text");
            entity.Property(e => e.Publisher).HasColumnName("publisher");
            entity.Property(e => e.RamMb).HasColumnName("ram_mb");
            entity.Property(e => e.RamText).HasColumnName("ram_text");
            entity.Property(e => e.State).HasColumnName("state");
            entity.Property(e => e.StateLabel).HasColumnName("state_label");
            entity.Property(e => e.ToggleEnabled)
                .HasColumnType("INT")
                .HasColumnName("toggle_enabled");
            entity.Property(e => e.ToggleIcon).HasColumnName("toggle_icon");
            entity.Property(e => e.ToggleLabel).HasColumnName("toggle_label");
            entity.Property(e => e.ToggleTitle).HasColumnName("toggle_title");
        });

        modelBuilder.Entity<VSoftwareSort>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_software_sort");

            entity.Property(e => e.ColumnKey).HasColumnName("column_key");
            entity.Property(e => e.SoftwareId).HasColumnName("software_id");
            entity.Property(e => e.SortValue).HasColumnName("sort_value");
        });

        modelBuilder.Entity<VSoftwareSystemValue>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_software_system_value");

            entity.Property(e => e.FieldKey).HasColumnName("field_key");
            entity.Property(e => e.SoftwareId).HasColumnName("software_id");
            entity.Property(e => e.Value).HasColumnName("value");
        });

        modelBuilder.Entity<VToggleAction>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_toggle_action");

            entity.Property(e => e.Action).HasColumnName("action");
            entity.Property(e => e.ExePath).HasColumnName("exe_path");
            entity.Property(e => e.Label).HasColumnName("label");
            entity.Property(e => e.LogLevel).HasColumnName("log_level");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.SoftwareId).HasColumnName("software_id");
        });
    }
}