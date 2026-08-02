using System.IO;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.AppLibrary.Internal.Models;

namespace Vaeron.HelixStudio.AppLibrary.Internal;

internal sealed record StageImportFileRequest(string SourcePath, string DestPath);

internal sealed record ExtractZipRequest(string SourceZipPath, string DestDir);

internal sealed record FileSizeResult(long Bytes);

internal sealed class AppLibraryStore(IDbContextFactory<AppLibraryDbContext> dbFactory)
{
    private static IQueryable<ImportedCatalogEntryRow> Filter(AppLibraryDbContext db)
        => db.imported_catalog_entries.Where(x => !x.is_deleted);

    public async Task<List<ImportedCatalogEntryRow>> QueryImportedListAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db).OrderBy(x => x.name).ToListAsync();
    }

    public async Task<ImportedCatalogEntryRow?> QueryByNameAsync(string name)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db).FirstOrDefaultAsync(x => x.name == name);
    }

    /// <summary>Upsert by name</summary>
    public async Task<ImportedCatalogEntryRow> InsertImportedEntryAsync(ImportedCatalogEntryRow data)
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

    public bool SourceFileExists(string path) => File.Exists(path);

    public FileSizeResult GetFileSizeBytes(string path) => new(new FileInfo(path).Length);

    /// <summary>複製來源檔案到暫存目錄</summary>
    public void StageImportFile(StageImportFileRequest request)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(request.DestPath) ?? ".");
        File.Copy(request.SourcePath, request.DestPath, overwrite: true);
    }

    /// <summary>解壓 zip 套件到目的資料夾。目的資料夾若已存在（同名工具重新匯入）先整個清空再解壓，
    /// 避免舊套件殘留檔案與新套件混在一起。ZipFile.ExtractToDirectory 本身即會拒絕解壓到
    /// 目的資料夾以外的路徑（zip slip），不需要額外處理。</summary>
    public void ExtractZip(ExtractZipRequest request)
    {
        if (Directory.Exists(request.DestDir)) Directory.Delete(request.DestDir, recursive: true);
        Directory.CreateDirectory(request.DestDir);
        ZipFile.ExtractToDirectory(request.SourceZipPath, request.DestDir);
    }
}
