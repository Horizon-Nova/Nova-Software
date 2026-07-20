using System.IO;

namespace Vaeron.HelixStudio.Features.RemoteBatchTransfer.Data;

/// <summary>本機來源路徑瀏覽</summary>
public sealed class LocalFileSystemRepository
{
    public IReadOnlyList<string> ListSubdirectories(string path)
        => Directory.Exists(path) ? Directory.GetDirectories(path).Select(Path.GetFileName).OfType<string>().ToList() : [];

    public IReadOnlyList<string> ListDriveRoots()
        => DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.RootDirectory.FullName).ToList();
}
