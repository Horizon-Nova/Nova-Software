using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using NetToolTestArea.Model;

namespace NetToolTestArea.Utility
{
    public sealed class KeyToolUtility
    {
        private const string Salt = "HorizonNova-InSpecTool-Salt-v2";
        private const int ScorePassThreshold = 60;
        private const int ScoreRebindThreshold = 40;

        private static readonly HashSet<string> InvalidPlaceholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "", "To Be Filled By O.E.M.", "Default string", "None",
            "System Serial Number", "System Product Name",
            "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF",
            "00000000-0000-0000-0000-000000000000",
            "0000000000000000"
        };

        public KeyToolSnapshot QueryLocalMachineFingerprints()
        {
            var snapshot = new KeyToolSnapshot
            {
                PassThreshold = ScorePassThreshold,
                RebindThreshold = ScoreRebindThreshold
            };

            try
            {
                snapshot.CpuProcessorId = QueryCpuProcessorIdByWmi();
                snapshot.SmbiosUuid = QuerySmbiosUuidByWmi();
                snapshot.BiosSerialNumber = QueryBiosSerialNumberByWmi();
                snapshot.BaseBoardSerialNumber = QueryBaseBoardSerialNumberByWmi();
                snapshot.SystemDriveVolumeSerial = QuerySystemDriveVolumeSerialByWmi();
                snapshot.PhysicalNicMac = QueryPhysicalNicMacByWmi();
                snapshot.WindowsMachineGuid = QueryWindowsMachineGuidByRegistry();
                QuerySystemDiskIdentityByWmi(snapshot);

                PopulateFingerprintScore(snapshot);
            }
            catch (Exception ex)
            {
                snapshot.ErrorMessage = $"{ex.GetType().Name} - {ex.Message}";
            }

            return snapshot;
        }

        public KeyToolSnapshot ComputeKeyFromValues(
            string moduleId,
            string guidKey,
            string cpuProcessorId,
            string smbiosUuid,
            string biosSerialNumber,
            string baseBoardSerialNumber,
            string systemDriveVolumeSerial,
            string physicalNicMac)
        {
            var snapshot = CreateSnapshot(
                moduleId,
                guidKey,
                cpuProcessorId,
                smbiosUuid,
                biosSerialNumber,
                baseBoardSerialNumber,
                systemDriveVolumeSerial,
                physicalNicMac);

            try
            {
                PopulateFingerprintScore(snapshot);

                if (string.IsNullOrWhiteSpace(snapshot.GuidKey))
                {
                    snapshot.ErrorMessage = "GUID 不可為空白。";
                    snapshot.KeyResult = string.Empty;
                    return snapshot;
                }

                snapshot.KeyResult = ComputeKeyHash(snapshot.ModuleId, snapshot.GuidKey, snapshot.Fingerprints);
                if (string.IsNullOrWhiteSpace(snapshot.KeyResult))
                {
                    snapshot.ErrorMessage = "無任何有效指紋可參與金鑰計算。";
                    snapshot.KeyResult = string.Empty;
                    return snapshot;
                }

                if (!snapshot.IsPassedByScore)
                {
                    string suffix = snapshot.IsRebindEligible
                        ? $"（分數 {snapshot.TotalScore}，未達通過門檻 {ScorePassThreshold}；可重新綁定門檻 {ScoreRebindThreshold}）"
                        : $"（分數 {snapshot.TotalScore}，未達重新綁定門檻 {ScoreRebindThreshold}）";
                    snapshot.ErrorMessage = $"指紋分數不足{suffix}";
                }

                return snapshot;
            }
            catch (Exception ex)
            {
                snapshot.ErrorMessage = $"{ex.GetType().Name} - {ex.Message}";
                snapshot.KeyResult = string.Empty;
                return snapshot;
            }
        }

