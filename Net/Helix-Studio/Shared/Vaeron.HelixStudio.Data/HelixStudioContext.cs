using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.Data.Model;

namespace Vaeron.HelixStudio.Data;

public partial class HelixStudioContext : DbContext
{
    public HelixStudioContext(DbContextOptions<HelixStudioContext> options)
        : base(options)
    {
    }

    public virtual DbSet<EditState> EditStates { get; set; }

    public virtual DbSet<EtwSource> EtwSources { get; set; }

    public virtual DbSet<FieldDefinition> FieldDefinitions { get; set; }

    public virtual DbSet<HeatBand> HeatBands { get; set; }

    public virtual DbSet<MachineInfo> MachineInfos { get; set; }

    public virtual DbSet<MachineMetric> MachineMetrics { get; set; }

    public virtual DbSet<MetricSource> MetricSources { get; set; }

    public virtual DbSet<PerfcounterSource> PerfcounterSources { get; set; }

    public virtual DbSet<ProcessSeen> ProcessSeens { get; set; }

    public virtual DbSet<ProviderStatus> ProviderStatuses { get; set; }

    public virtual DbSet<QuotaDraft> QuotaDrafts { get; set; }

    public virtual DbSet<Reading> Readings { get; set; }

    public virtual DbSet<Sampling> Samplings { get; set; }

    public virtual DbSet<Software> Softwares { get; set; }

    public virtual DbSet<SoftwareValue> SoftwareValues { get; set; }

    public virtual DbSet<SortColumn> SortColumns { get; set; }

    public virtual DbSet<SortDirection> SortDirections { get; set; }

    public virtual DbSet<StateFilter> StateFilters { get; set; }

    public virtual DbSet<StatePresentation> StatePresentations { get; set; }

    public virtual DbSet<TextTemplate> TextTemplates { get; set; }

    public virtual DbSet<ToggleAction> ToggleActions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EditState>(entity =>
        {
            entity.HasKey(e => new { e.Editable, e.Dirty, e.Present });

            entity.ToTable("edit_state");

            entity.Property(e => e.Editable).HasColumnName("editable");
            entity.Property(e => e.Dirty).HasColumnName("dirty");
            entity.Property(e => e.Present).HasColumnName("present");
            entity.Property(e => e.IconTitle).HasColumnName("icon_title");
            entity.Property(e => e.Placeholder).HasColumnName("placeholder");
            entity.Property(e => e.State).HasColumnName("state");
        });

        modelBuilder.Entity<EtwSource>(entity =>
        {
            entity.HasKey(e => e.MetricKey);

            entity.ToTable("etw_source");

            entity.Property(e => e.MetricKey).HasColumnName("metric_key");
            entity.Property(e => e.ProviderGuid).HasColumnName("provider_guid");
            entity.Property(e => e.ProviderName).HasColumnName("provider_name");

            entity.HasOne(d => d.MetricKeyNavigation).WithOne(p => p.EtwSource)
                .HasForeignKey<EtwSource>(d => d.MetricKey)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<FieldDefinition>(entity =>
        {
            entity.HasKey(e => e.Key);

            entity.ToTable("field_definition");

            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Editable).HasColumnName("editable");
            entity.Property(e => e.Label).HasColumnName("label");
            entity.Property(e => e.Max).HasColumnName("max");
            entity.Property(e => e.Min).HasColumnName("min");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.Position).HasColumnName("position");
            entity.Property(e => e.Unit).HasColumnName("unit");
        });

        modelBuilder.Entity<HeatBand>(entity =>
        {
            entity.HasKey(e => new { e.Metric, e.Threshold });

            entity.ToTable("heat_band");

            entity.Property(e => e.Metric).HasColumnName("metric");
            entity.Property(e => e.Threshold).HasColumnName("threshold");
            entity.Property(e => e.Level).HasColumnName("level");
        });

