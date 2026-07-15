namespace Vaeron.SharedKernel;

public sealed class FileStorage
{
    /// <summary>解析 relativePath 相對 root 的絕對路徑，若結果逃出 root 範圍則回傳失敗，不回傳任何路徑。</summary>
    public (bool Success, string Message, string? ResolvedPath) TryResolve(string root, string relativePath)
    {
        var rootFull = Path.GetFullPath(root);
        var combined = Path.GetFullPath(Path.Combine(rootFull, relativePath));

        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "路徑超出允許範圍", null);
        }
        return (true, "成功", combined);
    }
}
