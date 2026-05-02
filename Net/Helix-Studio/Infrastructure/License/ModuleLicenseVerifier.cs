using System.Globalization;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using HorizonNova.HelixStudio.ModuleSdk;
using Microsoft.Win32;

namespace Helix_Studio.Infrastructure.License;

/// <summary>模組授權金鑰與本機硬體指紋之驗證與評分邏輯。</summary>
internal static class ModuleLicenseVerifier
{
    private const string Salt               = "HorizonNova-InSpecTool-Salt-v2";
    private const int    PassThreshold      = 60;
    private const int    RebindThreshold    = 40;

    private static readonly HashSet<string> InvalidPlaceholders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "", "To Be Filled By O.E.M.", "Default string", "None",
            "System Serial Number", "System Product Name",
            "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF",
            "00000000-0000-0000-0000-000000000000",
            "0000000000000000",
        };


    /// <summary>解析並驗證授權金鑰，回傳有效、無效或硬體分數不足等結果。</summary>
    public static ModuleLicenseResult Verify(string moduleId, string licenseKey)
    {
        var parsed = ParseKey((licenseKey ?? string.Empty).Trim());
        if (!parsed.IsValid)
            return ModuleLicenseResult.Invalid(parsed.Error);

        var fingerprints = CollectFingerprints();
        var score        = ComputeScore(fingerprints);
        var expected     = ComputeHash(moduleId, parsed.GuidKey, fingerprints);

        if (string.IsNullOrEmpty(expected))
            return ModuleLicenseResult.Invalid("無任何有效硬體指紋可進行授權驗證", score);

        bool matched = string.Equals(expected, parsed.HashPart, StringComparison.OrdinalIgnoreCase);

        if (!matched)
            return ModuleLicenseResult.Invalid("授權金鑰與此機器的硬體不匹配", score);

        bool passed   = score >= PassThreshold;
        bool rebind   = !passed && score >= RebindThreshold;

        if (!passed)
        {
            string reason = rebind
                ? $"硬體指紋總分 {score} 低於門檻 {PassThreshold}，但達到重新綁定底線 {RebindThreshold}"
                : $"硬體指紋總分 {score} 低於重新綁定底線 {RebindThreshold}";
            return ModuleLicenseResult.Invalid(reason, score, rebind);
        }

        return ModuleLicenseResult.Valid(score);
    }


    private sealed record Fingerprint(string Label, string Value, int Weight)
    {
        public bool IsAvailable => !string.IsNullOrWhiteSpace(Value);
        public bool IsValid     => IsAvailable && !InvalidPlaceholders.Contains(Value);
    }

    private static List<Fingerprint> CollectFingerprints()
    {
        return
        [
            new("CPU ProcessorId",  QueryCpuProcessorId(),        30),
            new("SMBIOS UUID",      QuerySmbiosUuid(),            20),
            new("BIOS Serial",      QueryBiosSerial(),            15),
            new("BaseBoard Serial", QueryBaseBoardSerial(),       15),
            new("C: Volume Serial", QueryVolumeSerial(),          10),
            new("Physical NIC MAC", QueryPhysicalNicMac(),        10),
        ];
    }

    private static int ComputeScore(List<Fingerprint> fps)
        => fps.Where(f => f.IsValid).Sum(f => f.Weight);


    private static string ComputeHash(string moduleId, string guidKey, List<Fingerprint> fps)
    {
        var valid = fps
            .Where(f => f.IsValid)
            .OrderBy(f => f.Label, StringComparer.Ordinal)
            .ToList();

        if (valid.Count == 0) return string.Empty;

        byte[] sep = [0x1F];
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        if (!string.IsNullOrWhiteSpace(moduleId))
        {
            sha.AppendData(Encoding.UTF8.GetBytes(moduleId));
            sha.AppendData(sep);
        }

        sha.AppendData(Encoding.UTF8.GetBytes(guidKey));

        foreach (var fp in valid)
        {
            sha.AppendData(sep);
            sha.AppendData(Encoding.UTF8.GetBytes(fp.Label));
            sha.AppendData(sep);
            sha.AppendData(Encoding.UTF8.GetBytes(fp.Value));
        }

        sha.AppendData(sep);
        sha.AppendData(Encoding.UTF8.GetBytes(Salt));

        return string.Concat(sha.GetHashAndReset()
            .Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
    }


    private sealed record ParsedKey(bool IsValid, string GuidKey, string HashPart, string Error)
    {
        public static ParsedKey Ok(string g, string h) => new(true,  g, h, string.Empty);
        public static ParsedKey Fail(string e)          => new(false, string.Empty, string.Empty, e);
    }

    private static ParsedKey ParseKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return ParsedKey.Fail("授權金鑰為空");

        var idx = key.IndexOf(':');
        if (idx < 1 || idx >= key.Length - 1)
            return ParsedKey.Fail("金鑰格式無效（應為 GUID:雜湊）");

        var guidPart = key[..idx].Trim();
        var hashPart = key[(idx + 1)..].Trim();

        if (!Guid.TryParse(guidPart, out _))
            return ParsedKey.Fail("金鑰中的 GUID 格式無效");

        if (hashPart.Length != 64)
            return ParsedKey.Fail("金鑰中的雜湊長度不正確（應為 64 字元）");

        return ParsedKey.Ok(guidPart, hashPart);
    }


    private static string QueryCpuProcessorId()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            using var r = s.Get();
            foreach (ManagementObject o in r)
                if (o["ProcessorId"]?.ToString() is { } v && !string.IsNullOrWhiteSpace(v))
                    return v.Trim();
        }
        catch { }
        return string.Empty;
    }

    private static string QuerySmbiosUuid()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
            using var r = s.Get();
            foreach (ManagementObject o in r)
                if (o["UUID"]?.ToString() is { } v && !string.IsNullOrWhiteSpace(v))
                    return v.Trim();
        }
        catch { }
        return string.Empty;
    }

    private static string QueryBiosSerial()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
            using var r = s.Get();
            foreach (ManagementObject o in r)
                if (o["SerialNumber"]?.ToString() is { } v && !string.IsNullOrWhiteSpace(v))
                    return v.Trim();
        }
        catch { }
        return string.Empty;
    }

    private static string QueryBaseBoardSerial()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            using var r = s.Get();
            foreach (ManagementObject o in r)
                if (o["SerialNumber"]?.ToString() is { } v && !string.IsNullOrWhiteSpace(v))
                    return v.Trim();
        }
        catch { }
        return string.Empty;
    }

    private static string QueryVolumeSerial()
    {
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID='C:'");
            using var r = s.Get();
            foreach (ManagementObject o in r)
                if (o["VolumeSerialNumber"]?.ToString() is { } v && !string.IsNullOrWhiteSpace(v))
                    return v.Trim();
        }
        catch { }
        return string.Empty;
    }

    private static string QueryPhysicalNicMac()
    {
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT MACAddress, IPEnabled FROM Win32_NetworkAdapterConfiguration " +
                "WHERE IPEnabled=true AND MACAddress IS NOT NULL");
            using var r = s.Get();
            foreach (ManagementObject o in r)
            {
                var mac = o["MACAddress"]?.ToString();
                if (!string.IsNullOrWhiteSpace(mac))
                    return mac.Trim().Replace(":", "-").ToUpperInvariant();
            }
        }
        catch { }

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Ethernet
                                            or NetworkInterfaceType.Wireless80211
                    && nic.OperationalStatus == OperationalStatus.Up)
                {
                    var bytes = nic.GetPhysicalAddress().GetAddressBytes();
                    if (bytes.Length == 6)
                        return string.Join("-", bytes.Select(b => b.ToString("X2")));
                }
            }
        }
        catch { }
        return string.Empty;
    }
}
