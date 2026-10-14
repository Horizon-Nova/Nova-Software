namespace Vaeron.Kernel.Lifecycle;

/// <summary>恢復策略。判準取自 ResultKind 的可重試區分,不另立分類。無條件重試會使永久性故障看起來像暫時性故障。</summary>
public sealed class RecoveryPolicy
{
    public required int MaxAttempts { get; init; }
    public required TimeSpan InitialDelay { get; init; }
    public required double BackoffFactor { get; init; }

    public static RecoveryPolicy Default => new()
    {
        MaxAttempts = 5,
        InitialDelay = TimeSpan.FromSeconds(1),
        BackoffFactor = 2.0
    };

    public bool ShouldRetry(Result result, int attempt)
        => result.IsRetryable && attempt < MaxAttempts;

    public TimeSpan DelayFor(int attempt)
        => TimeSpan.FromMilliseconds(
            InitialDelay.TotalMilliseconds * Math.Pow(BackoffFactor, attempt));
}