        public KeyToolSnapshot VerifyKeyAgainstValues(
            string moduleId,
            string licenseKey,
            string cpuProcessorId,
            string smbiosUuid,
            string biosSerialNumber,
            string baseBoardSerialNumber,
            string systemDriveVolumeSerial,
            string physicalNicMac)
        {
            string inputLicenseKey = (licenseKey ?? string.Empty).Trim();
            var parsedLicense = ParseLicenseKey(inputLicenseKey);

            var snapshot = CreateSnapshot(
                moduleId,
                parsedLicense.GuidKey,
                cpuProcessorId,
                smbiosUuid,
                biosSerialNumber,
                baseBoardSerialNumber,
                systemDriveVolumeSerial,
                physicalNicMac);

            snapshot.InputLicenseKey = inputLicenseKey;

            if (!parsedLicense.IsValid)
            {
                snapshot.ErrorMessage = parsedLicense.ErrorMessage;
                snapshot.KeyResult = string.Empty;
                snapshot.IsKeyFormatValid = false;
                snapshot.IsKeyMatched = false;
                snapshot.IsActivationReady = false;
                PopulateFingerprintScore(snapshot);
                return snapshot;
            }

            try
            {
                PopulateFingerprintScore(snapshot);

                snapshot.KeyResult = ComputeKeyHash(snapshot.ModuleId, snapshot.GuidKey, snapshot.Fingerprints);
                snapshot.IsKeyFormatValid = true;

                if (string.IsNullOrWhiteSpace(snapshot.KeyResult))
                {
                    snapshot.ErrorMessage = "無任何有效指紋可參與金鑰驗證。";
                    snapshot.IsKeyMatched = false;
                    snapshot.IsActivationReady = false;
                    return snapshot;
                }

                snapshot.IsKeyMatched = string.Equals(
                    snapshot.KeyResult,
                    parsedLicense.HashPart,
                    StringComparison.OrdinalIgnoreCase);

                snapshot.IsActivationReady = snapshot.IsKeyMatched && snapshot.IsPassedByScore;

                if (!snapshot.IsKeyMatched)
                {
                    snapshot.ErrorMessage = "授權金鑰與本機指紋不匹配。";
                }
                else if (!snapshot.IsPassedByScore)
                {
                    snapshot.ErrorMessage = snapshot.IsRebindEligible
                        ? $"指紋分數不足（分數 {snapshot.TotalScore}，未達通過門檻 {ScorePassThreshold}；可重新綁定門檻 {ScoreRebindThreshold}）。"
                        : $"指紋分數不足（分數 {snapshot.TotalScore}，未達重新綁定門檻 {ScoreRebindThreshold}）。";
                }

                return snapshot;
            }
            catch (Exception ex)
            {
                snapshot.ErrorMessage = $"{ex.GetType().Name} - {ex.Message}";
                snapshot.KeyResult = string.Empty;
                snapshot.IsKeyMatched = false;
                snapshot.IsActivationReady = false;
                return snapshot;
            }
        }

        private static KeyToolSnapshot CreateSnapshot(
            string moduleId,
            string guidKey,
            string cpuProcessorId,
            string smbiosUuid,
            string biosSerialNumber,
            string baseBoardSerialNumber,
            string systemDriveVolumeSerial,
            string physicalNicMac)
        {
            return new KeyToolSnapshot
            {
                ModuleId = (moduleId ?? string.Empty).Trim(),
                GuidKey = (guidKey ?? string.Empty).Trim(),
                CpuProcessorId = (cpuProcessorId ?? string.Empty).Trim(),
                SmbiosUuid = (smbiosUuid ?? string.Empty).Trim(),
                BiosSerialNumber = (biosSerialNumber ?? string.Empty).Trim(),
                BaseBoardSerialNumber = (baseBoardSerialNumber ?? string.Empty).Trim(),
                SystemDriveVolumeSerial = (systemDriveVolumeSerial ?? string.Empty).Trim(),
                PhysicalNicMac = NormalizeMac(physicalNicMac)
            };
        }

