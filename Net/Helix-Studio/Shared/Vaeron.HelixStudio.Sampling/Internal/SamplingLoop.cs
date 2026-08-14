namespace Vaeron.HelixStudio.Sampling.Internal;

/// <summary>一種取得方式與它的取值實作。provider 名稱來自資料庫，新增一種方式多一列。</summary>
/// <remarks>整批目標一次交給取值端：來源多半是一次讀完整個類別，逐個目標問會慢上兩個數量級。</remarks>
internal sealed record ProviderBinding(
    string Provider,
    Func<int> Probe,
    string Reason,
    Func<IReadOnlyList<SamplingTarget>, IEnumerable<SampleValue>> Sample);

/// <summary>常駐採集迴圈。每輪先探索清單再對工作清單取值，不認得任何特定度量或特定軟體。</summary>
internal sealed class SamplingLoop(SamplingStore store, ProcessProbe probe, PerfCounterProvider perfCounter)
    : ISamplerFacade
{
    private readonly CancellationTokenSource stopping = new();
    private readonly List<Action<string>> subscribers = [];

    private IReadOnlyList<ProviderBinding> Bindings =>
    [
        new ProviderBinding(PerfCounterProvider.Name, perfCounter.Probe, "效能計數器", perfCounter.Sample),
        new ProviderBinding("etw", static () => 0, "尚未實作", static _ => [])
    ];

    public void Subscribe(Action<string> handler) => subscribers.Add(handler);

    public void Start()
    {
        store.SetCores(Environment.ProcessorCount);

        foreach (var binding in Bindings)
        {
            store.SetProvider(binding.Provider, binding.Probe(), binding.Reason);
        }

        Task.Run(() => RunAsync(stopping.Token));
    }

    public void Stop() => stopping.Cancel();

    private async Task RunAsync(CancellationToken token)
    {
        var interval = TimeSpan.FromSeconds(store.IntervalSeconds());

        while (token.IsCancellationRequested == false)
        {
            SampleOnce();
            await Task.Delay(interval, CancellationToken.None);
        }
    }

    /// <summary>先探索清單再取讀數：清單是這台機器目前的行程，不是預先寫好的名單。</summary>
    private void SampleOnce()
    {
        store.Merge(probe.Enumerate(store.KnownIds()));

        var targets = store.Targets();
        var samples = Bindings
            .SelectMany(binding => binding.Sample(
                targets.Where(target => target.Provider == binding.Provider).ToList()))
            .ToList();

        store.WriteReadings(samples);

        var stamp = DateTime.Now.ToString("HH:mm:ss");
        store.Stamp(stamp);

        foreach (var handler in subscribers)
        {
            handler(stamp);
        }
    }
}
