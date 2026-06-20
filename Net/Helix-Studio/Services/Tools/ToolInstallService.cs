using System.IO;
using Helix_Studio.Data;
using Helix_Studio.Infrastructure;
using Helix_Studio.Models;
using Helix_Studio.Services;

namespace Helix_Studio.Services.Tools;

/// <summary>
/// 工具安裝協調：安裝（複製目錄 + 寫索引）、解除安裝、匯入、更新設定。
/// </summary>
public sealed class ToolInstallService
{
    private readonly ToolCatalogService _catalog;
    private readonly InstalledToolsRepository _repository;
    private readonly EventLogService _eventLog;

    public ToolInstallService(ToolCatalogService catalog, InstalledToolsRepository repository, EventLogService eventLog)
    {
        _catalog = catalog;
        _repository = repository;
        _eventLog = eventLog;
    }

    public void Install(string workspaceId, ToolManifest manifest)
    {
        CopyDirectory(_catalog.ToolSourceDir(manifest.Id), AppPathProvider.ToolDir(workspaceId, manifest.Id));

        var records = _repository.Load(workspaceId).ToList();
        if (records.Any(r => r.ToolId == manifest.Id)) return;

        records.Add(new InstalledToolRecord
        {
            ToolId = manifest.Id,
            InstalledAt = DateTime.Now,
            Settings = new ToolSettings { Args = manifest.Args },
        });
        _repository.Save(workspaceId, records);
        _eventLog.Record(EventLevel.Info, EventCategory.Install, manifest.Name, $"已安裝（v{manifest.Version}）");
    }

    public void Uninstall(string workspaceId, string toolId)
    {
        var dir = AppPathProvider.ToolDir(workspaceId, toolId);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);

        var records = _repository.Load(workspaceId).Where(r => r.ToolId != toolId).ToList();
        _repository.Save(workspaceId, records);
        _eventLog.Record(EventLevel.Warning, EventCategory.Install, toolId, "已解除安裝");
    }

    /// <summary>更新某工具的使用者設定並持久化到 installed.json。</summary>
    public void UpdateSettings(string workspaceId, string toolId, ToolSettings settings)
    {
        var records = _repository.Load(workspaceId).ToList();
        var index = records.FindIndex(r => r.ToolId == toolId);
        if (index < 0) return;

        records[index] = new InstalledToolRecord
        {
            ToolId = toolId,
            InstalledAt = records[index].InstalledAt,
            Settings = settings,
        };
        _repository.Save(workspaceId, records);
        _eventLog.Record(EventLevel.Info, EventCategory.Install, toolId, "已更新設定");
    }

    /// <summary>
    /// 從外部資料夾匯入工具到使用者目錄。資料夾須含有效 tool.json。
    /// 回傳匯入的 manifest；無效則回傳 null。
    /// </summary>
    public ToolManifest? ImportFromFolder(string folderPath)
    {
        var manifest = ToolCatalogService.TryReadManifest(Path.Combine(folderPath, "tool.json"));
        if (manifest is null) return null;

        CopyDirectory(folderPath, Path.Combine(AppPathProvider.UserCatalogDir, manifest.Id));
        return manifest;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
