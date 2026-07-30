using System.IO;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.Internal.Runtime;

public sealed record FileResolveRequest(string Root, string RelativePath);

public sealed class FileStorage
{
    /// <summary>解析 RelativePath 相對 Root 的絕對路徑，若結果逃出 Root 範圍則回傳失敗，不回傳任何路徑。</summary>
    public Result<string> TryResolve(FileResolveRequest request)
    {
        var rootFull = Path.GetFullPath(request.Root);
        var combined = Path.GetFullPath(Path.Combine(rootFull, request.RelativePath));

        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            return Result<string>.Fail(ResultKind.ValidationFailed, "路徑超出允許範圍");
        }

        return Result<string>.Ok(combined);
    }
}
