using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.Data;
using Vaeron.HelixStudio.Data.Model;
using Vaeron.HelixStudio.SystemAssistant.Model;

namespace Vaeron.HelixStudio.SystemAssistant.Internal;

/// <summary>資料存取邊界。只送查詢與收列，過濾、排序、值域對映、字串組裝全部在資料庫。</summary>
/// <remarks>查詢一律為 LINQ；view 已把每一列該顯示的字樣算好，這裡不再加工。</remarks>
internal sealed class SystemAssistantStore(IDbContextFactory<HelixStudioContext> contexts)
{
    // 表頭以 position 倒序取出：畫面由右往左停靠，位置最小的「軟體」欄最後停靠並填滿剩餘寬度。
    public IReadOnlyList<ColumnView> Columns(SystemAssistantQuery query)
    {
        using var context = contexts.CreateDbContext();

        var current = context.SortDirections.Single(row => row.Name == query.SortDirection);

        return context.SortColumns
            .AsNoTracking()
            .OrderByDescending(column => column.Position)
            .Select(column => new
            {
                column.Key,
                column.Label,
                column.Alignment,
                column.Width,
                column.DefaultDirection
            })
            .ToList()
            .Select(column => new ColumnView(
                column.Key,
                column.Label,
                column.Alignment,
                column.Width ?? 0,
                column.Width == null,
                column.Key == query.SortKey,
                Arrow(column.Key, query.SortKey, current.Arrow),
                Next(column.Key, query.SortKey, current.Opposite, column.DefaultDirection)))
            .ToList();
    }

    public IReadOnlyList<SoftwareRowView> Rows(SystemAssistantQuery query)
    {
        using var context = contexts.CreateDbContext();
        var sign = context.SortDirections.Single(row => row.Name == query.SortDirection).Sign;

        return Visible(context, query)
            .Join(context.Set<VSoftwareSort>().Where(sort => sort.ColumnKey == query.SortKey),
                  row => row.Id, sort => sort.SoftwareId, (row, sort) => new { row, sort.SortValue })
            .OrderBy(pair => pair.SortValue * sign)
            .Select(pair => new SoftwareRowView(
                pair.row.Id!,
                pair.row.Name!,
                pair.row.Publisher!,
                pair.row.State!,
                pair.row.StateLabel!,
                pair.row.CpuText!,
                pair.row.CpuLevel!,
                pair.row.RamText!,
                pair.row.NetText!,
                pair.row.NetLevel!,
                pair.row.DiskText!,
                pair.row.ToggleLabel!,
                pair.row.ToggleTitle!,
                pair.row.ToggleEnabled == 1,
                pair.row.ChangedCount!.Value,
                pair.row.ChangedCount > 0,
                pair.row.Id == query.ActiveId))
            .ToList();
    }

    public IReadOnlyList<FieldRowView> Fields(SystemAssistantQuery query)
    {
        using var context = contexts.CreateDbContext();

        return Visible(context, query)
            .Where(row => row.Id == query.ActiveId)
            .Join(context.Set<VSoftwareField>(),
                  row => row.Id, field => field.SoftwareId, (row, field) => field)
            .OrderBy(field => field.Position)
            .Select(field => new FieldRowView(
                field.SoftwareId!,
                field.FieldKey!,
                field.Label!,
                field.Note!,
                field.Unit!,
                field.DisplayValue!,
                field.Placeholder!,
                field.Editable == 0,
                field.EditState!,
                field.IconTitle!))
            .ToList();
    }

    /// <summary>摘要的 CPU、記憶體、網路是 Windows 量出來的機器用量，不是把清單各列加起來。</summary>
    public SummaryView Summary(SystemAssistantQuery query, string sampledAt)
    {
        using var context = contexts.CreateDbContext();

        var filter = context.StateFilters.Single(row => row.Key == query.FilterKey);
        var machine = context.Set<VMachineSummary>().AsNoTracking().Single();
        var template = context.TextTemplates.Single(row => row.Key == "summary").Value;
        var count = Visible(context, query).Count();

        var active = Visible(context, query)
            .Where(row => row.Id == query.ActiveId)
            .Select(row => new
            {
                row.Name,
                row.MetaText,
                row.ToggleLabel,
                row.ToggleIcon,
                row.ChangedCount
            })
            .ToList();

        return new SummaryView(
            string.Format(template, count, machine.CpuText, machine.RamText, machine.NetText, sampledAt),
            filter.Label,
            filter.NextKey,
            filter.Highlighted == 1,
            string.Concat(active.Select(row => row.Name)),
            string.Concat(active.Select(row => row.MetaText)),
            string.Concat(active.Select(row => row.ToggleLabel)),
            string.Concat(active.Select(row => row.ToggleIcon)),
            active.Count > 0,
            active.Select(row => row.ChangedCount!.Value).Sum(),
            active.Select(row => row.ChangedCount).Sum() > 0);
    }

