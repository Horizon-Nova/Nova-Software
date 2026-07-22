namespace Vaeron.Capability.Sample;

/// <summary>公開表面：對外資料形狀。不可變,不含行為。</summary>
public sealed record SampleRequest(string Key);

public sealed record SampleSnapshot(string Key, string Value, DateTimeOffset CapturedAt);
