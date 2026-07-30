using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.EventLog.Internal.Models;
using Vaeron.HelixStudio.Internal.Runtime;

namespace Vaeron.HelixStudio.EventLog.Internal;

internal sealed class EventLogDatabaseInitializer(IDbContextFactory<EventLogDbContext> dbFactory) : IFeatureDatabaseInitializer
{
    public async Task InitializeAsync()
    {
        await DatabaseBootstrap.EnsureCreatedAsync(dbFactory);
        await DatabaseBootstrap.EnsureColumnExistsAsync(dbFactory, new ColumnMigration("log_settings_rows", "export_path", "TEXT"));
    }
}
