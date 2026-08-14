namespace Vaeron.HelixStudio.Kernel;

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

/// <summary>無回傳值的操作結果。</summary>
public readonly record struct Result
{
    public required ResultKind Kind { get; init; }
    public required string Message { get; init; }

    public bool IsSuccess => Kind == ResultKind.Success;

    public bool IsRetryable => Kind == ResultKind.Unavailable;

    public static Result Ok()
        => new() { Kind = ResultKind.Success, Message = string.Empty };

    public static Result Fail(ResultKind kind, string message)
        => new() { Kind = kind, Message = message };
}

/// <summary>帶回傳值的操作結果。</summary>
public readonly record struct Result<T>
{
    public required ResultKind Kind { get; init; }
    public required string Message { get; init; }
    public T? Value { get; init; }

    public bool IsSuccess => Kind == ResultKind.Success;
    public bool IsRetryable => Kind == ResultKind.Unavailable;

    public static Result<T> Ok(T value)
        => new() { Kind = ResultKind.Success, Message = string.Empty, Value = value };

    public static Result<T> Fail(ResultKind kind, string message)
        => new() { Kind = kind, Message = message };
}
