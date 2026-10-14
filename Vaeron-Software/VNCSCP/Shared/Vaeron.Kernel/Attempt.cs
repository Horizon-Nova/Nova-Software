using Vaeron.Kernel.Guarding;

namespace Vaeron.Kernel;

/// <summary>把可能擲出的工作收進結果型別。例外只在這裡被收下，之後一路由型別承載。</summary>
public static class Attempt
{
    public static async ValueTask<Result<T>> RunAsync<T>(
        Func<Task<T>> work, ResultKind failureKind, string failurePrefix)
    {
        var outcome = Result<T>.Fail(failureKind, failurePrefix);

        await Guard.RunAsync(
            async () => outcome = Result<T>.Ok(await work()),
            error => outcome = Result<T>.Fail(failureKind, $"{failurePrefix}{error.Message}"));

        return outcome;
    }

    public static async ValueTask<Result> RunAsync(
        Func<Task> work, ResultKind failureKind, string failurePrefix)
    {
        var outcome = Result.Ok();

        await Guard.RunAsync(
            work,
            error => outcome = Result.Fail(failureKind, $"{failurePrefix}{error.Message}"));

        return outcome;
    }
}
