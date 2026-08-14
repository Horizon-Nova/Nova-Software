using Vaeron.Kernel;

namespace Vaeron.Capability.Sample;

/// <summary>公開表面：契約。實作在 Internal,外部組件無法存取。</summary>
public interface ISampleFacade
{
    public ValueTask<Result<SampleSnapshot>> LoadAsync(SampleRequest request, CancellationToken cancellationToken);
}
