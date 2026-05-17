using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using Helix_Studio.Model;

namespace Helix_Studio.Core;

/// <summary>
/// 授權驗證核心。
/// </summary>
public sealed class LicenseCore
{
    private const string Salt = "HorizonNova-InSpecTool-Salt-v2";
    private const int PassThreshold = 60;

    /// <summary>
    /// 驗證主授權金鑰。
    /// </summary>
    /// <param name="license">授權金鑰模型。</param>
    public bool ValidateLicense(LicenseModel license)
        => Verify(string.Empty, license);

    /// <summary>
    /// 驗證模組授權金鑰。
    /// </summary>
    /// <param name="moduleId">模組識別碼。</param>
    /// <param name="license">授權金鑰模型。</param>
    public bool ValidateModuleLicense(string moduleId, LicenseModel license)
        => Verify(moduleId, license);

    /// <summary>
    /// 查詢模組授權驗證結果。
    /// </summary>
    /// <param name="moduleId">模組識別碼。</param>
    /// <param name="licenseKey">授權金鑰字串。</param>
    public ModuleLicenseResultModel QueryModuleLicenseResult(string moduleId, string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return ModuleLicenseResultModel.Invalid("授權金鑰為空。");
        }

        try
        {
            var ok = ValidateModuleLicense(moduleId, new LicenseModel { LicenseKey = licenseKey.Trim() });
            return ok
                ? ModuleLicenseResultModel.Valid()
                : ModuleLicenseResultModel.Invalid("授權驗證失敗。");
        }
        catch (FormatException ex)
        {
            return ModuleLicenseResultModel.Invalid(ex.Message);
        }
    }

    private static bool Verify(string moduleId, LicenseModel license)
    {
        var parsed = ParseKey(license.LicenseKey);
        var fingerprints = CollectFingerprints();
        var score = fingerprints.Where(static fingerprint => fingerprint.IsValid).Sum(static fingerprint => fingerprint.Weight);

        if (score < PassThreshold)
        {
            return false;
        }

        var expected = ComputeHash(moduleId, parsed.GuidKey, fingerprints);
        return VerifyHash(expected, parsed.HashPart);
    }

    private static bool VerifyHash(string expectedHex, string actualHex)
    {
        var expected = Convert.FromHexString(expectedHex);
        var actual = Convert.FromHexString(actualHex);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static ParsedKey ParseKey(string key)
    {
        var separatorIndex = key.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex < 1 || separatorIndex >= key.Length - 1)
        {
            throw new FormatException("授權金鑰格式錯誤，必須為 GUID:HASH。");
        }

        var guidPart = key[..separatorIndex].Trim();
        var hashPart = key[(separatorIndex + 1)..].Trim();

        _ = Guid.Parse(guidPart);
        if (hashPart.Length != 64)
        {
            throw new FormatException("授權金鑰雜湊長度錯誤，必須為 64 字元。");
        }

        return new ParsedKey(guidPart, hashPart);
    }

    private static string ComputeHash(string moduleId, string guidKey, IReadOnlyList<Fingerprint> fingerprints)
    {
        var validFingerprints = fingerprints
            .Where(static fingerprint => fingerprint.IsValid)
            .OrderBy(static fingerprint => fingerprint.Label, StringComparer.Ordinal)
            .ToList();

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

        return string.Concat(sha.GetHashAndReset()
            .Select(static value => value.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static IReadOnlyList<Fingerprint> CollectFingerprints()
        =>
        [
            new("CPU ProcessorId", QueryCpuProcessorId(), 30),
            new("SMBIOS UUID", QuerySmbiosUuid(), 20),
            new("BIOS Serial", QueryBiosSerial(), 15),
            new("BaseBoard Serial", QueryBaseBoardSerial(), 15),
            new("C: Volume Serial", QueryVolumeSerial(), 10),
            new("Physical NIC MAC", QueryPhysicalNicMac(), 10),
        ];

    private static string QueryCpuProcessorId()
    {
        using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
        using var results = searcher.Get();

        foreach (ManagementObject result in results)
        {
            if (result["ProcessorId"]?.ToString() is { } value && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string QuerySmbiosUuid()
    {
        using var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
        using var results = searcher.Get();

        foreach (ManagementObject result in results)
        {
            if (result["UUID"]?.ToString() is { } value && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string QueryBiosSerial()
    {
        using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
        using var results = searcher.Get();

        foreach (ManagementObject result in results)
        {
            if (result["SerialNumber"]?.ToString() is { } value && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string QueryBaseBoardSerial()
    {
        using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
        using var results = searcher.Get();

        foreach (ManagementObject result in results)
        {
            if (result["SerialNumber"]?.ToString() is { } value && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string QueryVolumeSerial()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID='C:'");
        using var results = searcher.Get();

        foreach (ManagementObject result in results)
        {
            if (result["VolumeSerialNumber"]?.ToString() is { } value && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string QueryPhysicalNicMac()
        => NetworkInterface.GetAllNetworkInterfaces()
            .Where(static networkInterface => networkInterface.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
            .Where(static networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up)
            .Select(static networkInterface => networkInterface.GetPhysicalAddress().GetAddressBytes())
            .Where(static bytes => bytes.Length == 6)
            .Select(static bytes => string.Join("-", bytes.Select(static value => value.ToString("X2", CultureInfo.InvariantCulture))))
            .FirstOrDefault() ?? string.Empty;

    private sealed record ParsedKey(string GuidKey, string HashPart);

    private sealed record Fingerprint(string Label, string Value, int Weight)
    {
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
    }
}

