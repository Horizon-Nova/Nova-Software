using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.AppLibrary.Internal.Models;
using Vaeron.HelixStudio.Internal.Runtime;

namespace Vaeron.HelixStudio.AppLibrary.Internal;

internal sealed class AppLibraryDatabaseInitializer(IDbContextFactory<AppLibraryDbContext> dbFactory) : IFeatureDatabaseInitializer
{
    public Task InitializeAsync() => DatabaseBootstrap.EnsureCreatedAsync(dbFactory);
}
