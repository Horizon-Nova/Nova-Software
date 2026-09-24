"""硬體事實實測值：每個來源實際取到什麼。"""

import sys

import cim

SECTIONS = [
    ("整機", "root/cimv2", "Win32_ComputerSystem",
     ["Manufacturer", "Model", "SystemFamily", "SystemSKUNumber", "TotalPhysicalMemory",
      "NumberOfProcessors", "NumberOfLogicalProcessors", "Domain", "PartOfDomain",
      "PCSystemType", "HypervisorPresent", "BootupState", "ThermalState"]),
    ("整機識別", "root/cimv2", "Win32_ComputerSystemProduct",
     ["Name", "Vendor", "Version", "IdentifyingNumber", "UUID", "SKUNumber"]),
    ("主機板", "root/cimv2", "Win32_BaseBoard",
     ["Manufacturer", "Product", "Version", "SerialNumber", "Tag"]),
    ("韌體", "root/cimv2", "Win32_BIOS",
     ["Manufacturer", "Name", "SMBIOSBIOSVersion", "Version", "ReleaseDate",
      "SerialNumber", "PrimaryBIOS", "SystemBiosMajorVersion", "SystemBiosMinorVersion"]),
    ("機殼", "root/cimv2", "Win32_SystemEnclosure",
     ["Manufacturer", "SerialNumber", "ChassisTypes", "SMBIOSAssetTag", "LockPresent",
      "SecurityStatus"]),
    ("處理器", "root/cimv2", "Win32_Processor",
     ["Name", "Manufacturer", "ProcessorId", "SocketDesignation", "NumberOfCores",
      "NumberOfLogicalProcessors", "MaxClockSpeed", "CurrentClockSpeed", "L2CacheSize",
      "L3CacheSize", "Architecture", "AddressWidth", "VirtualizationFirmwareEnabled",
      "SecondLevelAddressTranslationExtensions", "Status", "CurrentVoltage"]),
    ("記憶體模組", "root/cimv2", "Win32_PhysicalMemory",
     ["BankLabel", "DeviceLocator", "Capacity", "Speed", "ConfiguredClockSpeed",
      "Manufacturer", "PartNumber", "SerialNumber", "SMBIOSMemoryType", "FormFactor",
      "ConfiguredVoltage"]),
    ("記憶體插槽總覽", "root/cimv2", "Win32_PhysicalMemoryArray",
     ["MaxCapacityEx", "MemoryDevices", "Use", "MemoryErrorCorrection"]),
    ("實體磁碟", "root/microsoft/windows/storage", "MSFT_PhysicalDisk",
     ["FriendlyName", "SerialNumber", "FirmwareVersion", "MediaType", "BusType",
      "Size", "HealthStatus", "OperationalStatus", "SpindleSpeed", "CanPool",
      "AllocatedSize", "PhysicalSectorSize", "LogicalSectorSize"]),
    ("磁碟", "root/microsoft/windows/storage", "MSFT_Disk",
     ["Number", "FriendlyName", "SerialNumber", "Size", "PartitionStyle", "IsBoot",
      "IsSystem", "IsReadOnly", "IsOffline", "BusType", "HealthStatus", "Location"]),
    ("磁碟機（舊介面）", "root/cimv2", "Win32_DiskDrive",
     ["Model", "SerialNumber", "FirmwareRevision", "InterfaceType", "Size",
      "Partitions", "MediaType", "PNPDeviceID", "Status", "CapabilityDescriptions"]),
    ("磁碟區", "root/microsoft/windows/storage", "MSFT_Volume",
     ["DriveLetter", "FileSystemLabel", "FileSystem", "FileSystemType", "Size",
      "SizeRemaining", "HealthStatus", "DriveType", "AllocationUnitSize"]),
    ("分割", "root/microsoft/windows/storage", "MSFT_Partition",
     ["DiskNumber", "PartitionNumber", "DriveLetter", "Size", "Offset", "Type",
      "IsBoot", "IsSystem", "IsHidden", "GptType", "Guid"]),
    ("顯示卡", "root/cimv2", "Win32_VideoController",
     ["Name", "AdapterCompatibility", "AdapterRAM", "DriverVersion", "DriverDate",
      "VideoProcessor", "PNPDeviceID", "CurrentHorizontalResolution",
      "CurrentVerticalResolution", "CurrentRefreshRate", "Status", "VideoModeDescription"]),
    ("螢幕識別", "root/wmi", "WmiMonitorID",
     ["InstanceName", "ManufacturerName", "ProductCodeID", "SerialNumberID",
      "UserFriendlyName", "WeekOfManufacture", "YearOfManufacture"]),
    ("螢幕尺寸", "root/wmi", "WmiMonitorBasicDisplayParams",
     ["InstanceName", "MaxHorizontalImageSize", "MaxVerticalImageSize", "VideoInputType"]),
    ("螢幕接法", "root/wmi", "WmiMonitorConnectionParams",
     ["InstanceName", "VideoOutputTechnology"]),
    ("風扇", "root/cimv2", "Win32_Fan",
     ["Name", "DeviceID", "Status", "ActiveCooling", "VariableSpeed", "DesiredSpeed"]),
    ("溫度探針", "root/cimv2", "Win32_TemperatureProbe",
     ["Name", "DeviceID", "Status", "CurrentReading", "MaxReadable", "MinReadable"]),
    ("USB 控制器", "root/cimv2", "Win32_USBController",
     ["Name", "Manufacturer", "DeviceID", "Status", "ProtocolSupported"]),
    ("音效裝置", "root/cimv2", "Win32_SoundDevice",
     ["Name", "Manufacturer", "DeviceID", "Status"]),
    ("指標裝置", "root/cimv2", "Win32_PointingDevice",
     ["Name", "Manufacturer", "DeviceID", "HardwareType", "NumberOfButtons", "Status"]),
    ("鍵盤", "root/cimv2", "Win32_Keyboard",
     ["Name", "Description", "DeviceID", "NumberOfFunctionKeys", "Status"]),
    ("作業系統", "root/cimv2", "Win32_OperatingSystem",
     ["Caption", "Version", "BuildNumber", "OSArchitecture", "InstallDate",
      "LastBootUpTime", "SerialNumber", "RegisteredUser", "TotalVisibleMemorySize",
      "FreePhysicalMemory", "SystemDrive"]),
    ("虛擬化式安全", "root/microsoft/windows/deviceguard", "Win32_DeviceGuard",
     ["AvailableSecurityProperties", "SecurityServicesConfigured", "SecurityServicesRunning",
      "VirtualizationBasedSecurityStatus", "CodeIntegrityPolicyEnforcementStatus"]),
    ("防毒", "root/securitycenter2", "AntiVirusProduct",
     ["displayName", "productState", "pathToSignedProductExe", "timestamp"]),
]


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    for title, namespace, name, fields in SECTIONS:
        ok, rows = cim.query(name, namespace, fields)
        print(f"\n=== {title}　{namespace}/{name} ===")
        if not ok:
            print(f"    取不到：{rows[:80]}")
            continue
        for index, row in enumerate(rows):
            filled = cim.clean(row)
            print(f"  [{index}] 有值 {len(filled)}/{len(fields)}")
            for key in fields:
                value = row.get(key)
                mark = " " if key in filled else "·"
                print(f"    {mark} {key:38} {value}")


if __name__ == "__main__":
    main()
