using Vaeron.Kernel;

namespace Vaeron._App_.Feature.Internal;

/// <summary>決定操作成功或失敗、失敗屬於哪一種。</summary>
/// <remarks>
/// 例外在此轉為 ResultKind——本型別的方法回傳 Result,是合法的攔截點(VAE002 豁免 1)。
/// 不直接碰儲存體,一律經 Logic。
/// </remarks>
internal sealed class FeatureService(FeatureLogic logic)
{
    #region 統一的查詢方法

    public async ValueTask<Result<FeatureSnapshot>> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await logic.LoadSnapshotAsync(cancellationToken);
            return Result<FeatureSnapshot>.Ok(snapshot);
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

    #endregion

    #region 基本 CRUD 操作

    public async ValueTask<Result> CreateAsync(string item, CancellationToken cancellationToken)
    {
        try
        {
            var (success, message) = await logic.CreateItemAsync(item, cancellationToken);
            return success ? Result.Ok() : Result.Fail(ResultKind.ValidationFailed, message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (IOException ex)
        {
            return Result.Fail(ResultKind.Unavailable, ex.Message);
        }
    }

    #endregion
}
