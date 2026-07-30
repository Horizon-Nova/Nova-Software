using Microsoft.EntityFrameworkCore;

namespace Vaeron.HelixStudio.WorkspaceManagement.Internal.Models;

internal sealed class WorkspaceManagementDbContext(DbContextOptions<WorkspaceManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<WorkspaceRow> workspaces => Set<WorkspaceRow>();
    public DbSet<ActiveWorkspaceRow> active_workspaces => Set<ActiveWorkspaceRow>();
}

internal sealed class WorkspaceRow
{
    public int id { get; set; }
    public string name { get; set; } = "";
    public string description { get; set; } = "";
    public DateTimeOffset created_at { get; set; }
    public bool is_deleted { get; set; }
}

/// <summary>單列表：目前選取的工作區</summary>
internal sealed class ActiveWorkspaceRow
{
    public int id { get; set; }
    public int workspace_id { get; set; }
}
