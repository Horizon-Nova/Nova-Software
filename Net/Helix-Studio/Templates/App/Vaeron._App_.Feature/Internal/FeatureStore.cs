namespace Vaeron._App_.Feature.Internal;

/// <summary>
/// 資料存取邊界。只做讀寫,不含業務判斷。
/// 不 catch——它不知道失敗的業務語意,讓例外傳播給 Service 決定。
/// 回傳儲存體型別,不回傳對外資料形狀(投影在 Service 完成)。
/// </summary>
internal sealed class FeatureStore
{
    public ValueTask<IReadOnlyList<string>> QueryItemListAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult<IReadOnlyList<string>>([]);
}
