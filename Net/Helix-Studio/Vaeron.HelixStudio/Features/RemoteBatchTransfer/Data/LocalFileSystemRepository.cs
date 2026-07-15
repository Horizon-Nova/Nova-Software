using System.IO;

namespace Vaeron.HelixStudio.Features.RemoteBatchTransfer.Data;

/// <summary>本機來源路徑瀏覽——這是 Windows 檔案系統 API，跟遠端傳輸協定是兩件事，
/// 不需要等待協定決定就能真的實作（見 RemoteBatchTransfer.md 藍圖查證）。</summary>
public sealed class LocalFileSystemRepository
{
    public IReadOnlyList<string> ListSubdirectories(string path)
        => Directory.Exists(path) ? Directory.GetDirectories(path).Select(Path.GetFileName).OfType<string>().ToList() : [];

    public IReadOnlyList<string> ListDriveRoots()
        => DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.RootDirectory.FullName).ToList();
}
