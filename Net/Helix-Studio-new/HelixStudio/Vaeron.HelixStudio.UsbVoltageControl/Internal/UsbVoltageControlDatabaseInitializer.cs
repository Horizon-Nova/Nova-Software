using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.HelixStudio.UsbVoltageControl.Internal.Models;

namespace Vaeron.HelixStudio.UsbVoltageControl.Internal;

internal sealed class UsbVoltageControlDatabaseInitializer(IDbContextFactory<UsbVoltageControlDbContext> dbFactory)
    : IFeatureDatabaseInitializer
{
    public Task InitializeAsync() => DatabaseBootstrap.EnsureCreatedAsync(dbFactory);
}
