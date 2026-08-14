using Vaeron.HelixStudio.Kernel;

namespace Vaeron.HelixStudio.Shell.Internal;

/// <summary>決定操作成功或失敗。本殼層的來源全為記憶體內靜態資料，沒有會擲出例外的路徑，故不含 try-catch。</summary>
internal sealed class ShellService(ShellLogic logic)
{
    public async ValueTask<Result<ShellSnapshot>> LoadAsync(CancellationToken cancellationToken)
    {
        var snapshot = await logic.BuildSnapshotAsync(cancellationToken);
        return Result<ShellSnapshot>.Ok(snapshot);
    }
}
