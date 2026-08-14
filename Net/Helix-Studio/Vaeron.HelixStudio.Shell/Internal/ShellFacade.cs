using Vaeron.HelixStudio.Kernel;

namespace Vaeron.HelixStudio.Shell.Internal;

/// <summary>只委派，不含邏輯。</summary>
internal sealed class ShellFacade(ShellService service) : IShellFacade
{
    public ValueTask<Result<ShellSnapshot>> LoadAsync(CancellationToken cancellationToken)
        => service.LoadAsync(cancellationToken);
}
