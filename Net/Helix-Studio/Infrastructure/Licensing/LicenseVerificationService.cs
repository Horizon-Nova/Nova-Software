using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using HorizonNova.HelixStudio.ModuleSdk;

namespace Helix_Studio.Infrastructure.Licensing;

/// <summary>
/// 依 `NetToolTestArea` 規則驗證一般金鑰與模組金鑰。
/// </summary>
public sealed class LicenseVerificationService
{
    private const string Salt = "HorizonNova-InSpecTool-Salt-v2";
    private const int ScorePassThreshold = 60;

    private static readonly HashSet<string> InvalidPlaceholders = new(StringComparer.OrdinalIgnoreCase)
    {
        string.Empty,
        "To Be Filled By O.E.M.",
        "Default string",
        "None",
        "System Serial Number",
        "System Product Name",
        "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF",
        "00000000-0000-0000-0000-000000000000",
        "0000000000000000",
    };

    /// <summary>
    /// 以 `GUID:HASH` 規則驗證授權金鑰；一般金鑰請傳入空白 `moduleId`。
    /// </summary>
    /// <param name="moduleId">模組識別碼；一般金鑰傳入空字串。</param>
    /// <param name="licenseKey">待驗證的金鑰內容。</param>
    /// <returns>授權驗證結果。</returns>
    public ModuleLicenseVerificationResult VerifyLicense(string moduleId, string? licenseKey)
    {
        var inputLicenseKey = (licenseKey ?? string.Empty).Trim();
        var parsedLicense = ParseLicenseKey(inputLicenseKey);
        if (!parsedLicense.IsValid)
        {
            return new ModuleLicenseVerificationResult
            {
                IsValid = false,
                ErrorMessage = parsedLicense.ErrorMessage,
            };
        }

        var fingerprintEntries = BuildFingerprintEntries();
        var totalScore = fingerprintEntries
            .Where(static entry => entry.IsAvailable && entry.IsValid)
            .Sum(static entry => entry.Weight);

        if (totalScore < ScorePassThreshold)
        {
            return new ModuleLicenseVerificationResult
            {
                IsValid = false,
                ErrorMessage = $"裝置有效指紋分數不足，目前為 {totalScore}，低於授權門檻 {ScorePassThreshold}。",
            };
        }

        var expectedHash = ComputeKeyHash(moduleId, parsedLicense.GuidKey, fingerprintEntries);
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            return new ModuleLicenseVerificationResult
            {
                IsValid = false,
                ErrorMessage = "無法根據本機指紋計算授權雜湊。",
            };
        }

        if (!string.Equals(expectedHash, parsedLicense.HashPart, StringComparison.OrdinalIgnoreCase))
        {
            return new ModuleLicenseVerificationResult
            {
                IsValid = false,
                ErrorMessage = "授權金鑰與本機指紋或模組識別碼不匹配。",
            };
        }

