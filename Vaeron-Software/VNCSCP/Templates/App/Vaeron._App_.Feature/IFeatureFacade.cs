using Vaeron.Kernel;

namespace Vaeron._App_.Feature;

/// <summary>跨功能單元往來的唯一入口。其他上層組件僅能經此存取本功能。</summary>
public interface IFeatureFacade
{
    public ValueTask<Result<FeatureSnapshot>> LoadAsync(CancellationToken cancellationToken);
}
