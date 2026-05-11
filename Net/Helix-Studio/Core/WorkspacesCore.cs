using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Helix_Studio.Core;

/// <summary>
/// 工作區核心，負責監看工作區資料夾與提供名稱清單。
/// </summary>
public sealed class WorkspacesCore : IDisposable
{
    private FileSystemWatcher? _watcher;

    /// <summary>
    /// 工作區資料夾發生 I/O 變更時觸發。
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// 建立工作區核心。
    /// </summary>
    public WorkspacesCore()
    {
        AttachWatcher();
    }

    /// <summary>
    /// 查詢工作區根目錄路徑。
    /// </summary>
    public string QueryWorkspacesDirectoryPath()
    {
        var path = AppPathCore.QueryWorkspacesDirectory();
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 查詢所有工作區名稱。
    /// </summary>
    public IReadOnlyList<string> QueryWorkspaces()
        => Directory.EnumerateDirectories(QueryWorkspacesDirectoryPath())
            .Select(static path => Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// 釋放工作區檔案監看器。
    /// </summary>
    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
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
        _watcher.Renamed += OnWorkspacesRenamed;
    }

    private void OnWorkspacesChanged(object? sender, FileSystemEventArgs e)
        => Changed?.Invoke(this, EventArgs.Empty);

    private void OnWorkspacesRenamed(object? sender, RenamedEventArgs e)
        => Changed?.Invoke(this, EventArgs.Empty);
}
