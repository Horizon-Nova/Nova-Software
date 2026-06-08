using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Helix_Studio.Infrastructure.Paths;

namespace Helix_Studio.Data.Repositories;

/// <summary>
/// 工作區資料存取（以資料夾為單位）。
/// </summary>
public sealed class WorkspacesRepository : IDisposable
{
    private const string ReservedModulesDirectoryName = "Modules";
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

    /// <summary>
    /// 建立工作區 Repository，並初始化預設工作區與檔案監看。
    /// </summary>
    public WorkspacesRepository()
    {
        EnsureDefaultWorkspaces();
        AttachWatcher();
    }

    /// <summary>
    /// 取得工作區根目錄。
    /// </summary>
    public string QueryWorkspacesDirectoryPath() => AppPathProvider.QueryWorkspacesDirectory();

    /// <summary>
    /// 取得工作區名稱清單。
    /// </summary>
    public IReadOnlyList<string> QueryWorkspaceNames()
        => EnumerateWorkspaceNames(QueryWorkspacesDirectoryPath());

    /// <summary>
    /// 取得指定工作區的實際目錄路徑。
    /// </summary>
    public string QueryWorkspaceDirectoryPath(string workspaceName)
    {
        var resolvedName = ResolveWorkspaceName(workspaceName);
        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            throw new InvalidOperationException("工作區名稱不存在或無法解析路徑。");
        }

        return Path.Combine(QueryWorkspacesDirectoryPath(), resolvedName);
    }

    /// <summary>
    /// 將外部輸入的工作區名稱解析成現有有效工作區名稱。
    /// </summary>
    public string ResolveWorkspaceName(string workspaceName)
    {
        var normalizedName = (workspaceName ?? string.Empty).Trim();
        var workspaceNames = QueryWorkspaceNames();

        return workspaceNames.FirstOrDefault(name =>
                   string.Equals(name, normalizedName, StringComparison.OrdinalIgnoreCase))
               ?? string.Empty;
    }

    /// <summary>
    /// 解析工作區名稱，失敗時回退到第一個有效工作區。
    /// </summary>
    public string ResolveWorkspaceNameOrFallback(string workspaceName)
    {
        var resolvedName = ResolveWorkspaceName(workspaceName);
        if (!string.IsNullOrWhiteSpace(resolvedName))
        {
            return resolvedName;
        }

        return QueryWorkspaceNames().FirstOrDefault() ?? string.Empty;
    }

    /// <summary>
    /// 取得工作區描述（讀取 `description.txt`）。
    /// </summary>
    public string QueryWorkspaceDescription(string workspaceName)
    {
        var resolvedName = ResolveWorkspaceName(workspaceName);
        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            return string.Empty;
        }

        var descriptionPath = Path.Combine(QueryWorkspacesDirectoryPath(), resolvedName, "description.txt");
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

        if (!IsUsableWorkspaceName(name))
        {
            throw new InvalidOperationException("撌乩???迂??閰脩?銝?蝔?字元。");
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

    /// <summary>
    /// 釋放工作區目錄監看資源。
    /// </summary>
    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    private void EnsureDefaultWorkspaces()
    {
        var root = QueryWorkspacesDirectoryPath();
        if (EnumerateWorkspaceNames(root).Any())
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

    private static IReadOnlyList<string> EnumerateWorkspaceNames(string rootPath)
        => Directory.EnumerateDirectories(rootPath)
            .Select(static path => Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
            .Where(static name => IsUsableWorkspaceName(name))
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList()!;

    private static bool IsUsableWorkspaceName(string? name)
        => !string.IsNullOrWhiteSpace(name)
           && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
           && name.IndexOf('\uFFFD') < 0
           && !string.Equals(name, ReservedModulesDirectoryName, StringComparison.OrdinalIgnoreCase);
}
