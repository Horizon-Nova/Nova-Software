using Microsoft.Extensions.Options;
using Vaeron.Kernel;

namespace Vaeron.Capability.Sample.Internal;

// internal：外部組件無法存取。此為編譯器保證,不需規則文字。
internal sealed class SampleFacade : ISampleFacade
{
    private readonly SampleOptions _options;

    public SampleFacade(IOptions<SampleOptions> options) => _options = options.Value;

    public ValueTask<Result<SampleSnapshot>> LoadAsync(
        SampleRequest request,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(Result<SampleSnapshot>.Ok(
            new SampleSnapshot(request.Key, _options.Endpoint, DateTimeOffset.UtcNow)));
}
