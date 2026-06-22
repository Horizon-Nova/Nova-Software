using System.Globalization;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Helix_Studio.Infrastructure;

namespace Helix_Studio.Services;

public enum VerifyResult { Pass, Rebind, Fail, Invalid }

/// <summary>
/// L1 平台金鑰授權服務。
/// 哈希公式與 KeyToolUtility（NetToolTestArea）完全一致：
///   SHA256( [moduleId + 0x1F +] guidKey + sorted(0x1F+label+0x1F+value)... + 0x1F + salt )
/// L1 moduleId = 空白（一般金鑰模式），靠 PlatformSalt 與 L2 區分。
/// L2 工具金鑰由工具自身透過 Vaeron.ToolCore 驗證，本服務不介入。
/// </summary>
public sealed class LicenseService
{
    // L1 Platform：moduleId 空白（一般金鑰模式）；salt 與 KeyToolUtility 完全一致
    private const string PlatformModuleId = "";
    private const string PlatformSalt     = "HorizonNova-InSpecTool-Salt-v2";

    // Label 必須與 KeyToolUtility.BuildFingerprintEntries 完全相同（影響排序後的 hash）
    private static readonly (string Label, int Weight, Func<string> Query)[] Components =
    [
        ("CPU ProcessorId",  30, QueryCpu),
        ("SMBIOS UUID",      20, QuerySmbiosUuid),
        ("BIOS Serial",      15, QueryBiosSerial),
        ("BaseBoard Serial", 15, QueryBoardSerial),
        ("C: Volume Serial", 10, QueryVolumeSerial),
        ("Physical NIC MAC", 10, QueryMacAddress),
    ];

