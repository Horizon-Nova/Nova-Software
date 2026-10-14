namespace Vaeron._App_.Feature.Internal;

/// <summary>業務規則集中處。呼叫 Store 取得事實，決定回傳的內容與形狀。</summary>
internal sealed class FeatureLogic(FeatureStore store)
{
    public async ValueTask<FeatureSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        var items = await store.QueryItemListAsync(cancellationToken);
        return new FeatureSnapshot("Loaded", items);
    }

    public async ValueTask<(bool Success, string Message)> CreateItemAsync(
        string item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item))
        {
            return (false, "項目內容不得為空");
        }

        var exists = await store.ExistsItemAsync(item, cancellationToken);

        if (exists)
        {
            return (false, "項目已存在");
        }

        await store.InsertItemAsync(item, cancellationToken);
        return (true, "新增成功");
    }
}
