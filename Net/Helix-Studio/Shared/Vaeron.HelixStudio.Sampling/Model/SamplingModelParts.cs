using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.Data;

namespace Vaeron.HelixStudio.Sampling.Model;

/// <summary>採集組件用到的 view。</summary>
/// <remarks>內容由映射工具自資料庫產生，不手寫欄位對應。</remarks>
internal sealed class SamplingModelParts : IModelParts
{
    public void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VSamplingTarget>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_sampling_target");

            entity.Property(e => e.Category).HasColumnName("category");
            entity.Property(e => e.Counter).HasColumnName("counter");
            entity.Property(e => e.Divisor).HasColumnName("divisor");
            entity.Property(e => e.EtwGuid).HasColumnName("etw_guid");
            entity.Property(e => e.EtwProvider).HasColumnName("etw_provider");
            entity.Property(e => e.Instance).HasColumnName("instance");
            entity.Property(e => e.Kind).HasColumnName("kind");
            entity.Property(e => e.MatchKind).HasColumnName("match_kind");
            entity.Property(e => e.MetricKey).HasColumnName("metric_key");
            entity.Property(e => e.Provider).HasColumnName("provider");
            entity.Property(e => e.ScopeId).HasColumnName("scope_id");
        });
    }
}