    // 與 KeyToolUtility.InvalidPlaceholders 完全一致
    private static readonly HashSet<string> InvalidPlaceholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "", "To Be Filled By O.E.M.", "Default string", "None",
        "System Serial Number", "System Product Name",
        "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF",
        "00000000-0000-0000-0000-000000000000",
        "0000000000000000",
    };

    private bool   _isPlatformLicensed;
    private string _platformKeyStatus = "missing"; // "valid" / "missing" / "invalid" / "rebind"

    public bool   IsPlatformLicensed => _isPlatformLicensed;
    public string PlatformKeyStatus  => _platformKeyStatus;

    // ── 啟動同步驗證（無 UI，直接在呼叫執行緒執行）────────

    public bool Initialize()
    {
        var path = AppPathProvider.LicenseFile;
        if (!File.Exists(path)) { _platformKeyStatus = "missing"; return false; }

        try
        {
            var json   = File.ReadAllText(path, Encoding.UTF8);
            var record = JsonSerializer.Deserialize<LicenseRecord>(json, JsonOptions);
            if (record?.Key is null) { _platformKeyStatus = "invalid"; return false; }

            var result = VerifyKeyInternal(record.Key, PlatformModuleId, PlatformSalt);
            if (result == VerifyResult.Pass)
            {
                WriteRecord(record with { LastVerifiedAt = DateTime.UtcNow });
                _isPlatformLicensed = true;
                _platformKeyStatus  = "valid";
                return true;
            }

            _platformKeyStatus = result == VerifyResult.Rebind ? "rebind" : "invalid";
            return false;
        }
        catch
        {
            _platformKeyStatus = "invalid";
            return false;
        }
    }

    // ── 使用者輸入金鑰驗證（LicensingViewModel 背景 Task）──

    public async Task<VerifyResult> VerifyPlatformKeyAsync(string key)
    {
        if (!IsValidFormat(key)) return VerifyResult.Invalid;

        var result = await Task.Run(() => VerifyKeyInternal(key, PlatformModuleId, PlatformSalt));
        if (result == VerifyResult.Pass)
        {
            var record = new LicenseRecord("platform", key, DateTime.UtcNow, DateTime.UtcNow);
            Directory.CreateDirectory(Path.GetDirectoryName(AppPathProvider.LicenseFile)!);
            await Task.Run(() => WriteRecord(record));
            _isPlatformLicensed = true;
            _platformKeyStatus  = "valid";
        }
        return result;
    }

    /// <summary>格式驗證：GUID:64 位十六進位字串。</summary>
    public static bool IsValidFormat(string key)
    {
        var colon = key.IndexOf(':');
        if (colon < 0) return false;
        var guidPart = key[..colon];
        var hashPart = key[(colon + 1)..];
        return Guid.TryParse(guidPart, out _) &&
               hashPart.Length == 64 &&
               hashPart.All(c => c is >= '0' and <= '9'
                               or >= 'a' and <= 'f'
                               or >= 'A' and <= 'F');
    }

    // ── 驗證核心（與 KeyToolUtility.VerifyKeyAgainstValues 等效）──

    private static VerifyResult VerifyKeyInternal(string key, string moduleId, string salt)
    {
        var colon = key.IndexOf(':');
        if (colon < 0) return VerifyResult.Invalid;

        var guidKey    = key[..colon].Trim();
        var storedHash = key[(colon + 1)..].Trim().ToUpperInvariant();

        if (!Guid.TryParse(guidKey, out _)) return VerifyResult.Invalid;
        if (storedHash.Length != 64) return VerifyResult.Invalid;

        var (score, currentHash) = ComputeScoreAndHash(moduleId, guidKey, salt);

        if (string.IsNullOrEmpty(currentHash)) return VerifyResult.Fail;

        // 固定時間比較防計時側通道；兩邊皆已是大寫，長度皆為 64
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(currentHash),
                Encoding.ASCII.GetBytes(storedHash)))
            return VerifyResult.Invalid;

        return score switch
        {
            >= 60 => VerifyResult.Pass,
            >= 40 => VerifyResult.Rebind,
            _     => VerifyResult.Fail,
        };
    }

    // ── Hash 計算（與 KeyToolUtility.ComputeKeyHash 等效）────

    private static (int score, string hash) ComputeScoreAndHash(
        string moduleId, string guidKey, string salt)
    {
        int score = 0;
        var validEntries = new List<(string Label, string Value)>();

        foreach (var (label, weight, query) in Components)
        {
            string value = string.Empty;
            try { value = (query() ?? string.Empty).Trim(); }
            catch { /* WMI 失敗時略過 */ }

            if (string.IsNullOrWhiteSpace(value) || InvalidPlaceholders.Contains(value)) continue;

            score += weight;
            validEntries.Add((label, value));
        }

        if (validEntries.Count == 0) return (score, string.Empty);

        byte[] sep = [0x1F];
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        if (!string.IsNullOrWhiteSpace(moduleId))
        {
            sha.AppendData(Encoding.UTF8.GetBytes(moduleId));
            sha.AppendData(sep);
        }

        sha.AppendData(Encoding.UTF8.GetBytes(guidKey));

        // 按 label ASCII 排序（與 KeyToolUtility 行為一致，確保順序穩定）
        foreach (var (label, value) in validEntries.OrderBy(e => e.Label, StringComparer.Ordinal))
        {
            sha.AppendData(sep);
            sha.AppendData(Encoding.UTF8.GetBytes(label));
            sha.AppendData(sep);
            sha.AppendData(Encoding.UTF8.GetBytes(value));
        }

        sha.AppendData(sep);
        sha.AppendData(Encoding.UTF8.GetBytes(salt));

        byte[] hashBytes = sha.GetHashAndReset();
        // 大寫 HEX — 與 KeyToolUtility "X2" 輸出完全相同
        return (score, string.Concat(hashBytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture))));
    }

    // ── WMI 查詢 ──────────────────────────────────────────

    private static string QueryCpu()        => QueryWmi("Win32_Processor",            "ProcessorId");
    private static string QuerySmbiosUuid() => QueryWmi("Win32_ComputerSystemProduct", "UUID");
    private static string QueryBiosSerial() => QueryWmi("Win32_BIOS",                 "SerialNumber");
    private static string QueryBoardSerial()=> QueryWmi("Win32_BaseBoard",             "SerialNumber");

    private static string QueryVolumeSerial()
    {
        using var s = new ManagementObjectSearcher(
            "SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID='C:'");
        using var r = s.Get();
        foreach (ManagementObject o in r)
            return o["VolumeSerialNumber"]?.ToString() ?? string.Empty;
        return string.Empty;
    }

    private static string QueryMacAddress()
    {
        // PhysicalAdapter=True；格式 normalize 成 XX-XX-XX-XX-XX-XX 大寫（與 KeyToolUtility 一致）
        using var s = new ManagementObjectSearcher(
            "SELECT MACAddress FROM Win32_NetworkAdapter WHERE PhysicalAdapter=True AND MACAddress IS NOT NULL");
        using var r = s.Get();
        foreach (ManagementObject o in r)
        {
            var mac = o["MACAddress"]?.ToString();
            if (!string.IsNullOrWhiteSpace(mac))
                return mac.Trim().Replace(":", "-").ToUpperInvariant();
        }
        return string.Empty;
    }

    private static string QueryWmi(string className, string property)
    {
        using var s = new ManagementObjectSearcher($"SELECT {property} FROM {className}");
        using var r = s.Get();
        foreach (ManagementObject o in r)
            return o[property]?.ToString() ?? string.Empty;
        return string.Empty;
    }

    // ── 持久化 ────────────────────────────────────────────

    private static void WriteRecord(LicenseRecord record)
    {
        var path = AppPathProvider.LicenseFile;
        var tmp  = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(record, JsonOptions), Encoding.UTF8);
        File.Move(tmp, path, overwrite: true);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private sealed record LicenseRecord(
        [property: JsonPropertyName("keyType")]        string   KeyType,
        [property: JsonPropertyName("key")]            string   Key,
        [property: JsonPropertyName("activatedAt")]    DateTime ActivatedAt,
        [property: JsonPropertyName("lastVerifiedAt")] DateTime LastVerifiedAt);
}
