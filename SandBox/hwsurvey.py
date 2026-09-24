"""硬體來源普查：每個 CIM 類別在非提權下回幾列、哪些欄位真的有值。"""

import sys

import cim

CLASSES = [
    ("root/cimv2", "Win32_ComputerSystem"),
    ("root/cimv2", "Win32_ComputerSystemProduct"),
    ("root/cimv2", "Win32_BaseBoard"),
    ("root/cimv2", "Win32_BIOS"),
    ("root/cimv2", "Win32_SystemEnclosure"),
    ("root/cimv2", "Win32_Processor"),
    ("root/cimv2", "Win32_CacheMemory"),
    ("root/cimv2", "Win32_PhysicalMemory"),
    ("root/cimv2", "Win32_PhysicalMemoryArray"),
    ("root/cimv2", "Win32_DiskDrive"),
    ("root/cimv2", "Win32_DiskPartition"),
    ("root/cimv2", "Win32_LogicalDisk"),
    ("root/cimv2", "Win32_Volume"),
    ("root/cimv2", "Win32_VideoController"),
    ("root/cimv2", "Win32_DesktopMonitor"),
    ("root/cimv2", "Win32_Battery"),
    ("root/cimv2", "Win32_PortableBattery"),
    ("root/cimv2", "Win32_Fan"),
    ("root/cimv2", "Win32_TemperatureProbe"),
    ("root/cimv2", "Win32_PnPEntity"),
    ("root/cimv2", "Win32_PnPSignedDriver"),
    ("root/cimv2", "Win32_USBController"),
    ("root/cimv2", "Win32_USBHub"),
    ("root/cimv2", "Win32_SoundDevice"),
    ("root/cimv2", "Win32_Keyboard"),
    ("root/cimv2", "Win32_PointingDevice"),
    ("root/cimv2", "Win32_OperatingSystem"),
    ("root/cimv2", "Win32_SystemDriver"),
    ("root/wmi", "MSStorageDriver_FailurePredictStatus"),
    ("root/wmi", "MSStorageDriver_FailurePredictData"),
    ("root/wmi", "MSStorageDriver_ATAPISmartData"),
    ("root/wmi", "WmiMonitorID"),
    ("root/wmi", "WmiMonitorBasicDisplayParams"),
    ("root/wmi", "WmiMonitorConnectionParams"),
    ("root/wmi", "MSAcpi_ThermalZoneTemperature"),
    ("root/microsoft/windows/storage", "MSFT_PhysicalDisk"),
    ("root/microsoft/windows/storage", "MSFT_Disk"),
    ("root/microsoft/windows/storage", "MSFT_Volume"),
    ("root/microsoft/windows/storage", "MSFT_Partition"),
    ("root/microsoft/windows/storage", "MSFT_StorageReliabilityCounter"),
    ("root/cimv2/security/microsoftvolumeencryption", "Win32_EncryptableVolume"),
    ("root/cimv2/security/microsofttpm", "Win32_Tpm"),
    ("root/microsoft/windows/deviceguard", "Win32_DeviceGuard"),
    ("root/securitycenter2", "AntiVirusProduct"),
    ("root/microsoft/windows/defender", "MSFT_MpComputerStatus"),
]


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    print(f"{'namespace/類別':62} {'列數':>5}  結果")
    print("-" * 110)
    for namespace, name in CLASSES:
        ok, rows = cim.query(name, namespace)
        label = f"{namespace}/{name}"
        if not ok:
            print(f"{label:62} {'—':>5}  失敗: {rows[:60]}")
            continue
        fields = sorted({k for row in rows for k, v in cim.clean(row).items()})
        print(f"{label:62} {len(rows):>5}  欄位 {len(fields)}")


if __name__ == "__main__":
    main()
