using Vaeron.HelixStudio.Kernel;

namespace Vaeron.HelixStudio.Shell;

/// <summary>跨功能單元往來的唯一入口。其他上層組件僅能經此存取本功能。</summary>
public interface IShellFacade
{
    public ValueTask<Result<ShellSnapshot>> LoadAsync(CancellationToken cancellationToken);
}
