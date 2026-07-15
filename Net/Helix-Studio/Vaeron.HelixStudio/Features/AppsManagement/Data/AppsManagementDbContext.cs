using Microsoft.EntityFrameworkCore;

namespace Vaeron.HelixStudio.Features.AppsManagement.Data;

public sealed class AppsManagementDbContext(DbContextOptions<AppsManagementDbContext> options) : DbContext(options)
{
    public DbSet<installed_tool> installed_tools => Set<installed_tool>();
}

public sealed class installed_tool
{
    public int id { get; set; }
    public int workspace_id { get; set; }
    public string catalog_id { get; set; } = "";
    public string name { get; set; } = "";
    public string version { get; set; } = "";
    public string developer { get; set; } = "";
    public string description { get; set; } = "";
    public string entry_path { get; set; } = "";
    public string args { get; set; } = "";
    public bool auto_start { get; set; }
    public bool auto_repair { get; set; }
    public string locked_fields_json { get; set; } = "[]";
    public DateTimeOffset installed_at { get; set; }
    public bool is_deleted { get; set; }
}
