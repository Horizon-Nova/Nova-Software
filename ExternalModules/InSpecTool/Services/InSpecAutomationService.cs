using ExternalModules.InSpecTool.Core;
using ExternalModules.InSpecTool.Utilities;

namespace ExternalModules.InSpecTool.Services;

public sealed class InSpecAutomationService : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly InSpecAutomationUtility _automationUtility = new();
    private CancellationTokenSource? _monitoringCancellation;
    private Task? _monitoringTask;

    public event EventHandler<string>? AutoInputRequested;

    public void UpdateConfiguration(InSpecToolConfiguration configuration)
    {
        _automationUtility.UpdateConfiguration(configuration);
    }

    public void Start()
    {
        lock (_syncRoot)
        {
            if (_monitoringTask is { IsCompleted: false })
            {
                return;
            }

            _monitoringCancellation = new CancellationTokenSource();
            _monitoringTask = Task.Run(() => MonitorLoopAsync(_monitoringCancellation.Token));
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cancellation;
        Task? task;

        lock (_syncRoot)
        {
            cancellation = _monitoringCancellation;
            task = _monitoringTask;
            _monitoringCancellation = null;
            _monitoringTask = null;
        }

        cancellation?.Cancel();

        try
        {
            task?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
        finally
        {
            cancellation?.Dispose();
        }
    }

    public bool TryAutoInput(string value) =>
        _automationUtility.TryAutoInput(value);

    public void Dispose() => Stop();

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var inputTypeName = _automationUtility.QueryAutoInputRequest();
            if (!string.IsNullOrWhiteSpace(inputTypeName))
            {
                AutoInputRequested?.Invoke(this, inputTypeName);
            }

            try
            {
                await Task.Delay(_automationUtility.QueryMonitorIntervalMs(), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
