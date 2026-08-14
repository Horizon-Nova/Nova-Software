using System.Diagnostics;

namespace Vaeron.HelixStudio.Sampling.Internal;

/// <summary>某個計數器在某一刻的原始值。</summary>
internal sealed record CounterReading(double Raw, DateTime At);

/// <summary>以效能計數器取值。整個類別一次讀完，不逐個目標查 instance 名單。</summary>
/// <remarks>
/// ReadCategory 一輪一次即涵蓋全部 instance 與全部計數器；逐個 new PerformanceCounter 的做法在
/// 上百個軟體時一輪跑不完，且行程在讀取途中結束會擲例外。原始值取回後自行算差，故無需保留計數器物件。
/// </remarks>
internal sealed class PerfCounterProvider
{
    public const string Name = "perfcounter";

    /// <summary>原始值的形狀對應的算法。key 是資料自身的值域，新增一種形狀多一列。</summary>
    private static readonly Dictionary<string, Func<CounterReading, CounterReading, double, double>> Formulas = new()
    {
        ["instant"] = static (now, _, divisor) => now.Raw / divisor,
        ["rate"] = static (now, before, divisor) => Rate(now, before) / divisor,
        ["rate_inverse"] = static (now, before, divisor) => 100 - (Rate(now, before) / divisor)
    };

    /// <summary>instance 的比對方式。key 是資料自身的值域，新增一種比對多一列。</summary>
    private static readonly Dictionary<string, Func<string, string, bool>> Matchers = new()
    {
        ["indexed"] = static (instance, wanted) =>
            instance == wanted || instance.StartsWith(wanted + "#", StringComparison.OrdinalIgnoreCase),
        ["exact"] = static (instance, wanted) => instance == wanted,
        ["all"] = static (instance, _) => instance.Length >= 0
    };

    private Dictionary<string, CounterReading> previous = [];

    /// <summary>本方式是否可用：能列出 Process 類別就算可用。</summary>
    public int Probe() =>
        Enumerable.Repeat(1, 1).Where(_ => PerformanceCounterCategory.Exists("Process")).Sum();

    /// <summary>第一輪沒有前一次的值，一律不出值，畫面顯示 N/A。</summary>
    public IEnumerable<SampleValue> Sample(IReadOnlyList<SamplingTarget> targets)
    {
        var current = Snapshot(targets);
        var samples = targets.SelectMany(target => Value(target, current)).ToList();
        previous = current;
        return samples;
    }

    private static double Rate(CounterReading now, CounterReading before) =>
        (now.Raw - before.Raw) / (now.At - before.At).TotalSeconds;

    private Dictionary<string, CounterReading> Snapshot(IReadOnlyList<SamplingTarget> targets)
    {
        var at = DateTime.UtcNow;
        var categories = targets
            .Select(target => target.Category)
            .Distinct()
            .ToDictionary(name => name, name => new PerformanceCounterCategory(name).ReadCategory());

        return targets.ToDictionary(Key, target => new CounterReading(Total(categories, target), at));
    }

    private static double Total(Dictionary<string, InstanceDataCollectionCollection> categories,
        SamplingTarget target)
    {
        var matches = Pick(Matchers, target.MatchKind).Single();

        return Pick(categories, target.Category)
            .SelectMany(data => data.Values.Cast<InstanceDataCollection>())
            .Where(set => set.CounterName == target.Counter)
            .SelectMany(set => set.Values.Cast<InstanceData>())
            .Where(item => matches(item.InstanceName, target.Instance))
            .Sum(item => (double)item.RawValue);
    }

    private IEnumerable<SampleValue> Value(SamplingTarget target, Dictionary<string, CounterReading> current)
    {
        var key = Key(target);

        return Pick(current, key).SelectMany(now =>
               Pick(previous, key).Select(before =>
                   new SampleValue(target.ScopeId, target.MetricKey,
                       Pick(Formulas, target.Kind).Single()(now, before, target.Divisor))));
    }

    /// <summary>以 key 取值。取不到就是空序列，交給呼叫端的 SelectMany 決定沒有值時的形狀。</summary>
    private static IEnumerable<T> Pick<T>(Dictionary<string, T> table, string key) =>
        table.Where(pair => pair.Key == key).Select(pair => pair.Value);

    private static string Key(SamplingTarget target) => $"{target.Counter}\\{target.ScopeId}";
}
