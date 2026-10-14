using Vaeron.Kernel;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.RemoteDeployment.Internal;

/// <summary>部署操作的成敗。佇列由 Runner 承擔，本層只決定結果的形狀。</summary>
internal sealed class RemoteDeploymentService(RemoteDeploymentLogic logic)
{
    #region 統一的查詢方法

    internal ValueTask<Result<IReadOnlyList<DeploymentHistorySnapshot>>> LoadHistoryListAsync(CancellationToken cancellationToken)
        => Attempt.RunAsync(
            () => logic.LoadHistoryListAsync(cancellationToken),
            ResultKind.Unavailable,
            "讀取部署歷史失敗：");

    internal ValueTask<Result<DeployPreferencesSnapshot>> LoadPreferencesAsync(CancellationToken cancellationToken)
        => Attempt.RunAsync(
            () => logic.LoadPreferencesAsync(cancellationToken),
            ResultKind.Unavailable,
            "讀取上次路徑失敗：");

    #endregion

    #region 基本 CRUD 操作

    internal ValueTask<Result<PreparedSubmission>> PrepareSubmissionAsync(
        DeploySubmission submission, CancellationToken cancellationToken)
        => new(logic.PrepareSubmissionAsync(submission, cancellationToken));

    #endregion
}