        private static void PopulateFingerprintScore(KeyToolSnapshot snapshot)
        {
            snapshot.Fingerprints = BuildFingerprintEntries(snapshot);
            snapshot.TotalScore = snapshot.Fingerprints
                .Where(fp => fp.IsAvailable && fp.IsValid)
                .Sum(fp => fp.Weight);

            snapshot.IsPassedByScore = snapshot.TotalScore >= ScorePassThreshold;
            snapshot.IsRebindEligible = snapshot.TotalScore >= ScoreRebindThreshold && snapshot.TotalScore < ScorePassThreshold;
        }

        private static string ComputeKeyHash(string moduleId, string guidKey, List<FingerprintEntry> fingerprints)
        {
            var validFingerprints = fingerprints
                .Where(fp => fp.IsAvailable && fp.IsValid)
                .OrderBy(fp => fp.Label, StringComparer.Ordinal)
                .ToList();

            if (validFingerprints.Count == 0)
            {
                return string.Empty;
            }

            byte[] separator = { 0x1F };

            using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                if (!string.IsNullOrWhiteSpace(moduleId))
                {
                    sha.AppendData(Encoding.UTF8.GetBytes(moduleId));
                    sha.AppendData(separator);
                }

                sha.AppendData(Encoding.UTF8.GetBytes(guidKey));

                foreach (var fp in validFingerprints)
                {
                    sha.AppendData(separator);
                    sha.AppendData(Encoding.UTF8.GetBytes(fp.Label));
                    sha.AppendData(separator);
                    sha.AppendData(Encoding.UTF8.GetBytes(fp.Value));
                }

                sha.AppendData(separator);
                sha.AppendData(Encoding.UTF8.GetBytes(Salt));

                byte[] hash = sha.GetHashAndReset();
                return string.Concat(hash.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
            }
        }

        private static ParsedLicenseKey ParseLicenseKey(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return ParsedLicenseKey.Fail("授權金鑰不可為空白。");
            }

            int separatorIndex = licenseKey.IndexOf(':');
            if (separatorIndex < 1 || separatorIndex >= licenseKey.Length - 1)
            {
                return ParsedLicenseKey.Fail("授權金鑰格式錯誤，應為 GUID:HASH。");
            }

            string guidKey = licenseKey.Substring(0, separatorIndex).Trim();
            string hashPart = licenseKey.Substring(separatorIndex + 1).Trim();

            if (!Guid.TryParse(guidKey, out _))
            {
                return ParsedLicenseKey.Fail("授權金鑰的 GUID 部分格式錯誤。");
            }

            if (hashPart.Length != 64)
            {
                return ParsedLicenseKey.Fail("授權金鑰的雜湊長度錯誤，應為 64 個十六進位字元。");
            }

