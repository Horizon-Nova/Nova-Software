using Vaeron.Kernel;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.RemoteDeployment.Internal;

/// <summary>本功能對外的唯一入口。提交先過 Service 驗證，再交給 Runner 排入佇列。</summary>
internal sealed class RemoteDeploymentFacade(RemoteDeploymentService service, RemoteDeploymentRunner runner)
    : IRemoteDeploymentFacade
{
    public event Action<DeployItemProgress>? ItemChanged
    {
        add => runner.ItemChanged += value;
        remove => runner.ItemChanged -= value;
    }

    public ValueTask<Result<IReadOnlyList<DeploymentHistorySnapshot>>> LoadHistoryListAsync(CancellationToken cancellationToken)
        => service.LoadHistoryListAsync(cancellationToken);

    public ValueTask<Result<DeployPreferencesSnapshot>> LoadPreferencesAsync(CancellationToken cancellationToken)
        => service.LoadPreferencesAsync(cancellationToken);

    public async ValueTask<Result<DeploySubmissionResult>> SubmitDeploymentAsync(
        DeploySubmission submission, CancellationToken cancellationToken)
    {
        var prepared = await service.PrepareSubmissionAsync(submission, cancellationToken);

        foreach (var value in prepared.Values)
        {
            runner.Submit(value);
        }

        return prepared.Map(value => new DeploySubmissionResult
        {
            TaskId = value.TaskId,
            Label = value.Label,
            Targets = value.Targets
        });
    }

    public Result RetryItem(RetryItemRequest request) => runner.RetryItem(request);

    public void CancelTask(string taskId) => runner.CancelTask(taskId);

    public IReadOnlyList<DeployItemProgress> LatestProgressOf(int connectionId) => runner.LatestProgressOf(connectionId);
}

