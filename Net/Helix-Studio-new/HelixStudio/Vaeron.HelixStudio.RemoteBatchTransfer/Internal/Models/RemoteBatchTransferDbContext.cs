using Microsoft.EntityFrameworkCore;

namespace Vaeron.HelixStudio.RemoteBatchTransfer.Internal.Models;

internal sealed class RemoteBatchTransferDbContext(DbContextOptions<RemoteBatchTransferDbContext> options) : DbContext(options)
{
    public DbSet<RemoteConnectionRow> remote_connections => Set<RemoteConnectionRow>();
}

internal sealed class RemoteConnectionRow
{
    public int id { get; set; }
    public string name { get; set; } = "";
    public string host { get; set; } = "";
    public int port { get; set; }
    public string auth_method { get; set; } = "Password";
    public string account { get; set; } = "";
    public bool is_deleted { get; set; }
}