            return ParsedLicenseKey.Success(guidKey, hashPart);
        }

        private static List<FingerprintEntry> BuildFingerprintEntries(KeyToolSnapshot snapshot)
        {
            return new List<FingerprintEntry>
            {
                BuildEntry("CPU ProcessorId", snapshot.CpuProcessorId, 30),
                BuildEntry("SMBIOS UUID", snapshot.SmbiosUuid, 20),
                BuildEntry("BIOS Serial", snapshot.BiosSerialNumber, 15),
                BuildEntry("BaseBoard Serial", snapshot.BaseBoardSerialNumber, 15),
                BuildEntry("C: Volume Serial", snapshot.SystemDriveVolumeSerial, 10),
                BuildEntry("Physical NIC MAC", snapshot.PhysicalNicMac, 10),
            };
        }

        private static FingerprintEntry BuildEntry(string label, string rawValue, int weight)
        {
            string value = (rawValue ?? string.Empty).Trim();
            bool isAvailable = !string.IsNullOrWhiteSpace(value);
            bool isValid = isAvailable && !InvalidPlaceholders.Contains(value);

            return new FingerprintEntry
            {
                Label = label,
                Value = value,
                Weight = weight,
                IsAvailable = isAvailable,
                IsValid = isValid
            };
        }

        private static void QuerySystemDiskIdentityByWmi(KeyToolSnapshot snapshot)
        {
            try
            {
                using (var logicalDisk = new ManagementObject(@"Win32_LogicalDisk.DeviceID='C:'"))
                {
                    logicalDisk.Get();
                    using (var partitions = logicalDisk.GetRelated("Win32_DiskPartition"))
                    {
                        foreach (ManagementObject partition in partitions)
                        {
                            using (var drives = partition.GetRelated("Win32_DiskDrive"))
                            {
                                foreach (ManagementObject drive in drives)
                                {
                                    string serial = drive?["SerialNumber"]?.ToString();
                                    string pnp = drive?["PNPDeviceID"]?.ToString();
                                    string model = drive?["Model"]?.ToString();

                                    snapshot.SystemDiskSerialOrPnpId = (string.IsNullOrWhiteSpace(serial) ? pnp : serial) ?? string.Empty;
                                    snapshot.SystemDiskModel = model ?? string.Empty;
                                    snapshot.SystemDiskSerialOrPnpId = snapshot.SystemDiskSerialOrPnpId.Trim();
                                    snapshot.SystemDiskModel = snapshot.SystemDiskModel.Trim();
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                snapshot.SystemDiskSerialOrPnpId = string.Empty;
                snapshot.SystemDiskModel = string.Empty;
            }
        }

        private static string QueryBiosSerialNumberByWmi()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        string value = obj?["SerialNumber"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        private static string QueryBaseBoardSerialNumberByWmi()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        string value = obj?["SerialNumber"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        private static string QueryPhysicalNicMacByWmi()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT MACAddress, IPEnabled FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=true AND MACAddress IS NOT NULL"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        string mac = obj?["MACAddress"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(mac))
                        {
                            return mac.Trim().Replace(":", "-").ToUpperInvariant();
                        }
                    }
                }
            }
            catch { }

            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
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

                    return string.Join("-", bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
                }
            }
            catch { }

            return string.Empty;
        }

        private static string QuerySmbiosUuidByWmi()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        string value = obj?["UUID"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        private static string QueryCpuProcessorIdByWmi()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        string value = obj?["ProcessorId"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        private static string QuerySystemDriveVolumeSerialByWmi()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID='C:'"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        string value = obj?["VolumeSerialNumber"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        private static string QueryWindowsMachineGuidByRegistry()
        {
            const string subKeyPath = @"SOFTWARE\Microsoft\Cryptography";
            const string valueName = "MachineGuid";

            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var subKey = baseKey.OpenSubKey(subKeyPath, false))
                {
                    string value = subKey?.GetValue(valueName)?.ToString();
                    if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
                }
            }
            catch { }

            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (var subKey = baseKey.OpenSubKey(subKeyPath, false))
                {
                    string value = subKey?.GetValue(valueName)?.ToString();
                    if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
                }
            }
            catch { }

            return string.Empty;
        }

        private static string NormalizeMac(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace(":", "-").ToUpperInvariant();
        }

        private sealed class ParsedLicenseKey
        {
            public string GuidKey { get; private set; } = string.Empty;
            public string HashPart { get; private set; } = string.Empty;
            public bool IsValid { get; private set; }
            public string ErrorMessage { get; private set; } = string.Empty;

            public static ParsedLicenseKey Success(string guidKey, string hashPart)
            {
                return new ParsedLicenseKey
                {
                    GuidKey = guidKey,
                    HashPart = hashPart,
                    IsValid = true
                };
            }

            public static ParsedLicenseKey Fail(string errorMessage)
            {
                return new ParsedLicenseKey
                {
                    ErrorMessage = errorMessage,
                    IsValid = false
                };
            }
        }
    }
}
