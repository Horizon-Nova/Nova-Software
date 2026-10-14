using System.Management;
using Microsoft.Win32;

namespace HelixStudio.Store;

/// <summary>量這台機器身上讀得到的每一項</summary>
/// <remarks>
/// 讀出來是空值的那一項就是沒有這一筆——工廠現場常有機器某一項是空的，那不該變成裝不起來；
/// WMI 或登錄檔本身出錯不吞掉，交給全域的錯誤捕捉記進 event_log。比對時只比兩邊都有的項目。
/// </remarks>
public class DeviceStore
{
    #region 量這台機器

    /// <summary>量這台機器身上每一項讀得到的識別值</summary>
    /// <remarks>機器在程式跑著的時候不會換，一個行程只量一次（啟動驗授權那一次），之後開工具、驗工具金鑰都用這一份。</remarks>
    public IReadOnlyList<MarkItem> Marks() =>
        Measured.Value;

    #endregion

    #region 量這台機器給人看的那幾項

    /// <summary>畫面上露出來的那幾項：設計檔的 CPU、系統磁碟、網卡、主機板，一項一個值；讀不到的那一項就不出現</summary>
    /// <remarks>驗證用的是 <see cref="Marks"/> 的全部；畫面只露出這幾項，全部攤開等於把秘密交出去。</remarks>
    public IReadOnlyList<MarkItem> Shown()
    {
        // 系統磁碟：Windows 裝在哪一個磁碟機，往回找到它的分割區，再找到那一顆硬碟
        var drive = $"{Environment.GetEnvironmentVariable("SystemDrive")}";
        var partitions = Ordered(
            $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{drive}'}} WHERE AssocClass = Win32_LogicalDiskToPartition",
            "DeviceID", "DeviceID");
        var disks = partitions
            .SelectMany(one => Ordered(
                $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{one}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition",
                "SerialNumber", "Index"))
            .Distinct()
            .ToList();

        // 設計檔的四項；網卡有好幾張時取編號最小的那一張，不管有沒有連線都是同一張
        return new[]
        {
            new KeyValuePair<string, IReadOnlyList<string>>("CPU 識別碼",
                Ordered("SELECT ProcessorId, DeviceID FROM Win32_Processor", "ProcessorId", "DeviceID")),
            new KeyValuePair<string, IReadOnlyList<string>>("系統磁碟序號", disks),
            new KeyValuePair<string, IReadOnlyList<string>>("網卡 MAC 位址",
                Ordered("SELECT MACAddress, Index FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE", "MACAddress", "Index")),
            new KeyValuePair<string, IReadOnlyList<string>>("主機板序號",
                Ordered("SELECT SerialNumber FROM Win32_BaseBoard", "SerialNumber", "SerialNumber")),
        }
        .SelectMany(one => one.Value.Take(1).Select(two => new MarkItem { Kind = one.Key, Value = two }))
        .ToList();
    }

    #endregion

    #region 對照

    /// <summary>跑一段 WMI 查詢，照某一欄由小到大排，取出另一欄的每一個值；都是空值就是零筆</summary>
    /// <param name="query">查詢</param>
    /// <param name="field">要取哪一欄</param>
    /// <param name="order">照哪一欄排</param>
    private static IReadOnlyList<string> Ordered(string query, string field, string order)
    {
        using var search = new ManagementObjectSearcher(query);

        return search.Get()
            .OfType<ManagementObject>()
            .OrderBy(one => $"{one[order]}".Length)
            .ThenBy(one => $"{one[order]}", StringComparer.Ordinal)
            .Select(one => $"{one[field]}".Trim())
            .Where(one => one.Length > 0)
            .ToList();
    }

    /// <summary>每一項識別值怎麼讀</summary>
    private static IReadOnlyDictionary<string, Func<IReadOnlyList<string>>> Sources { get; } =
        new Dictionary<string, Func<IReadOnlyList<string>>>
        {
            ["主機板序號"] = () => Wmi("Win32_BaseBoard", "SerialNumber"),
            ["處理器"] = () => Wmi("Win32_Processor", "ProcessorId"),
            ["BIOS 序號"] = () => Wmi("Win32_BIOS", "SerialNumber"),
            ["機器 UUID"] = () => Wmi("Win32_ComputerSystemProduct", "UUID"),
            ["硬碟序號"] = () => Wmi("Win32_DiskDrive", "SerialNumber"),
            ["網卡"] = () => Wmi("Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE", "MACAddress"),
            ["Windows 安裝"] = () => Registry(@"SOFTWARE\Microsoft\Cryptography", "MachineGuid"),
        };

    /// <summary>這個行程量到的那一份；第一次要用時才量</summary>
    private static readonly Lazy<IReadOnlyList<MarkItem>> Measured = new(() =>
        // 每一項各自去讀；空值不出現在結果裡
        Sources
            .SelectMany(one => one.Value().Select(two => new MarkItem { Kind = one.Key, Value = two }))
            .Where(one => one.Value.Length > 0)
            .OrderBy(one => one.Kind, StringComparer.Ordinal)
            .ThenBy(one => one.Value, StringComparer.Ordinal)
            .ToList());

    /// <summary>從 WMI 讀某一個欄位的每一個值；都是空值就是零筆</summary>
    /// <param name="from">哪一個類別</param>
    /// <param name="field">哪一個欄位</param>
    private static IReadOnlyList<string> Wmi(string from, string field)
    {
        using var query = new ManagementObjectSearcher($"SELECT {field} FROM {from}");

        return query.Get()
            .OfType<ManagementObject>()
            .Select(one => $"{one[field]}".Trim())
            .Where(one => one.Length > 0)
            .Distinct()
            .ToList();
    }

    /// <summary>從登錄檔讀一個值；沒有這一項或是空值就是零筆</summary>
    /// <param name="path">哪一個機碼</param>
    /// <param name="name">哪一項</param>
    private static IReadOnlyList<string> Registry(string path, string name)
    {
        using var key = RegistryKey
            .OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(path);

        return new[] { $"{key?.GetValue(name)}".Trim() }.Where(one => one.Length > 0).ToList();
    }

    #endregion

    #region 模型

    /// <summary>這台機器身上的一項識別值</summary>
    public class MarkItem
    {
        /// <summary>這是哪一種：主機板序號、網卡…</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>讀到的值</summary>
        public string Value { get; set; } = string.Empty;
    }

    #endregion
}
