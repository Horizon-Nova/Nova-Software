using CommunityToolkit.Mvvm.ComponentModel;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.Dashboard;

/// <summary>批次部署任務的彙總列。逐台明細已在主表格各列呈現，這裡只記彙總與失敗清單。</summary>
public sealed partial class DeployTaskRow : ObservableObject
{
    private static readonly DeployItemState[] TerminalStates =
        [DeployItemState.Success, DeployItemState.Failed, DeployItemState.Cancelled];

    private readonly Dictionary<int, DeployTargetProgress> _items;

    [ObservableProperty]
    private int _doneCount;

    [ObservableProperty]
    private int _successCount;

    [ObservableProperty]
    private int _failedCount;

    [ObservableProperty]
    private int _cancelledCount;

    internal DeployTaskRow(
        DeploySubmissionResult submission,
        string localSourcePath,
        string remoteTargetPath,
        DateTimeOffset? scheduledAt,
        DateTimeOffset submittedAt,
        DeployItemState initialState)
    {
        TaskId = submission.TaskId;
        Label = submission.Label;
        LocalSourcePath = localSourcePath;
        RemoteTargetPath = remoteTargetPath;
        ScheduledAt = scheduledAt;
        SubmittedAt = submittedAt;
        _items = submission.Targets.ToDictionary(
            target => target.ConnectionId,
            target => new DeployTargetProgress(target.ConnectionName, initialState));

        ApplyAggregate();
    }

    public string TaskId { get; }
    public string Label { get; }
    public string LocalSourcePath { get; }
    public string RemoteTargetPath { get; }
    public DateTimeOffset? ScheduledAt { get; }
    public DateTimeOffset SubmittedAt { get; }

    public string SubmittedAtDisplay => SubmittedAt.ToLocalTime().ToString("MM-dd HH:mm");
    public string PathDisplay => $"{LocalSourcePath} → {RemoteTargetPath}";
    public string AllTargetNamesText => string.Join("、", _items.Values.Select(item => item.Name));
    public int Total => _items.Count;

    public bool AllTerminal => DoneCount == Total;
    public bool IsCancelable => AllTerminal is false;
    public bool HasFailedItems => FailedCount > 0;

    public IReadOnlyList<int> FailedConnectionIds => _items
        .Where(entry => entry.Value.State == DeployItemState.Failed)
        .Select(entry => entry.Key)
        .ToList();

    public string FailedConnectionNamesText => string.Join("、", _items.Values
        .Where(item => item.State == DeployItemState.Failed)
        .Select(item => item.Name));

    /// <summary>任務層級的一句話。逐條規則依序比對，第一條成立的即為結果。</summary>
    public string SummaryText
    {
        get
        {
            var rules = new (Func<bool> Matches, Func<string> Text)[]
            {
                (() => Total > 0 && _items.Values.All(item => item.State == DeployItemState.Scheduled),
                 () => $"排程於 {ScheduledAt!.Value.ToLocalTime():MM-dd HH:mm} 派發"),
                (() => AllTerminal is false, () => $"執行中 {DoneCount}/{Total}"),
                (() => FailedCount > 0, () => $"完成 · 成功 {SuccessCount}、失敗 {FailedCount}"),
                (() => CancelledCount > 0, () => $"完成 · 成功 {SuccessCount}、取消 {CancelledCount}")
            };

            return rules
                .Where(rule => rule.Matches())
                .Select(rule => rule.Text())
                .DefaultIfEmpty($"全部成功 {SuccessCount}/{Total}")
                .First();
        }
    }

    /// <summary>回報這個任務下某台機器的最新狀態並重算彙總。不屬於本任務時什麼也不做。</summary>
    public void ApplyItemState(int connectionId, DeployItemState state)
    {
        var existing = _items.GetValueOrDefault(connectionId);

        foreach (var item in new[] { existing }.OfType<DeployTargetProgress>())
        {
            _items[connectionId] = item with { State = state };
            ApplyAggregate();
        }
    }

    private void ApplyAggregate()
    {
        DoneCount = _items.Values.Count(item => TerminalStates.Contains(item.State));
        SuccessCount = _items.Values.Count(item => item.State == DeployItemState.Success);
        FailedCount = _items.Values.Count(item => item.State == DeployItemState.Failed);
        CancelledCount = _items.Values.Count(item => item.State == DeployItemState.Cancelled);

        var derived = new[]
        {
            nameof(AllTerminal), nameof(IsCancelable), nameof(HasFailedItems),
            nameof(SummaryText), nameof(FailedConnectionNamesText)
        };

        foreach (var name in derived)
        {
            OnPropertyChanged(name);
        }
    }
}

/// <summary>任務裡某一台機器目前的名稱與狀態。</summary>
internal sealed record DeployTargetProgress(string Name, DeployItemState State);
