using System.IO;
using Helix_Studio.Infrastructure.Utilities;
using Helix_Studio.Model.Modules;
using HorizonNova.HelixStudio.ModuleSdk;

namespace Helix_Studio.Services;

/// <summary>模組安裝目錄解析、掃描請求、清單投影與從資料夾匯入模組。</summary>
public sealed class ModulesCatalogServices
{
    private readonly ModuleRuntimeServices _runtime;
    private readonly WorkspaceServices     _workspace;

    /// <summary>以模組執行時期與工作區服務建立目錄服務。</summary>
    public ModulesCatalogServices(ModuleRuntimeServices runtime, WorkspaceServices workspace)
    {
        _runtime   = runtime;
        _workspace = workspace;
    }

    /// <summary>依使用中工作區解析模組應安裝的實體目錄路徑。</summary>
    public string QueryInstallDirectoryPath()
    {
        var activeId = _workspace.GetActive()?.Id;
        return activeId.HasValue
            ? AppPathUtility.QueryWorkspaceModulesDirectory(activeId.Value)
            : AppPathUtility.QueryModulesDirectory();
    }

    /// <summary>將目前掃描到的模組清冊轉成 UI 列表項目（含 README 文字）。</summary>
    public IReadOnlyList<ModuleListItem> QueryLoadedModules()
    {
        return _runtime.GetManifests()
            .Select(m => new ModuleListItem
            {
                Id                  = m.Id,
                DisplayName         = m.DisplayName,
                Version             = m.Version,
                StatusText          = "已載入",
                IsEnabled           = true,
                Author              = m.Author,
                Description         = m.Description,
                MinHostVersion      = m.MinHostVersion,
                Icon                = m.IconPngBytes,
                InstalledFolderPath = m.InstalledFolderPath,
                ReadmeText          = ReadReadmeText(m.InstalledFolderPath),
            })
            .ToList();
    }

    private static string ReadReadmeText(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath)) return string.Empty;
        var path = Path.Combine(folderPath, "README.md");
        if (!File.Exists(path)) return string.Empty;
        try   { return File.ReadAllText(path); }
        catch { return string.Empty; }
    }

    /// <summary>目前載入清冊中的模組數量。</summary>
    public int GetLoadedCount() => _runtime.GetManifests().Count;

    /// <summary>依模組識別取得單一清冊，若不存在則為 <c>null</c>。</summary>
    public ModuleManifest? GetManifest(string moduleId)
        => _runtime.GetManifests().FirstOrDefault(m => m.Id == moduleId);

    /// <summary>於背景針對目前安裝目錄觸發模組掃描。</summary>
    public void RequestScan()
    {
        var dir = QueryInstallDirectoryPath();
        Task.Run(() => _runtime.Scan(dir));
    }

    /// <summary>將來源資料夾複製到模組安裝目錄，必要時先卸載重複安裝後再觸發掃描。</summary>
    public void ImportFromFolder(string sourceFolder)
    {
        var destParent = QueryInstallDirectoryPath();
        var folderName = Path.GetFileName(
            sourceFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var destFolder = Path.Combine(destParent, folderName);

        if (Directory.Exists(destFolder))
        {
            var existing = _runtime.GetManifests()
                .FirstOrDefault(m => string.Equals(
                    m.InstalledFolderPath, destFolder,
                    StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                var weakRef = _runtime.UnloadForReplace(existing.Id);

                if (weakRef is not null)
                {
                    for (int i = 0; i < 10 && weakRef.IsAlive; i++)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }
            }
        }

        CopyDirectoryRecursive(sourceFolder, destFolder);
        RequestScan();
    }

    /// <summary>委託執行時期卸載模組，可選擇刪除安裝目錄。</summary>
    public void Unload(string moduleId, bool deleteFolder = true)
    {
        _runtime.Unload(moduleId, deleteFolder);
    }

    private static void CopyDirectoryRecursive(string source, string dest)
    {
        Directory.CreateDirectory(dest);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            var name = Path.GetFileName(file);
            File.Copy(file, Path.Combine(dest, name), overwrite: true);
        }

        foreach (var dir in Directory.EnumerateDirectories(source))
        {
            var name = Path.GetFileName(dir);
            CopyDirectoryRecursive(dir, Path.Combine(dest, name));
        }
    }
}

