using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.HelixStudio.WorkspaceManagement.Internal.Models;

namespace Vaeron.HelixStudio.WorkspaceManagement.Internal;

internal sealed class WorkspaceManagementDatabaseInitializer(IDbContextFactory<WorkspaceManagementDbContext> dbFactory)
    : IFeatureDatabaseInitializer
{
    public Task InitializeAsync() => DatabaseBootstrap.EnsureCreatedAsync(dbFactory);
}
