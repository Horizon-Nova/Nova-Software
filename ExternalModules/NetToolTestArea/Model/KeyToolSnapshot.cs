using System.Collections.Generic;

namespace NetToolTestArea.Model
{
    public sealed class KeyToolSnapshot
    {
        public string ModuleId { get; set; } = string.Empty;

        public string GuidKey { get; set; } = string.Empty;

        public string SmbiosUuid { get; set; } = string.Empty;
        public string BiosSerialNumber { get; set; } = string.Empty;
        public string BaseBoardSerialNumber { get; set; } = string.Empty;
        public string WindowsMachineGuid { get; set; } = string.Empty;
        public string CpuProcessorId { get; set; } = string.Empty;
        public string SystemDriveVolumeSerial { get; set; } = string.Empty;
        public string SystemDiskSerialOrPnpId { get; set; } = string.Empty;
        public string SystemDiskModel { get; set; } = string.Empty;

        public string PhysicalNicMac { get; set; } = string.Empty;

        public string InputLicenseKey { get; set; } = string.Empty;
        public string KeyResult { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        public int TotalScore { get; set; }
        public int PassThreshold { get; set; }
        public int RebindThreshold { get; set; }
        public bool IsPassedByScore { get; set; }
        public bool IsRebindEligible { get; set; }
        public bool IsKeyFormatValid { get; set; }
        public bool IsKeyMatched { get; set; }
        public bool IsActivationReady { get; set; }

        public List<FingerprintEntry> Fingerprints { get; set; } = new List<FingerprintEntry>();
    }

    public sealed class FingerprintEntry
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public int Weight { get; set; }
        public bool IsValid { get; set; }
        public bool IsAvailable { get; set; }
    }
}
