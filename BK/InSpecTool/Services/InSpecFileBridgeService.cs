using System.IO;
using ExternalModules.InSpecTool.Utilities;

namespace ExternalModules.InSpecTool.Services;

public sealed class InSpecFileBridgeService : IDisposable
{
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private FileSystemWatcher? _watcher;
    private string _currentTargetPath = string.Empty;
    private bool _isStarted;

    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        InSpecFileBridgeUtility.PrepareBridgeFiles();

        _watcher = new FileSystemWatcher(InSpecFileBridgeUtility.BaseDirectory, "Input.txt")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnInputFileChanged;
        _watcher.Created += OnInputFileChanged;

        _isStarted = true;
        _ = ProcessInputFileAsync();
    }

    public void Stop()
    {
        if (!_isStarted)
        {
            return;
        }

        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnInputFileChanged;
            _watcher.Created -= OnInputFileChanged;
            _watcher.Dispose();
            _watcher = null;
        }

        _isStarted = false;
    }

    public void UpdateCurrentTargetPath(string? targetPath)
    {
        _currentTargetPath = string.IsNullOrWhiteSpace(targetPath)
            ? string.Empty
            : targetPath.Trim();

        InSpecFileBridgeUtility.SaveCurrentTargetPathToState(_currentTargetPath);
        InSpecFileBridgeUtility.DeleteLegacyCurrentTargetFile();
    }

    public void Dispose()
    {
        Stop();
        _syncLock.Dispose();
    }

    private void OnInputFileChanged(object sender, FileSystemEventArgs eventArgs)
    {
        _ = ProcessInputFileAsync();
    }

    private async Task ProcessInputFileAsync()
    {
        if (!_isStarted)
        {
            return;
        }

        await _syncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(InSpecFileBridgeUtility.InputFile))
            {
                InSpecFileBridgeUtility.EnsureInputFileReady();
                return;
            }

            var inputBytes = await File.ReadAllBytesAsync(InSpecFileBridgeUtility.InputFile).ConfigureAwait(false);
            if (inputBytes.Length == 0)
            {
                return;
            }

            var contentBytes = InSpecFileBridgeUtility.StripUtf16LeBom(inputBytes);
            if (contentBytes.Length == 0)
            {
                await File.WriteAllBytesAsync(InSpecFileBridgeUtility.InputFile, []).ConfigureAwait(false);
                return;
            }

            var targetPath = QueryCurrentTargetPath();
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return;
            }

            await InSpecFileBridgeUtility.AppendContentToTargetAsync(targetPath, contentBytes).ConfigureAwait(false);
            await File.WriteAllBytesAsync(InSpecFileBridgeUtility.InputFile, []).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                InSpecFileBridgeUtility.TruncateInputFile();
            }
            catch
            {
            }
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private string QueryCurrentTargetPath()
    {
        if (!string.IsNullOrWhiteSpace(_currentTargetPath))
        {
            InSpecFileBridgeUtility.SaveCurrentTargetPathToState(_currentTargetPath);
            return _currentTargetPath;
        }

        _currentTargetPath = InSpecFileBridgeUtility.LoadCurrentTargetPathFromState();
        return _currentTargetPath;
    }
}
