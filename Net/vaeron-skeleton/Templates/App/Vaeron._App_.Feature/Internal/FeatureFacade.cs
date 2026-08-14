using Vaeron.Kernel;

namespace Vaeron._App_.Feature.Internal;

/// <summary>只委派,不含邏輯。Facade 停在業務層,不注入 Store。</summary>
internal sealed class FeatureFacade(FeatureService service) : IFeatureFacade
{
    public ValueTask<Result<FeatureSnapshot>> LoadAsync(CancellationToken cancellationToken)
        => service.LoadAsync(cancellationToken);
}
