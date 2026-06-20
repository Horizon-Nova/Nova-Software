using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Helix_Studio.Data;
using Helix_Studio.Infrastructure;
using Helix_Studio.Models;

namespace Helix_Studio.Services;

public sealed class BackupManifest
{
    public string Version { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public List<string> WorkspaceIds { get; set; } = new();
}

public sealed class BackupValidationResult
{
    public BackupManifest? Manifest { get; init; }
    public string? Error { get; init; }
    public bool IsValid => Error is null && Manifest is not null;
}

public sealed class BackupService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };
    private const string CurrentVersion = "1.0";

    private readonly InstalledToolsRepository _toolsRepo;
    private readonly WorkspacesRepository _workspacesRepo;

    public BackupService(InstalledToolsRepository toolsRepo, WorkspacesRepository workspacesRepo)
    {
        _toolsRepo = toolsRepo;
        _workspacesRepo = workspacesRepo;
    }

    public void Export(string destinationPath, IEnumerable<string> workspaceIds)
    {
        var tmp = destinationPath + ".tmp";
        var workspacesFile = _workspacesRepo.Load();
        var ids = workspaceIds.ToList();

        using (var archive = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            var manifest = new BackupManifest
            {
                Version = CurrentVersion,
                CreatedAt = DateTime.UtcNow,
                AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0",
                WorkspaceIds = ids,
            };
            WriteJsonEntry(archive, "manifest.json", manifest);

            foreach (var id in ids)
            {
                var ws = workspacesFile.Workspaces.FirstOrDefault(w => w.Id == id);
                if (ws is null) continue;
                WriteJsonEntry(archive, $"workspaces/{id}/workspace.json", new { ws.Id, ws.Name });
                WriteJsonEntry(archive, $"workspaces/{id}/installed.json", _toolsRepo.Load(id));
            }
        }

        File.Move(tmp, destinationPath, overwrite: true);
    }

    public BackupValidationResult Validate(string sourcePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(sourcePath);
            var manifestEntry = archive.GetEntry("manifest.json");
            if (manifestEntry is null)
                return new BackupValidationResult { Error = "missing_manifest" };

            BackupManifest? manifest;
            using (var stream = manifestEntry.Open())
                manifest = JsonSerializer.Deserialize<BackupManifest>(stream, JsonOpts);

            if (manifest is null)
                return new BackupValidationResult { Error = "invalid_manifest" };

            if (!Version.TryParse(manifest.Version, out var bv) ||
                !Version.TryParse(CurrentVersion, out var cv) || bv > cv)
                return new BackupValidationResult { Error = "incompatible_version", Manifest = manifest };

            foreach (var id in manifest.WorkspaceIds)
                if (archive.GetEntry($"workspaces/{id}/installed.json") is null)
                    return new BackupValidationResult { Error = "missing_workspace_data" };

            return new BackupValidationResult { Manifest = manifest };
        }
        catch (InvalidDataException)
        {
            return new BackupValidationResult { Error = "invalid_zip" };
        }
    }

    public void Restore(string sourcePath, bool overwriteExisting)
    {
        var result = Validate(sourcePath);
        if (!result.IsValid || result.Manifest is null) return;

        using var archive = ZipFile.OpenRead(sourcePath);
        var workspacesFile = _workspacesRepo.Load();

        foreach (var id in result.Manifest.WorkspaceIds)
        {
            var wsEntry = archive.GetEntry($"workspaces/{id}/workspace.json");
            var installedEntry = archive.GetEntry($"workspaces/{id}/installed.json");
            if (wsEntry is null || installedEntry is null) continue;

            WorkspaceData? wsData;
            using (var stream = wsEntry.Open())
                wsData = JsonSerializer.Deserialize<WorkspaceData>(stream, JsonOpts);
            if (wsData is null) continue;

            var targetId = overwriteExisting ? id : id + "-restored";
            Directory.CreateDirectory(AppPathProvider.WorkspaceDir(targetId));
            installedEntry.ExtractToFile(AppPathProvider.InstalledIndex(targetId), overwrite: true);

            var existing = workspacesFile.Workspaces.FirstOrDefault(w => w.Id == targetId);
            if (existing is not null)
            {
                if (overwriteExisting) existing.Name = wsData.Name;
            }
            else
            {
                workspacesFile.Workspaces.Add(new WorkspaceInfo { Id = targetId, Name = wsData.Name });
            }
        }

        _workspacesRepo.Save(workspacesFile);
    }

    private static void WriteJsonEntry<T>(ZipArchive archive, string entryName, T data)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, data, JsonOpts);
    }

    private sealed class WorkspaceData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
