using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.Data;
using Vaeron.HelixStudio.Data.Model;
using Vaeron.HelixStudio.Sampling.Model;

namespace Vaeron.HelixStudio.Sampling.Internal;

/// <summary>資料存取邊界。寫觀測、併入軟體清單、取工作清單、寫讀數。</summary>
/// <remarks>
/// 誰是新軟體、誰在跑、哪些讀數該作廢，全部是集合運算，專案端不逐列判斷。
/// 每輪動到上百列，故清空與整欄覆寫走 ExecuteDelete／ExecuteUpdate，不逐列載進變更追蹤器。
/// </remarks>
internal sealed class SamplingStore(IDbContextFactory<HelixStudioContext> contexts)
{
    /// <summary>路徑與版本已經有值的軟體不必每輪重讀，開檔讀版本資源是每輪最貴的一項。</summary>
    public IReadOnlySet<string> KnownIds()
    {
        using var context = contexts.CreateDbContext();
        return context.Softwares
            .Where(item => item.ExePath != null)
            .Select(item => item.Id)
            .ToHashSet();
    }

    /// <summary>一輪觀測寫入並併入清單。整批在同一個交易裡，畫面不會讀到只完成一半的清單。</summary>
    public void Merge(IEnumerable<DiscoveredProcess> processes)
    {
        var seen = processes
            .GroupBy(item => item.Id)
            .Select(group => new ProcessSeen
            {
                Id = group.Key,
                ProductName = First(group.Select(item => item.ProductName)),
                CompanyName = First(group.Select(item => item.CompanyName)),
                FileVersion = First(group.Select(item => item.FileVersion)),
                ExePath = First(group.Select(item => item.ExePath)),
                HasWindow = group.Max(item => item.HasWindow),
                SessionId = group.Min(item => item.SessionId),
                IsCritical = group.Max(item => item.IsCritical)
            })
            .ToList();

        using var context = contexts.CreateDbContext();
        using var transaction = context.Database.BeginTransaction();

        context.ProcessSeens.ExecuteDelete();
        context.ProcessSeens.AddRange(seen);
        context.SaveChanges();

        var ids = seen.Select(item => item.Id).ToList();

        context.Softwares
            .Where(item => ids.Contains(item.Id) == false)
            .ExecuteUpdate(item => item.SetProperty(row => row.State, "stopped"));

        AddNew(context, seen);
        Refresh(context, seen);

        transaction.Commit();
    }

    public IReadOnlyList<SamplingTarget> Targets()
    {
        using var context = contexts.CreateDbContext();
        return context.Set<VSamplingTarget>()
            .AsNoTracking()
            .Select(target => new SamplingTarget(
                target.ScopeId!,
                target.MetricKey!,
                target.Provider!,
                target.Divisor!.Value,
                target.Instance!,
                target.Category!,
                target.Counter!,
                target.Kind!,
                target.MatchKind!,
                target.EtwProvider!,
                target.EtwGuid!))
            .ToList();
    }

    public int IntervalSeconds()
    {
        using var context = contexts.CreateDbContext();
        return context.Samplings
            .Where(row => row.Key == "interval_seconds")
            .Select(row => Convert.ToInt32(row.Value))
            .Single();
    }

    public void SetProvider(string provider, int available, string reason)
    {
        using var context = contexts.CreateDbContext();
        context.ProviderStatuses
            .Where(row => row.Provider == provider)
            .ExecuteUpdate(row => row
                .SetProperty(item => item.Available, available)
                .SetProperty(item => item.Reason, reason));
    }

    public void SetCores(int cores)
    {
        using var context = contexts.CreateDbContext();
        context.MachineInfos
            .Where(row => row.Key == "logical_processors")
            .ExecuteUpdate(row => row.SetProperty(item => item.Value, cores.ToString()));
    }

    /// <summary>整輪讀數在同一個交易裡換掉，畫面不會讀到只更新一半的一輪。</summary>
    public void WriteReadings(IEnumerable<SampleValue> samples)
    {
        var rows = samples
            .Select(sample => new Reading
            {
                ScopeId = sample.ScopeId,
                MetricKey = sample.MetricKey,
                Value = sample.Value
            })
            .ToList();

        using var context = contexts.CreateDbContext();
        using var transaction = context.Database.BeginTransaction();

        context.Readings.ExecuteDelete();
        context.Readings.AddRange(rows);
        context.SaveChanges();

        transaction.Commit();
    }

    public void Stamp(string stamp)
    {
        using var context = contexts.CreateDbContext();
        context.Samplings
            .Where(row => row.Key == "sampled_at")
            .ExecuteUpdate(row => row.SetProperty(item => item.Value, stamp));
    }

    /// <summary>本輪新出現的行程成為新的一列。</summary>
    private static void AddNew(HelixStudioContext context, IReadOnlyList<ProcessSeen> seen)
    {
        var existing = context.Softwares.Select(item => item.Id).ToHashSet();

        context.Softwares.AddRange(seen
            .Where(item => existing.Contains(item.Id) == false)
            .Select(item => new Software
            {
                Id = item.Id,
                HasWindow = item.HasWindow,
                SessionId = item.SessionId,
                IsCritical = item.IsCritical,
                State = "running"
            }));

        context.SaveChanges();
    }

    /// <summary>本輪見到的軟體以觀測值覆蓋。這輪沒讀的欄位為 NULL，保留既有值。</summary>
    private static void Refresh(HelixStudioContext context, IReadOnlyList<ProcessSeen> seen)
    {
        var observed = seen.ToDictionary(item => item.Id);
        var rows = context.Softwares.Where(item => observed.Keys.Contains(item.Id)).ToList();

        foreach (var row in rows)
        {
            var fresh = observed.Where(pair => pair.Key == row.Id).Select(pair => pair.Value).Single();

            row.ProductName = fresh.ProductName ?? row.ProductName;
            row.CompanyName = fresh.CompanyName ?? row.CompanyName;
            row.FileVersion = fresh.FileVersion ?? row.FileVersion;
            row.ExePath = fresh.ExePath ?? row.ExePath;
            row.HasWindow = fresh.HasWindow;
            row.SessionId = fresh.SessionId;
            row.IsCritical = fresh.IsCritical;
            row.State = "running";
        }

        context.SaveChanges();
    }

    /// <summary>取第一個有內容的觀測值。全部為空字串時為 NULL，畫面顯示 N/A。</summary>
    private static string? First(IEnumerable<string> candidates) =>
        candidates.Where(value => value.Length > 0).FirstOrDefault();
}
