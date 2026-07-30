using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.AppsManagement.Internal.Models;
using Vaeron.HelixStudio.Internal.Runtime;

namespace Vaeron.HelixStudio.AppsManagement.Internal;

internal sealed class AppsManagementDatabaseInitializer(IDbContextFactory<AppsManagementDbContext> dbFactory)
    : IFeatureDatabaseInitializer
{
    public Task InitializeAsync() => DatabaseBootstrap.EnsureCreatedAsync(dbFactory);
}
