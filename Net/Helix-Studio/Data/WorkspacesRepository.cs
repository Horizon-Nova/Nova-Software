using System.IO;
using System.Text.Json;
using Helix_Studio.Infrastructure;
using Helix_Studio.Models;

namespace Helix_Studio.Data;

/// <summary>
/// 工作區清單（workspaces.json）的讀寫。記錄所有工作區與目前選擇；
/// 首次執行時建立預設工作區，沿用既有資料目錄 id「line-a」。
/// </summary>
public sealed class WorkspacesRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private static string FilePath => Path.Combine(AppPathProvider.Home, "workspaces.json");

    public WorkspacesFile Load()
    {
        var path = FilePath;
        if (File.Exists(path))
        {
            var file = JsonSerializer.Deserialize<WorkspacesFile>(File.ReadAllText(path), JsonOpts);
            if (file is not null && file.Workspaces.Count > 0)
                return file;
        }
        return CreateDefault();
    }

    public void Save(WorkspacesFile file)
    {
        Directory.CreateDirectory(AppPathProvider.Home);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(file, JsonOpts));
    }

    private WorkspacesFile CreateDefault()
    {
        var file = new WorkspacesFile
        {
            Workspaces = [new WorkspaceInfo { Id = "line-a", Name = "生產線 A" }],
            CurrentId = "line-a",
        };
        Save(file);
        return file;
    }
}
