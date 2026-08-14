using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Vaeron.HelixStudio.Sampling.Internal;

/// <summary>列舉這台機器上的行程，把 Windows 給的欄位原樣取出。</summary>
/// <remarks>
/// 路徑走 QueryFullProcessImageName 而非 Process.MainModule：後者對系統行程會擲例外，
/// 前者以回傳值表達取不到，取不到就是空字串。
/// 路徑與版本只對還沒認識的行程讀：開檔讀版本資源是每輪成本最高的一項，同名行程的答案不會變。
/// </remarks>
internal sealed class ProcessProbe
{
    private const int QueryLimitedInformation = 0x1000;
    private const int PathCapacity = 1024;

    /// <summary>已認識的行程只回報視窗、工作階段與關鍵性，其餘欄位留空，由資料庫保留既有值。</summary>
    public IEnumerable<DiscoveredProcess> Enumerate(IReadOnlySet<string> known) =>
        Process.GetProcesses().Select(process => Describe(process, known));

    private static DiscoveredProcess Describe(Process process, IReadOnlySet<string> known)
    {
        using var handle = OpenProcess(QueryLimitedInformation, false, process.Id);

        var detail = Enumerable.Repeat(process, 1)
            .Where(item => known.Contains(item.ProcessName) == false)
            .Select(item => Detail(item, handle))
            .ToList();

        return new DiscoveredProcess(
            process.ProcessName,
            string.Concat(detail.Select(item => item.Product)),
            string.Concat(detail.Select(item => item.Company)),
            string.Concat(detail.Select(item => item.Version)),
            string.Concat(detail.Select(item => item.Path)),
            HasWindow(process),
            process.SessionId,
            Critical(handle));
    }

    private static ProcessDetail Detail(Process process, SafeProcessHandle handle)
    {
        var path = PathOf(handle);
        var info = VersionOf(path).ToList();

        return new ProcessDetail(
            string.Concat(info.Select(item => item.ProductName)),
            string.Concat(info.Select(item => item.CompanyName)),
            string.Concat(info.Select(item => item.FileVersion)),
            path);
    }

    /// <summary>有無主視窗。0 或 1 是要存進資料庫的事實，不是選路的依據。</summary>
    private static int HasWindow(Process process) =>
        Math.Sign(Math.Abs(process.MainWindowHandle.ToInt64()));

    /// <summary>是否為系統關鍵行程。結束這種行程會使 Windows 立即停止，故此事實必須取得。</summary>
    private static int Critical(SafeProcessHandle handle)
    {
        var critical = false;
        var answered = IsProcessCritical(handle, ref critical);

        return Enumerable.Repeat(1, 1)
            .Where(_ => answered)
            .Where(_ => critical)
            .Sum();
    }

    private static IEnumerable<FileVersionInfo> VersionOf(string path) =>
        Enumerable.Repeat(path, 1).Where(File.Exists).Select(FileVersionInfo.GetVersionInfo);

    private static string PathOf(SafeProcessHandle handle)
    {
        var buffer = new char[PathCapacity];
        var size = PathCapacity;
        var found = QueryFullProcessImageName(handle, 0, buffer, ref size);

        return string.Concat(Enumerable.Repeat(0, 1)
            .Where(_ => found)
            .Select(_ => new string(buffer, 0, size)));
    }

    // 用 DllImport 而非 LibraryImport：後者要求整個組件開啟 unsafe，只為幾個呼叫不值得。
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(int access, bool inherit, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, int flags,
        [Out] char[] buffer, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsProcessCritical(SafeProcessHandle process, ref bool critical);

    private sealed record ProcessDetail(string Product, string Company, string Version, string Path);
}