        return new ModuleLicenseVerificationResult
        {
            IsValid = true,
        };
    }

    /// <summary>
    /// 查詢目前裝置的授權指紋摘要與可複製內容。
    /// </summary>
    public LicenseFingerprintSnapshot QueryFingerprintSnapshot()
    {
        var entries = BuildFingerprintEntries();
        var totalScore = entries
            .Where(static entry => entry.IsAvailable && entry.IsValid)
            .Sum(static entry => entry.Weight);

        var displayText = string.Join(" | ", entries
            .Where(static entry => entry.IsAvailable && entry.IsValid)
            .Select(static entry => $"{entry.Label}:{TrimFingerprintValue(entry.Value)}"));

        var clipboardText = string.Join(Environment.NewLine, entries.Select(static entry =>
            $"{entry.Label}={entry.Value} | Score={entry.Weight} | Available={entry.IsAvailable} | Valid={entry.IsValid}"));

        return new LicenseFingerprintSnapshot(
            Environment.MachineName,
            string.IsNullOrWhiteSpace(displayText) ? "無可用指紋" : displayText,
            clipboardText,
            totalScore);
    }

    private static string ComputeKeyHash(string moduleId, string guidKey, IReadOnlyList<FingerprintEntry> fingerprints)
    {
        var validFingerprints = fingerprints
            .Where(static fp => fp.IsAvailable && fp.IsValid)
            .OrderBy(static fp => fp.Label, StringComparer.Ordinal)
            .ToList();

        if (validFingerprints.Count == 0)
        {
            return string.Empty;
        }

        byte[] separator = [0x1F];

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        if (!string.IsNullOrWhiteSpace(moduleId))
        {
            sha.AppendData(Encoding.UTF8.GetBytes(moduleId));
            sha.AppendData(separator);
        }

        sha.AppendData(Encoding.UTF8.GetBytes(guidKey));

        foreach (var fingerprint in validFingerprints)
        {
            sha.AppendData(separator);
            sha.AppendData(Encoding.UTF8.GetBytes(fingerprint.Label));
            sha.AppendData(separator);
            sha.AppendData(Encoding.UTF8.GetBytes(fingerprint.Value));
        }

        sha.AppendData(separator);
        sha.AppendData(Encoding.UTF8.GetBytes(Salt));

        var hash = sha.GetHashAndReset();
        return string.Concat(hash.Select(static b => b.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static List<FingerprintEntry> BuildFingerprintEntries() =>
    [
        BuildEntry("CPU ProcessorId", QueryCpuProcessorId(), 30),
        BuildEntry("SMBIOS UUID", QuerySmbiosUuid(), 20),
        BuildEntry("BIOS Serial", QueryBiosSerialNumber(), 15),
        BuildEntry("BaseBoard Serial", QueryBaseBoardSerialNumber(), 15),
        BuildEntry("C: Volume Serial", QuerySystemDriveVolumeSerial(), 10),
        BuildEntry("Physical NIC MAC", QueryPhysicalNicMac(), 10),
    ];

    private static FingerprintEntry BuildEntry(string label, string rawValue, int weight)
    {
        var value = (rawValue ?? string.Empty).Trim();
        var isAvailable = value.Length > 0;
        var isValid = isAvailable && !InvalidPlaceholders.Contains(value);
        return new FingerprintEntry(label, value, weight, isAvailable, isValid);
    }

    private static ParsedLicenseKey ParseLicenseKey(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return ParsedLicenseKey.Fail("授權金鑰不可為空白。");
        }

        var separatorIndex = licenseKey.IndexOf(':');
        if (separatorIndex < 1 || separatorIndex >= licenseKey.Length - 1)
        {
            return ParsedLicenseKey.Fail("授權金鑰格式錯誤，應為 GUID:HASH。");
        }

        var guidKey = licenseKey[..separatorIndex].Trim();
        var hashPart = licenseKey[(separatorIndex + 1)..].Trim();

        if (!Guid.TryParse(guidKey, out _))
        {
            return ParsedLicenseKey.Fail("授權金鑰的 GUID 部分格式錯誤。");
        }

        if (hashPart.Length != 64 || hashPart.Any(static ch => !Uri.IsHexDigit(ch)))
        {
            return ParsedLicenseKey.Fail("授權金鑰的雜湊長度錯誤，應為 64 個十六進位字元。");
        }

        return ParsedLicenseKey.Success(guidKey, hashPart);
    }

    private static string QueryBiosSerialNumber()
        => QuerySingleWmiValue("SELECT SerialNumber FROM Win32_BIOS", "SerialNumber");

    private static string QueryBaseBoardSerialNumber()
        => QuerySingleWmiValue("SELECT SerialNumber FROM Win32_BaseBoard", "SerialNumber");

    private static string QuerySmbiosUuid()
        => QuerySingleWmiValue("SELECT UUID FROM Win32_ComputerSystemProduct", "UUID");

    private static string QueryCpuProcessorId()
        => QuerySingleWmiValue("SELECT ProcessorId FROM Win32_Processor", "ProcessorId");

    private static string QuerySystemDriveVolumeSerial()
        => QuerySingleWmiValue("SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID='C:'", "VolumeSerialNumber");

    private static string QueryPhysicalNicMac()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT MACAddress, IPEnabled FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=true AND MACAddress IS NOT NULL");
            using var results = searcher.Get();
            foreach (ManagementObject obj in results)
            {
                var mac = obj["MACAddress"]?.ToString();
                if (!string.IsNullOrWhiteSpace(mac))
                {
                    return NormalizeMac(mac);
                }
            }
        }
        catch
        {
        }

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType is not (NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211))
                {
                    continue;
                }

                if (nic.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                var bytes = nic.GetPhysicalAddress().GetAddressBytes();
                if (bytes.Length != 6)
                {
                    continue;
                }

                return string.Join("-", bytes.Select(static b => b.ToString("X2", CultureInfo.InvariantCulture)));
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    private static string QuerySingleWmiValue(string query, string propertyName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            using var results = searcher.Get();
            foreach (ManagementObject obj in results)
            {
                var value = obj[propertyName]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    private static string NormalizeMac(string value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace(":", "-").ToUpperInvariant();

    private static string TrimFingerprintValue(string value)
        => value.Length <= 12 ? value : $"{value[..6]}...{value[^4..]}";

    private sealed record FingerprintEntry(string Label, string Value, int Weight, bool IsAvailable, bool IsValid);

    private sealed class ParsedLicenseKey
    {
        public string GuidKey { get; private init; } = string.Empty;

        public string HashPart { get; private init; } = string.Empty;

        public bool IsValid { get; private init; }

        public string ErrorMessage { get; private init; } = string.Empty;

        public static ParsedLicenseKey Success(string guidKey, string hashPart) =>
            new()
            {
                GuidKey = guidKey,
                HashPart = hashPart,
                IsValid = true,
            };

        public static ParsedLicenseKey Fail(string errorMessage) =>
            new()
            {
                ErrorMessage = errorMessage,
                IsValid = false,
            };
    }
}

/// <summary>
/// 表示授權指紋摘要資訊。
/// </summary>
/// <param name="DeviceName">目前裝置名稱。</param>
/// <param name="DisplayText">畫面顯示用摘要。</param>
/// <param name="ClipboardText">複製到剪貼簿的完整內容。</param>
/// <param name="TotalScore">目前可用指紋分數。</param>
public sealed record LicenseFingerprintSnapshot(
    string DeviceName,
    string DisplayText,
    string ClipboardText,
    int TotalScore);
