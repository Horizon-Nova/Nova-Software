namespace Vaeron.Kernel;

/// <summary>失敗的種類。呼叫方依此決定重試、提示、或放棄。</summary>
public enum ResultKind
{
    Success,
    ValidationFailed,
    Conflict,
    NotFound,
    Unavailable,
    InfrastructureError
}

/// <summary>每一種結果種類各自帶幾個值。成功帶一個值零則訊息，其餘相反。</summary>
internal static class ResultShape
{
    private static readonly Dictionary<ResultKind, int> ValueCountByKind = new()
    {
        [ResultKind.Success] = 1,
        [ResultKind.ValidationFailed] = 0,
        [ResultKind.Conflict] = 0,
        [ResultKind.NotFound] = 0,
        [ResultKind.Unavailable] = 0,
        [ResultKind.InfrastructureError] = 0
    };

    internal static int ValueCountOf(ResultKind kind) => ValueCountByKind[kind];

    internal static int FailureCountOf(ResultKind kind) => 1 - ValueCountByKind[kind];
}

/// <summary>無回傳值的操作結果。</summary>
public readonly record struct Result
{
    public required ResultKind Kind { get; init; }
    public required string Message { get; init; }

    public bool IsSuccess => Kind == ResultKind.Success;

    /// <summary>重試是否有意義。恢復策略以此為判準。</summary>
    public bool IsRetryable => Kind == ResultKind.Unavailable;

    /// <summary>成功時走一輪、失敗時走零輪的序列。成敗由長度承載，呼叫端只描述成功那條路。</summary>
    public IEnumerable<Result> Successes => Enumerable.Repeat(this, ResultShape.ValueCountOf(Kind));

    /// <summary>失敗時帶一則訊息、成功時空的序列。</summary>
    public IEnumerable<string> Failures => Enumerable.Repeat(Message, ResultShape.FailureCountOf(Kind));

    public static Result Ok()
        => new() { Kind = ResultKind.Success, Message = string.Empty };

    public static Result Fail(ResultKind kind, string message)
        => new() { Kind = kind, Message = message };

    /// <summary>把外部世界的一次判定收進結果型別。判定只在這裡發生，之後一路由型別承載。</summary>
    public static Result When(bool satisfied, ResultKind failureKind, string failureMessage)
        => Enumerable.Repeat(Ok(), Convert.ToInt32(satisfied))
            .DefaultIfEmpty(Fail(failureKind, failureMessage))
            .First();
}

/// <summary>帶回傳值的操作結果。</summary>
public readonly record struct Result<T>
{
    public required ResultKind Kind { get; init; }
    public required string Message { get; init; }
    public T? Value { get; init; }

    public bool IsSuccess => Kind == ResultKind.Success;
    public bool IsRetryable => Kind == ResultKind.Unavailable;

    /// <summary>成功時帶一個值、失敗時空的序列。成敗由長度承載，呼叫端只描述成功那條路。</summary>
    public IEnumerable<T> Values => Enumerable.Repeat(Value!, ResultShape.ValueCountOf(Kind));

    /// <summary>失敗時帶一則訊息、成功時空的序列。</summary>
    public IEnumerable<string> Failures => Enumerable.Repeat(Message, ResultShape.FailureCountOf(Kind));

    public static Result<T> Ok(T value)
        => new() { Kind = ResultKind.Success, Message = string.Empty, Value = value };

    public static Result<T> Fail(ResultKind kind, string message)
        => new() { Kind = kind, Message = message };

    /// <summary>把外部世界的一次判定收進結果型別。判定只在這裡發生，之後一路由型別承載。</summary>
    public static Result<T> When(bool satisfied, T value, ResultKind failureKind, string failureMessage)
        => Enumerable.Repeat(value, Convert.ToInt32(satisfied))
            .Select(Ok)
            .DefaultIfEmpty(Fail(failureKind, failureMessage))
            .First();

    /// <summary>序列非空時取第一個為成功值，空的時候為失敗。</summary>
    public static Result<T> FromFirst(IEnumerable<T> candidates, ResultKind failureKind, string failureMessage)
        => candidates.Select(Ok)
            .DefaultIfEmpty(Fail(failureKind, failureMessage))
            .First();
}
