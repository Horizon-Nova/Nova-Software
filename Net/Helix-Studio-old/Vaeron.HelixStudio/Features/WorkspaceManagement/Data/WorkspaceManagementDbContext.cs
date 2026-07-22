using Microsoft.EntityFrameworkCore;

namespace Vaeron.HelixStudio.Features.WorkspaceManagement.Data;

public sealed class WorkspaceManagementDbContext(DbContextOptions<WorkspaceManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<workspace> workspaces => Set<workspace>();
    public DbSet<active_workspace> active_workspaces => Set<active_workspace>();
}

public sealed class workspace
{
    public int id { get; set; }
    public string name { get; set; } = "";
    public string description { get; set; } = "";
    public DateTimeOffset created_at { get; set; }
    public bool is_deleted { get; set; }
}

/// <summary>單列表：目前選取的工作區</summary>
public sealed class active_workspace
{
    public int id { get; set; }
    public int workspace_id { get; set; }
}