    /// <summary>查得到 0 或 1 列。查不到者沒有東西可執行。</summary>
    public IReadOnlyList<ToggleTarget> ToggleTargets(string softwareId)
    {
        using var context = contexts.CreateDbContext();

        return context.Set<VToggleAction>()
            .AsNoTracking()
            .Where(row => row.SoftwareId == softwareId)
            .Select(row => new ToggleTarget(
                row.SoftwareId!,
                row.Action!,
                row.Name!,
                row.LogLevel!,
                row.Label!,
                row.ExePath ?? ""))
            .ToList();
    }

    public void SaveDraft(string softwareId, string fieldKey, string value)
    {
        using var context = contexts.CreateDbContext();

        var existing = context.QuotaDrafts
            .Where(row => row.SoftwareId == softwareId && row.FieldKey == fieldKey)
            .ToList();

        foreach (var row in existing)
        {
            row.Value = value;
        }

        context.QuotaDrafts.AddRange(Enumerable.Repeat(0, 1)
            .Where(_ => existing.Count == 0)
            .Select(_ => new QuotaDraft { SoftwareId = softwareId, FieldKey = fieldKey, Value = value }));

        context.SaveChanges();
    }

    public void ClearDrafts(string softwareId)
    {
        using var context = contexts.CreateDbContext();
        context.QuotaDrafts.Where(row => row.SoftwareId == softwareId).ExecuteDelete();
    }

    /// <summary>探索出來的軟體一開始沒有設定值，套用時該列還不存在，故為新增或覆寫。</summary>
    public void ApplyDrafts(string softwareId)
    {
        using var context = contexts.CreateDbContext();
        using var transaction = context.Database.BeginTransaction();

        var drafts = context.QuotaDrafts.Where(row => row.SoftwareId == softwareId).ToList();
        var keys = drafts.Select(row => row.FieldKey).ToList();

        var stored = context.SoftwareValues
            .Where(row => row.SoftwareId == softwareId && keys.Contains(row.FieldKey))
            .ToList();

        foreach (var row in stored)
        {
            row.Value = drafts.Where(draft => draft.FieldKey == row.FieldKey)
                              .Select(draft => draft.Value)
                              .Single();
        }

        var present = stored.Select(row => row.FieldKey).ToHashSet();

        context.SoftwareValues.AddRange(drafts
            .Where(draft => present.Contains(draft.FieldKey) == false)
            .Select(draft => new SoftwareValue
            {
                SoftwareId = softwareId,
                FieldKey = draft.FieldKey,
                Value = draft.Value
            }));

        context.SaveChanges();
        transaction.Commit();
    }

    /// <summary>目前過濾條件與搜尋字下看得到的列。</summary>
    private static IQueryable<VSoftwareRow> Visible(HelixStudioContext context, SystemAssistantQuery query)
    {
        var states = context.StateFilters
            .Where(filter => filter.Key == query.FilterKey)
            .SelectMany(filter => filter.States.Select(state => state.State));

        return context.Set<VSoftwareRow>()
            .AsNoTracking()
            .Where(row => states.Contains(row.State!))
            .Where(row => EF.Functions.Like(row.Name! + row.Publisher!, "%" + query.Search + "%"));
    }

    private static string Arrow(string key, string sortKey, string arrow) =>
        string.Concat(Enumerable.Repeat(arrow, 1).Where(_ => key == sortKey));

    private static string Next(string key, string sortKey, string opposite, string fallback) =>
        string.Concat(Enumerable.Repeat(opposite, 1).Where(_ => key == sortKey))
            + string.Concat(Enumerable.Repeat(fallback, 1).Where(_ => key != sortKey));
}
