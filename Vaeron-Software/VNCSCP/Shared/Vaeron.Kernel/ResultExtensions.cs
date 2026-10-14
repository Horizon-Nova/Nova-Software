namespace Vaeron.Kernel;

/// <summary>結果串接。失敗原樣往下傳，呼叫端只描述成功那條路。</summary>
public static class ResultExtensions
{
    public static Result Bind(this Result result, Func<Result> next)
        => result.Successes.Select(_ => next())
            .DefaultIfEmpty(result)
            .First();

    public static Result<TOut> Bind<TOut>(this Result result, Func<Result<TOut>> next)
        => result.Successes.Select(_ => next())
            .DefaultIfEmpty(Result<TOut>.Fail(result.Kind, result.Message))
            .First();

    public static Result Bind<TIn>(this Result<TIn> result, Func<TIn, Result> next)
        => result.Values.Select(next)
            .DefaultIfEmpty(Result.Fail(result.Kind, result.Message))
            .First();

    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> next)
        => result.Values.Select(next)
            .DefaultIfEmpty(Result<TOut>.Fail(result.Kind, result.Message))
            .First();

    /// <summary>把成功值換一種形狀，失敗原樣往下傳。</summary>
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> project)
        => result.Bind(value => Result<TOut>.Ok(project(value)));

    /// <summary>丟掉成功值只留成敗。</summary>
    public static Result Discard<TIn>(this Result<TIn> result)
        => result.Bind(_ => Result.Ok());

    public static async ValueTask<Result> BindAsync(this Result result, Func<ValueTask<Result>> next)
    {
        var completed = await WhenAll(result.Successes.Select(_ => next()));
        return completed.DefaultIfEmpty(result).First();
    }

    public static async ValueTask<Result<TOut>> BindAsync<TOut>(this Result result, Func<ValueTask<Result<TOut>>> next)
    {
        var completed = await WhenAll(result.Successes.Select(_ => next()));
        return completed.DefaultIfEmpty(Result<TOut>.Fail(result.Kind, result.Message)).First();
    }

    public static async ValueTask<Result> BindAsync<TIn>(this Result<TIn> result, Func<TIn, ValueTask<Result>> next)
    {
        var completed = await WhenAll(result.Values.Select(next));
        return completed.DefaultIfEmpty(Result.Fail(result.Kind, result.Message)).First();
    }

    public static async ValueTask<Result<TOut>> BindAsync<TIn, TOut>(
        this Result<TIn> result, Func<TIn, ValueTask<Result<TOut>>> next)
    {
        var completed = await WhenAll(result.Values.Select(next));
        return completed.DefaultIfEmpty(Result<TOut>.Fail(result.Kind, result.Message)).First();
    }

    private static Task<T[]> WhenAll<T>(IEnumerable<ValueTask<T>> steps)
        => Task.WhenAll(steps.Select(step => step.AsTask()));
}
