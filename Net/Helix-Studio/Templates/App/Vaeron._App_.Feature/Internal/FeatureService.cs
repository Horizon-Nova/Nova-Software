using Vaeron.Kernel;

namespace Vaeron._App_.Feature.Internal;

/// <summary>
/// 決定操作成功或失敗、失敗屬於哪一種。
/// 例外在此轉為 ResultKind——本型別的方法回傳 Result,是合法的攔截點(VAE002 豁免 1)。
/// 不直接碰儲存體,一律經 Store。
/// </summary>
internal sealed class FeatureService(FeatureStore store)
{
    public async ValueTask<Result<FeatureSnapshot>> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var items = await store.QueryItemListAsync(cancellationToken);

            return Result<FeatureSnapshot>.Ok(
                new FeatureSnapshot("Loaded", items));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (IOException ex)
        {
            return Result<FeatureSnapshot>.Fail(ResultKind.Unavailable, ex.Message);
        }
    }
}
