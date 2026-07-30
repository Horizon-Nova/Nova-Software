using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.HelixStudio.RemoteBatchTransfer.Internal.Models;

namespace Vaeron.HelixStudio.RemoteBatchTransfer.Internal;

internal sealed class RemoteBatchTransferDatabaseInitializer(IDbContextFactory<RemoteBatchTransferDbContext> dbFactory)
    : IFeatureDatabaseInitializer
{
    public Task InitializeAsync() => DatabaseBootstrap.EnsureCreatedAsync(dbFactory);
}