        modelBuilder.Entity<MachineInfo>(entity =>
        {
            entity.HasKey(e => e.Key);

            entity.ToTable("machine_info");

            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Value).HasColumnName("value");
        });

        modelBuilder.Entity<MachineMetric>(entity =>
        {
            entity.HasKey(e => e.MetricKey);

            entity.ToTable("machine_metric");

            entity.Property(e => e.MetricKey).HasColumnName("metric_key");
            entity.Property(e => e.Instance).HasColumnName("instance");

            entity.HasOne(d => d.MetricKeyNavigation).WithOne(p => p.MachineMetric)
                .HasForeignKey<MachineMetric>(d => d.MetricKey)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<MetricSource>(entity =>
        {
            entity.HasKey(e => e.MetricKey);

            entity.ToTable("metric_source");

            entity.Property(e => e.MetricKey).HasColumnName("metric_key");
            entity.Property(e => e.Divisor).HasColumnName("divisor");
            entity.Property(e => e.Provider).HasColumnName("provider");
        });

        modelBuilder.Entity<PerfcounterSource>(entity =>
        {
            entity.HasKey(e => e.MetricKey);

            entity.ToTable("perfcounter_source");

            entity.Property(e => e.MetricKey).HasColumnName("metric_key");
            entity.Property(e => e.Category).HasColumnName("category");
            entity.Property(e => e.Counter).HasColumnName("counter");
            entity.Property(e => e.Kind).HasColumnName("kind");
            entity.Property(e => e.MatchKind).HasColumnName("match_kind");

            entity.HasOne(d => d.MetricKeyNavigation).WithOne(p => p.PerfcounterSource)
                .HasForeignKey<PerfcounterSource>(d => d.MetricKey)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ProcessSeen>(entity =>
        {
            entity.ToTable("process_seen");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CompanyName).HasColumnName("company_name");
            entity.Property(e => e.ExePath).HasColumnName("exe_path");
            entity.Property(e => e.FileVersion).HasColumnName("file_version");
            entity.Property(e => e.HasWindow).HasColumnName("has_window");
            entity.Property(e => e.IsCritical).HasColumnName("is_critical");
            entity.Property(e => e.ProductName).HasColumnName("product_name");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
        });

        modelBuilder.Entity<ProviderStatus>(entity =>
        {
            entity.HasKey(e => e.Provider);

            entity.ToTable("provider_status");

            entity.Property(e => e.Provider).HasColumnName("provider");
            entity.Property(e => e.Available).HasColumnName("available");
            entity.Property(e => e.Reason).HasColumnName("reason");
        });

        modelBuilder.Entity<QuotaDraft>(entity =>
        {
            entity.HasKey(e => new { e.SoftwareId, e.FieldKey });

            entity.ToTable("quota_draft");

            entity.Property(e => e.SoftwareId).HasColumnName("software_id");
            entity.Property(e => e.FieldKey).HasColumnName("field_key");
            entity.Property(e => e.Value).HasColumnName("value");

            entity.HasOne(d => d.FieldKeyNavigation).WithMany(p => p.QuotaDrafts)
                .HasForeignKey(d => d.FieldKey)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Software).WithMany(p => p.QuotaDrafts)
                .HasForeignKey(d => d.SoftwareId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Reading>(entity =>
        {
            entity.HasKey(e => new { e.ScopeId, e.MetricKey });

            entity.ToTable("reading");

            entity.Property(e => e.ScopeId).HasColumnName("scope_id");
            entity.Property(e => e.MetricKey).HasColumnName("metric_key");
            entity.Property(e => e.Value).HasColumnName("value");
        });

        modelBuilder.Entity<Sampling>(entity =>
        {
            entity.HasKey(e => e.Key);

            entity.ToTable("sampling");

            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Value).HasColumnName("value");
        });

        modelBuilder.Entity<Software>(entity =>
        {
            entity.ToTable("software");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CompanyName).HasColumnName("company_name");
            entity.Property(e => e.ExePath).HasColumnName("exe_path");
            entity.Property(e => e.FileVersion).HasColumnName("file_version");
            entity.Property(e => e.HasWindow).HasColumnName("has_window");
            entity.Property(e => e.IsCritical).HasColumnName("is_critical");
            entity.Property(e => e.ProductName).HasColumnName("product_name");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.State).HasColumnName("state");

            entity.HasOne(d => d.StateNavigation).WithMany(p => p.Softwares)
                .HasForeignKey(d => d.State)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<SoftwareValue>(entity =>
        {
            entity.HasKey(e => new { e.SoftwareId, e.FieldKey });

            entity.ToTable("software_value");

            entity.Property(e => e.SoftwareId).HasColumnName("software_id");
            entity.Property(e => e.FieldKey).HasColumnName("field_key");
            entity.Property(e => e.Value).HasColumnName("value");

            entity.HasOne(d => d.FieldKeyNavigation).WithMany(p => p.SoftwareValues)
                .HasForeignKey(d => d.FieldKey)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Software).WithMany(p => p.SoftwareValues)
                .HasForeignKey(d => d.SoftwareId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<SortColumn>(entity =>
        {
            entity.HasKey(e => e.Key);

            entity.ToTable("sort_column");

            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Alignment).HasColumnName("alignment");
            entity.Property(e => e.DefaultDirection).HasColumnName("default_direction");
            entity.Property(e => e.Label).HasColumnName("label");
            entity.Property(e => e.Position).HasColumnName("position");
            entity.Property(e => e.Width).HasColumnName("width");

            entity.HasOne(d => d.DefaultDirectionNavigation).WithMany(p => p.SortColumns)
                .HasForeignKey(d => d.DefaultDirection)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<SortDirection>(entity =>
        {
            entity.HasKey(e => e.Name);

            entity.ToTable("sort_direction");

            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Arrow).HasColumnName("arrow");
            entity.Property(e => e.Opposite).HasColumnName("opposite");
            entity.Property(e => e.Sign).HasColumnName("sign");
        });

        modelBuilder.Entity<StateFilter>(entity =>
        {
            entity.HasKey(e => e.Key);

            entity.ToTable("state_filter");

            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Highlighted).HasColumnName("highlighted");
            entity.Property(e => e.Label).HasColumnName("label");
            entity.Property(e => e.NextKey).HasColumnName("next_key");

            entity.HasMany(d => d.States).WithMany(p => p.FilterKeys)
                .UsingEntity<Dictionary<string, object>>(
                    "StateFilterState",
                    r => r.HasOne<StatePresentation>().WithMany()
                        .HasForeignKey("State")
                        .OnDelete(DeleteBehavior.ClientSetNull),
                    l => l.HasOne<StateFilter>().WithMany()
                        .HasForeignKey("FilterKey")
                        .OnDelete(DeleteBehavior.ClientSetNull),
                    j =>
                    {
                        j.HasKey("FilterKey", "State");
                        j.ToTable("state_filter_state");
                        j.IndexerProperty<string>("FilterKey").HasColumnName("filter_key");
                        j.IndexerProperty<string>("State").HasColumnName("state");
                    });
        });

        modelBuilder.Entity<StatePresentation>(entity =>
        {
            entity.HasKey(e => e.State);

            entity.ToTable("state_presentation");

            entity.Property(e => e.State).HasColumnName("state");
            entity.Property(e => e.Label).HasColumnName("label");
            entity.Property(e => e.LogLevel).HasColumnName("log_level");
            entity.Property(e => e.Rank).HasColumnName("rank");
            entity.Property(e => e.ShowsReading).HasColumnName("shows_reading");
        });

        modelBuilder.Entity<TextTemplate>(entity =>
        {
            entity.HasKey(e => e.Key);

            entity.ToTable("text_template");

            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Value).HasColumnName("value");
        });

        modelBuilder.Entity<ToggleAction>(entity =>
        {
            entity.HasKey(e => new { e.State, e.IsCritical });

            entity.ToTable("toggle_action");

            entity.Property(e => e.State).HasColumnName("state");
            entity.Property(e => e.IsCritical).HasColumnName("is_critical");
            entity.Property(e => e.Action).HasColumnName("action");
            entity.Property(e => e.Enabled).HasColumnName("enabled");
            entity.Property(e => e.Icon).HasColumnName("icon");
            entity.Property(e => e.Label).HasColumnName("label");
            entity.Property(e => e.Title).HasColumnName("title");

            entity.HasOne(d => d.StateNavigation).WithMany(p => p.ToggleActions)
                .HasForeignKey(d => d.State)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
