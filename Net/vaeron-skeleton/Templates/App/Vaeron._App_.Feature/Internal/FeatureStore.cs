namespace Vaeron._App_.Feature.Internal;

/// <summary>資料存取邊界。只做讀寫，不含業務判斷，不 catch——例外傳播給 Service 決定。</summary>
internal sealed class FeatureStore(List<string> rawItems)
{
    #region 統一的查詢來源

    private IQueryable<string> ValidItems
        => rawItems.AsQueryable().Where(item => !string.IsNullOrWhiteSpace(item));

    #endregion

    #region 專用查詢方法

    public ValueTask<IReadOnlyList<string>> QueryItemListAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult<IReadOnlyList<string>>(ValidItems.ToList());

    public ValueTask<bool> ExistsItemAsync(string item, CancellationToken cancellationToken)
        => ValueTask.FromResult(ValidItems.Any(existing => existing == item));

    #endregion

    #region 基本 CRUD 操作

    public ValueTask InsertItemAsync(string item, CancellationToken cancellationToken)
    {
        rawItems.Add(item);
        return ValueTask.CompletedTask;
    }

    #endregion
}
