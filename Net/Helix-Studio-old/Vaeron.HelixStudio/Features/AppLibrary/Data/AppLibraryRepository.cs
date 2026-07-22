using Microsoft.EntityFrameworkCore;
using System.IO;

namespace Vaeron.HelixStudio.Features.AppLibrary.Data;

public sealed class AppLibraryRepository(IDbContextFactory<AppLibraryDbContext> dbFactory)
{
    #region 統一的過濾條件

    private static IQueryable<imported_catalog_entry> Filter(AppLibraryDbContext db)
        => db.imported_catalog_entries.Where(x => !x.is_deleted);

    #endregion

    #region 專用查詢方法

    public async Task<List<imported_catalog_entry>> QueryImportedListAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db).OrderBy(x => x.name).ToListAsync();
    }

    public async Task<imported_catalog_entry?> QueryByNameAsync(string name)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db).FirstOrDefaultAsync(x => x.name == name);
    }

    #endregion

    #region 基本 CRUD 操作

    /// <summary>Upsert by name</summary>
    public async Task<imported_catalog_entry> InsertImportedEntryAsync(imported_catalog_entry data)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await Filter(db).FirstOrDefaultAsync(x => x.name == data.name);
        if (existing is null)
        {
            db.imported_catalog_entries.Add(data);
            await db.SaveChangesAsync();
            return data;
        }

        existing.version = data.version;
        existing.description = data.description;
        existing.developer = data.developer;
        existing.file_size_bytes = data.file_size_bytes;
        existing.requirements = data.requirements;
        existing.readme = data.readme;
        existing.entry_path = data.entry_path;
        existing.imported_at = data.imported_at;
        await db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteImportedEntryAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.imported_catalog_entries.FirstOrDefaultAsync(x => x.id == id);
        if (existing is null) return false;
        existing.is_deleted = true;
        await db.SaveChangesAsync();
        return true;
    }

    #endregion

    #region 匯入檔案操作

    public bool SourceFileExists(string path) => File.Exists(path);

    public long GetFileSizeBytes(string path) => new FileInfo(path).Length;

    /// <summary>複製來源檔案到暫存目錄</summary>
    public void StageImportFile(string sourcePath, string destPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath) ?? ".");
        File.Copy(sourcePath, destPath, overwrite: true);
    }

    #endregion
}
