namespace Vaeron._App_.Feature;

/// <summary>對外資料形狀。不可變。</summary>
public sealed record FeatureSnapshot(string Title, IReadOnlyList<string> Items);
