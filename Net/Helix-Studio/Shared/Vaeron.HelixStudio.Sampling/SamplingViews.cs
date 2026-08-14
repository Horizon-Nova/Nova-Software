namespace Vaeron.HelixStudio.Sampling;

/// <summary>採集器的一項工作：某個對象的某個度量，以及該用哪種方式取值。</summary>
/// <remarks>
/// ScopeId 是軟體 id 或 'machine'，兩者對採集器是同一種列。
/// 各取得方式只讀自己那幾欄；用不到的欄位為空字串，由資料庫的 LEFT JOIN 給。
/// </remarks>
public sealed record SamplingTarget(
    string ScopeId,
    string MetricKey,
    string Provider,
    double Divisor,
    string Instance,
    string Category,
    string Counter,
    string Kind,
    string MatchKind,
    string EtwProvider,
    string EtwGuid);

/// <summary>一次取樣的結果。取不到值的度量不會出現在結果裡，畫面顯示 N/A。</summary>
public sealed record SampleValue(string ScopeId, string MetricKey, double Value);

/// <summary>一個被觀測到的行程。欄位是 Windows 直接給的事實，取不到者為空字串。</summary>
/// <remarks>空字串轉 NULL 由資料庫的 NULLIF 完成，專案端不判斷有沒有值。</remarks>
public sealed record DiscoveredProcess(
    string Id,
    string ProductName,
    string CompanyName,
    string FileVersion,
    string ExePath,
    int HasWindow,
    int SessionId,
    int IsCritical);

/// <summary>採集器的對外入口。</summary>
public interface ISamplerFacade
{
    /// <summary>登記各取得方式目前可不可用，並開始定期取樣。</summary>
    public void Start();

    /// <summary>停止取樣。</summary>
    public void Stop();

    /// <summary>登記一個在每次取樣完成後被呼叫的對象，參數為當次取樣時刻。</summary>
    public void Subscribe(Action<string> handler);
}
