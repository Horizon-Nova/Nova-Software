using Microsoft.EntityFrameworkCore;

namespace Vaeron.HelixStudio.AppLibrary.Internal.Models;

internal sealed class AppLibraryDbContext(DbContextOptions<AppLibraryDbContext> options) : DbContext(options)
{
    public DbSet<ImportedCatalogEntryRow> imported_catalog_entries => Set<ImportedCatalogEntryRow>();
}

internal sealed class ImportedCatalogEntryRow
{
    public int id { get; set; }
    public string catalog_id { get; set; } = "";
    public string name { get; set; } = "";
    public string category_id { get; set; } = "custom";
    public string description { get; set; } = "";
    public string version { get; set; } = "";
    public string developer { get; set; } = "";
    public long file_size_bytes { get; set; }
    public string requirements { get; set; } = "";
    public string? readme { get; set; }
    public string entry_path { get; set; } = "";
    public DateTimeOffset imported_at { get; set; }
    public bool is_deleted { get; set; }
}
