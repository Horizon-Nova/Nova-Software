using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Helix_Studio.Utils;

namespace Helix_Studio.Data.Repositories;

/// <summary>
/// 工作區資料存取（以資料夾為單位）。
/// </summary>
public sealed class WorkspacesRepository : IDisposable
{
    private static readonly IReadOnlyList<(string Name, string Description)> DefaultWorkspaces =
    [
        ("生產線 A", "主要生產設備監控與控制"),
        ("生產線 B", "備援產線"),
    ];

    private FileSystemWatcher? _watcher;

    /// <summary>
    /// 工作區清單變更事件。
    /// </summary>
    public event EventHandler? Changed;

    public WorkspacesRepository()
    {
        EnsureDefaultWorkspaces();
        AttachWatcher();
    }

    /// <summary>
    /// 取得工作區根目錄。
    /// </summary>
    public string QueryWorkspacesDirectoryPath() => AppPathHelper.QueryWorkspacesDirectory();

    /// <summary>
    /// 取得工作區名稱清單。
    /// </summary>
    public IReadOnlyList<string> QueryWorkspaceNames()
        => Directory.EnumerateDirectories(QueryWorkspacesDirectoryPath())
            .Select(static path => Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList()!;

    /// <summary>
    /// 取得工作區描述（讀取 `description.txt`）。
    /// </summary>
    public string QueryWorkspaceDescription(string workspaceName)
    {
        var descriptionPath = Path.Combine(QueryWorkspacesDirectoryPath(), workspaceName, "description.txt");
        try
        {
            return File.ReadAllText(descriptionPath).Trim();
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 建立工作區資料夾與描述檔。
    /// </summary>
    public void CreateWorkspace(string workspaceName, string description)
    {
        var name = (workspaceName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("工作區名稱不可為空白。");
        }

        var workspacePath = Path.Combine(QueryWorkspacesDirectoryPath(), name);
        Directory.CreateDirectory(workspacePath);

        var normalizedDescription = (description ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(normalizedDescription))
        {
            File.WriteAllText(Path.Combine(workspacePath, "description.txt"), normalizedDescription);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    private void EnsureDefaultWorkspaces()
    {
        var root = QueryWorkspacesDirectoryPath();
        if (Directory.EnumerateDirectories(root).Any())
        {
            return;
        }

        foreach (var (name, description) in DefaultWorkspaces)
        {
            var workspacePath = Path.Combine(root, name);
            Directory.CreateDirectory(workspacePath);

            if (string.IsNullOrWhiteSpace(description))
            {
                continue;
            }

            File.WriteAllText(Path.Combine(workspacePath, "description.txt"), description);
        }
    }

    private void AttachWatcher()
    {
        _watcher?.Dispose();

        var workspacesDirectoryPath = QueryWorkspacesDirectoryPath();
        _watcher = new FileSystemWatcher(workspacesDirectoryPath)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.DirectoryName,
            EnableRaisingEvents = true,
        };

        _watcher.Created += OnWorkspacesChanged;
        _watcher.Deleted += OnWorkspacesChanged;
        _watcher.Renamed += OnWorkspacesChanged;
    }

    private void OnWorkspacesChanged(object? sender, FileSystemEventArgs e)
        => Changed?.Invoke(this, EventArgs.Empty);
}
