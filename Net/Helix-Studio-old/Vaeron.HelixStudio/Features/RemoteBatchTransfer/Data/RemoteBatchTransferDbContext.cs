using Microsoft.EntityFrameworkCore;

namespace Vaeron.HelixStudio.Features.RemoteBatchTransfer.Data;

public sealed class RemoteBatchTransferDbContext(DbContextOptions<RemoteBatchTransferDbContext> options) : DbContext(options)
{
    public DbSet<remote_connection> remote_connections => Set<remote_connection>();
}

public sealed class remote_connection
{
    public int id { get; set; }
    public string name { get; set; } = "";
    public string host { get; set; } = "";
    public int port { get; set; }
    public string auth_method { get; set; } = "Password";
    public string account { get; set; } = "";
    public bool is_deleted { get; set; }
}
