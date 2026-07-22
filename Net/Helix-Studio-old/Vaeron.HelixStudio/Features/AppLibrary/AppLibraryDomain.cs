using System.IO;
using Vaeron.HelixStudio.Features.AppLibrary.Contracts;
using Vaeron.HelixStudio.Features.AppLibrary.Data;
using Vaeron.HelixStudio.Features.AppsManagement;
using Vaeron.HelixStudio.Features.AppsManagement.Contracts;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio.Features.AppLibrary;

public sealed class AppLibraryDomain(
    AppLibraryRepository repo,
    BuiltInCatalogRepository builtInCatalog,
    AppsManagementFacade appsManagement,
    FileStorage fileStorage,
    string importStagingRoot)
{
    private static readonly string[] AllowedExtensions = [".exe", ".msi", ".zip"];
    private const long MaxImportSizeBytes = 200L * 1024 * 1024;

    private static readonly LibraryCategorySnapshot[] Categories =
    [
        new() { Id = "monitoring", Label = "監控" },
        new() { Id = "data", Label = "資料" },
        new() { Id = "maintenance", Label = "維護" },
        new() { Id = "custom", Label = "自訂" },
    ];

    #region 事件中繼

    public event Action? Changed;

    #endregion

    #region 統一的查詢方法

    public IReadOnlyList<LibraryCategorySnapshot> LoadCategoryList() => Categories;

    public async Task<IReadOnlyList<LibraryAppSnapshot>> LoadLibraryAsync(int? activeWorkspaceId)
    {
        var installedByName = activeWorkspaceId is null
            ? new Dictionary<string, InstalledToolSnapshot>()
            : (await appsManagement.LoadInstalledToolListAsync(activeWorkspaceId.Value))
                .GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.First());

        var builtIn = builtInCatalog.QueryBuiltInCatalog().Select(x => ToSnapshot(
            x.CatalogId, x.Name, x.CategoryId, x.Description, x.Version, x.Developer,
            x.FileSizeBytes, x.Requirements, x.LastUpdated, null, isBuiltIn: true, installedByName));

        var imported = (await repo.QueryImportedListAsync()).Select(x => ToSnapshot(
            x.catalog_id, x.name, x.category_id, x.description, x.version, x.developer,
            x.file_size_bytes, x.requirements, x.imported_at, x.readme, isBuiltIn: false, installedByName));

        return builtIn.Concat(imported).OrderBy(x => x.Name).ToList();
    }

    #endregion

    #region 基本 CRUD 操作

    public async Task<(bool success, string message)> ImportToolAsync(ImportToolRequest request)
    {
        var ext = Path.GetExtension(request.FilePath).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return ResultContract.Fail("不支援的檔案格式，僅接受 .exe/.msi/.zip");
        if (!repo.SourceFileExists(request.FilePath))
            return ResultContract.Fail("找不到來源檔案");

        var size = repo.GetFileSizeBytes(request.FilePath);
        if (size > MaxImportSizeBytes)
            return ResultContract.Fail("檔案超過 200MB 上限");

        var name = Path.GetFileNameWithoutExtension(request.FilePath);
        var (resolveOk, resolveMsg, destPath) = fileStorage.TryResolve(importStagingRoot, $"{name}{ext}");
        if (!resolveOk || destPath is null)
            return ResultContract.Fail(resolveMsg);

        repo.StageImportFile(request.FilePath, destPath);

        var existing = await repo.QueryByNameAsync(name);
        var nextVersion = existing is null ? "1.0.0" : BumpPatchVersion(existing.version);

        var entry = await repo.InsertImportedEntryAsync(new imported_catalog_entry
        {
            catalog_id = existing?.catalog_id ?? Guid.NewGuid().ToString("N"),
            name = name,
            category_id = "custom",
            description = "使用者匯入的工具",
            version = nextVersion,
            developer = "",
            file_size_bytes = size,
            requirements = "",
            entry_path = destPath,
            imported_at = DateTimeOffset.UtcNow,
        });

        if (request.WorkspaceId is { } workspaceId)
        {
            var installed = (await appsManagement.LoadInstalledToolListAsync(workspaceId))
                .FirstOrDefault(t => t.Name == name);
            if (installed is not null)
            {
                await appsManagement.InstallToolAsync(new InstallToolRequest
                {
                    WorkspaceId = workspaceId,
                    CatalogId = entry.catalog_id,
                    Name = entry.name,
                    Version = entry.version,
                    Developer = entry.developer,
                    Description = entry.description,
                    EntryPath = entry.entry_path,
                });
            }
        }

        Changed?.Invoke();
        return ResultContract.Ok(existing is null ? $"已匯入「{entry.name}」" : $"已更新「{entry.name}」至 v{entry.version}");
    }

    /// <summary>修訂號 +1（X.Y.Z → X.Y.(Z+1)）</summary>
    private static string BumpPatchVersion(string version)
    {
        var parts = version.Split('.');
        if (parts.Length == 3 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor) && int.TryParse(parts[2], out var patch))
            return $"{major}.{minor}.{patch + 1}";
        return "1.0.0";
    }

    public async Task<(bool success, string message)> AddToWorkspaceAsync(string catalogId, int workspaceId)
    {
        var builtIn = builtInCatalog.QueryBuiltInCatalog().FirstOrDefault(x => x.CatalogId == catalogId);
        if (builtIn is not null)
        {
            var result = await appsManagement.InstallToolAsync(new InstallToolRequest
            {
                WorkspaceId = workspaceId,
                CatalogId = builtIn.CatalogId,
                Name = builtIn.Name,
                Version = builtIn.Version,
                Developer = builtIn.Developer,
                Description = builtIn.Description,
                EntryPath = builtIn.EntryPath,
            });
            return result;
        }

        var imported = (await repo.QueryImportedListAsync()).FirstOrDefault(x => x.catalog_id == catalogId);
        if (imported is null) return ResultContract.Fail("找不到此工具");

        return await appsManagement.InstallToolAsync(new InstallToolRequest
        {
            WorkspaceId = workspaceId,
            CatalogId = imported.catalog_id,
            Name = imported.name,
            Version = imported.version,
            Developer = imported.developer,
            Description = imported.description,
            EntryPath = imported.entry_path,
        });
    }

    /// <summary>從目前工作區移除工具安裝</summary>
    public async Task<(bool success, string message)> RemoveFromWorkspaceAsync(string appName, int workspaceId)
    {
        var installed = (await appsManagement.LoadInstalledToolListAsync(workspaceId))
            .FirstOrDefault(t => t.Name == appName);
        if (installed is null) return ResultContract.Fail("此工作區未安裝此工具");

        var (success, message) = await appsManagement.RemoveToolAsync(installed.Id);
        if (success) Changed?.Invoke();
        return (success, message);
    }

    public async Task<(bool success, string message)> RemoveImportedByCatalogIdAsync(string catalogId)
    {
        var entry = (await repo.QueryImportedListAsync()).FirstOrDefault(x => x.catalog_id == catalogId);
        if (entry is null) return ResultContract.Fail("找不到此工具，或它是內建工具（無法移除）");

        var removed = await repo.DeleteImportedEntryAsync(entry.id);
        if (!removed) return ResultContract.Fail("找不到此工具");

        Changed?.Invoke();
        return ResultContract.Ok("已移除");
    }

    #endregion

    private static LibraryAppSnapshot ToSnapshot(
        string catalogId, string name, string categoryId, string description, string version, string developer,
        long fileSizeBytes, string requirements, DateTimeOffset lastUpdated, string? readme, bool isBuiltIn,
        IReadOnlyDictionary<string, InstalledToolSnapshot> installedByName)
    {
        installedByName.TryGetValue(name, out var installed);
        return new LibraryAppSnapshot
        {
            CatalogId = catalogId,
            Name = name,
            CategoryId = categoryId,
            Description = description,
            Version = version,
            Developer = developer,
            FileSize = FormatSize(fileSizeBytes),
            Requirements = requirements,
            LastUpdated = lastUpdated,
            Readme = readme,
            IsBuiltIn = isBuiltIn,
            IsInstalledInActiveWorkspace = installed is not null,
            InstalledVersion = installed?.Version,
        };
    }

    private static string FormatSize(long bytes)
        => bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:F1} MB" : $"{bytes / 1024.0:F0} KB";
}
