using System.IO;

namespace Vaeron.HelixStudio.RemoteBatchTransfer.Internal;

/// <summary>本機來源路徑瀏覽</summary>
internal sealed class LocalFileSystemStore
{
    public IReadOnlyList<string> ListSubdirectories(string path)
        => Directory.Exists(path) ? Directory.GetDirectories(path).Select(Path.GetFileName).OfType<string>().ToList() : [];

    public IReadOnlyList<string> ListDriveRoots()
        => DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.RootDirectory.FullName).ToList();
}
