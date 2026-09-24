"""SetupAPI 裝置樹綁定。綁定形狀與 C# P/Invoke 一對一對應。

裝置的到達與移除時刻、父子關係、容器識別、驅動提供者，都只在 DEVPKEY 屬性裡，
Win32_PnPEntity 沒有這些欄位。

先以 SetupDiGetDevicePropertyKeys 問出該裝置實際擁有哪些屬性，再逐一讀取——
不預先假設某個屬性存在。
"""

import ctypes
from ctypes import wintypes

setup = ctypes.WinDLL("setupapi", use_last_error=True)
config = ctypes.WinDLL("cfgmgr32", use_last_error=True)

CR_SUCCESS = 0
DN_HAS_PROBLEM = 0x00000400
DN_STARTED = 0x00000008
DN_DRIVER_LOADED = 0x00000002
DN_ROOT_ENUMERATED = 0x00000001
DN_REMOVABLE = 0x00004000
DN_DISABLEABLE = 0x00002000

DIGCF_ALLCLASSES = 0x00000004
ERROR_NO_MORE_ITEMS = 259
ERROR_INSUFFICIENT_BUFFER = 122
INVALID_HANDLE_VALUE = ctypes.c_void_p(-1).value

DEVPROP_TYPEMOD_ARRAY = 0x1000
DEVPROP_TYPEMOD_LIST = 0x2000

DEVPROP_TYPE_EMPTY = 0x00
DEVPROP_TYPE_NULL = 0x01
DEVPROP_TYPE_SBYTE = 0x02
DEVPROP_TYPE_BYTE = 0x03
DEVPROP_TYPE_INT16 = 0x04
DEVPROP_TYPE_UINT16 = 0x05
DEVPROP_TYPE_INT32 = 0x06
DEVPROP_TYPE_UINT32 = 0x07
DEVPROP_TYPE_INT64 = 0x08
DEVPROP_TYPE_UINT64 = 0x09
DEVPROP_TYPE_GUID = 0x0D
DEVPROP_TYPE_FILETIME = 0x10
DEVPROP_TYPE_BOOLEAN = 0x11
DEVPROP_TYPE_STRING = 0x12


class GUID(ctypes.Structure):
    _fields_ = [
        ("Data1", wintypes.DWORD),
        ("Data2", wintypes.WORD),
        ("Data3", wintypes.WORD),
        ("Data4", ctypes.c_ubyte * 8),
    ]

    def __str__(self):
        tail = "".join(f"{b:02x}" for b in self.Data4)
        return f"{{{self.Data1:08x}-{self.Data2:04x}-{self.Data3:04x}-{tail[:4]}-{tail[4:]}}}"


class DEVPROPKEY(ctypes.Structure):
    _fields_ = [("fmtid", GUID), ("pid", wintypes.ULONG)]

    def __str__(self):
        return f"{self.fmtid}:{self.pid}"


class SP_DEVINFO_DATA(ctypes.Structure):
    _fields_ = [
        ("cbSize", wintypes.DWORD),
        ("ClassGuid", GUID),
        ("DevInst", wintypes.DWORD),
        ("Reserved", ctypes.POINTER(wintypes.ULONG)),
    ]


def guid(text):
    """由 {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx} 建 GUID。"""
    body = text.strip("{}").replace("-", "")
    tail = bytes.fromhex(body[16:])
    return GUID(int(body[0:8], 16), int(body[8:12], 16), int(body[12:16], 16),
                (ctypes.c_ubyte * 8)(*tail))


def key(fmtid, pid):
    return DEVPROPKEY(guid(fmtid), pid)


# DEVPKEY 常數。名稱即 Windows 的官方名稱，值即其 (fmtid, pid)。
DEVICE = "{a45c254e-df1c-4efd-8020-67d146a850e0}"
NAME = "{b725f130-47ef-101a-a5f1-02608c9eebac}"
DRIVER = "{a8b865dd-2e3d-4094-ad97-e593a70c75d6}"
RELATION = "{4340a6c5-93fa-4706-972c-7b648008a5a7}"
TIMESTAMP = "{83da6326-97a6-4088-9453-a1923f573b29}"
CONTAINER = "{8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c}"
BUSREPORTED = "{540b947e-8b40-45bc-a8a2-6a0b894cbda2}"
INSTANCEID = "{78c34fc8-104a-4aca-9ea4-524d52996e57}"

KNOWN = {
    str(key(INSTANCEID, 256)): "InstanceId",
    str(key(NAME, 10)): "Name",
    str(key(DEVICE, 2)): "DeviceDesc",
    str(key(DEVICE, 3)): "HardwareIds",
    str(key(DEVICE, 4)): "CompatibleIds",
    str(key(DEVICE, 6)): "Service",
    str(key(DEVICE, 9)): "Class",
    str(key(DEVICE, 10)): "ClassGuid",
    str(key(DEVICE, 11)): "Driver",
    str(key(DEVICE, 13)): "Manufacturer",
    str(key(DEVICE, 14)): "FriendlyName",
    str(key(DEVICE, 15)): "LocationInfo",
    str(key(DEVICE, 16)): "PDOName",
    str(key(DEVICE, 18)): "UINumber",
    str(key(DEVICE, 24)): "EnumeratorName",
    str(key(DEVICE, 27)): "DevType",
    str(key(DEVICE, 30)): "Address",
    str(key(DEVICE, 33)): "RemovalPolicy",
    str(key(DEVICE, 36)): "InstallState",
    str(key(DEVICE, 37)): "LocationPaths",
    str(key(DRIVER, 2)): "DriverDate",
    str(key(DRIVER, 3)): "DriverVersion",
    str(key(DRIVER, 5)): "DriverInfPath",
    str(key(DRIVER, 9)): "DriverProvider",
    str(key(DRIVER, 10)): "DriverPropPageProvider",
    str(key(RELATION, 3)): "ProblemCode",
    str(key(RELATION, 5)): "IsPresent",
    str(key(RELATION, 6)): "HasProblem",
    str(key(RELATION, 8)): "Parent",
    str(key(RELATION, 9)): "Children",
    str(key(RELATION, 10)): "Siblings",
    str(key(RELATION, 24)): "ProblemStatus",
    str(key(TIMESTAMP, 100)): "InstallDate",
    str(key(TIMESTAMP, 101)): "FirstInstallDate",
    str(key(TIMESTAMP, 102)): "LastArrivalDate",
    str(key(TIMESTAMP, 103)): "LastRemovalDate",
    str(key(CONTAINER, 2)): "ContainerId",
    str(key(BUSREPORTED, 4)): "BusReportedDeviceDesc",
    str(key(BUSREPORTED, 6)): "SafeRemovalRequired",
}

setup.SetupDiGetClassDevsW.restype = wintypes.HANDLE
setup.SetupDiGetClassDevsW.argtypes = [ctypes.POINTER(GUID), wintypes.LPCWSTR,
                                       wintypes.HWND, wintypes.DWORD]
setup.SetupDiDestroyDeviceInfoList.argtypes = [wintypes.HANDLE]
setup.SetupDiEnumDeviceInfo.argtypes = [wintypes.HANDLE, wintypes.DWORD,
                                        ctypes.POINTER(SP_DEVINFO_DATA)]
setup.SetupDiGetDevicePropertyKeys.argtypes = [
    wintypes.HANDLE, ctypes.POINTER(SP_DEVINFO_DATA), ctypes.POINTER(DEVPROPKEY),
    wintypes.DWORD, ctypes.POINTER(wintypes.DWORD), wintypes.DWORD]
setup.SetupDiGetDevicePropertyW.argtypes = [
    wintypes.HANDLE, ctypes.POINTER(SP_DEVINFO_DATA), ctypes.POINTER(DEVPROPKEY),
    ctypes.POINTER(wintypes.ULONG), ctypes.POINTER(ctypes.c_ubyte), wintypes.DWORD,
    ctypes.POINTER(wintypes.DWORD), wintypes.DWORD]


config.CM_Get_DevNode_Status.argtypes = [ctypes.POINTER(wintypes.ULONG),
                                         ctypes.POINTER(wintypes.ULONG),
                                         wintypes.DWORD, wintypes.ULONG]

# DevNode 狀態旗標。旗標值域由 Windows 定義，新增一個旗標即多一列。
STATUS_FLAGS = {
    DN_ROOT_ENUMERATED: "RootEnumerated",
    DN_DRIVER_LOADED: "DriverLoaded",
    DN_STARTED: "Started",
    DN_DISABLEABLE: "Disableable",
    DN_REMOVABLE: "Removable",
    DN_HAS_PROBLEM: "HasProblem",
}


def _devnode(dev_inst):
    """執行時狀態。屬性存放區只保存靜態屬性，在不在線與問題碼不在其中。"""
    status = wintypes.ULONG()
    problem = wintypes.ULONG()
    code = config.CM_Get_DevNode_Status(ctypes.byref(status), ctypes.byref(problem),
                                        dev_inst, 0)
    return {
        "IsPresent": code == CR_SUCCESS,
        "StatusFlags": [name for bit, name in STATUS_FLAGS.items() if status.value & bit],
        "ProblemCode": problem.value,
        "StatusCode": status.value,
    }


def _filetime(raw):
    """FILETIME 為自 1601-01-01 起的 100 奈秒數。"""
    import datetime

    ticks = int.from_bytes(raw[:8], "little")
    if ticks == 0:
        return None
    epoch = datetime.datetime(1601, 1, 1, tzinfo=datetime.timezone.utc)
    return (epoch + datetime.timedelta(microseconds=ticks // 10)).isoformat()


def _decode(kind, raw):
    """DEVPROP 型別 → Python 值。型別是 Windows 給的，不是本端推測的。"""
    base = kind & ~(DEVPROP_TYPEMOD_ARRAY | DEVPROP_TYPEMOD_LIST)
    listed = bool(kind & DEVPROP_TYPEMOD_LIST)

    if base == DEVPROP_TYPE_STRING and listed:
        text = bytes(raw).decode("utf-16-le", "replace")
        return [part for part in text.split("\x00") if part]
    if base == DEVPROP_TYPE_STRING:
        return bytes(raw).decode("utf-16-le", "replace").rstrip("\x00")
    if base == DEVPROP_TYPE_FILETIME:
        return _filetime(bytes(raw))
    if base == DEVPROP_TYPE_GUID:
        return str(GUID.from_buffer_copy(bytes(raw)))
    if base == DEVPROP_TYPE_BOOLEAN:
        return bytes(raw)[0] != 0
    if base in (DEVPROP_TYPE_BYTE, DEVPROP_TYPE_SBYTE):
        return bytes(raw)[0]
    if base in (DEVPROP_TYPE_INT16, DEVPROP_TYPE_UINT16):
        return int.from_bytes(bytes(raw)[:2], "little", signed=(base == DEVPROP_TYPE_INT16))
    if base in (DEVPROP_TYPE_INT32, DEVPROP_TYPE_UINT32):
        return int.from_bytes(bytes(raw)[:4], "little", signed=(base == DEVPROP_TYPE_INT32))
    if base in (DEVPROP_TYPE_INT64, DEVPROP_TYPE_UINT64):
        return int.from_bytes(bytes(raw)[:8], "little", signed=(base == DEVPROP_TYPE_INT64))
    return bytes(raw).hex()


def _property(handle, info, propkey):
    kind = wintypes.ULONG()
    size = wintypes.DWORD()
    setup.SetupDiGetDevicePropertyW(handle, ctypes.byref(info), ctypes.byref(propkey),
                                    ctypes.byref(kind), None, 0, ctypes.byref(size), 0)
    if size.value == 0:
        return None
    buffer = (ctypes.c_ubyte * size.value)()
    ok = setup.SetupDiGetDevicePropertyW(handle, ctypes.byref(info), ctypes.byref(propkey),
                                         ctypes.byref(kind), buffer, size.value,
                                         ctypes.byref(size), 0)
    if not ok:
        return None
    return _decode(kind.value, buffer)


def devices():
    """列舉全部裝置（含目前不在線者），每個裝置回一個屬性字典。"""
    handle = setup.SetupDiGetClassDevsW(None, None, None, DIGCF_ALLCLASSES)
    if handle == INVALID_HANDLE_VALUE:
        raise ctypes.WinError(ctypes.get_last_error())

    result = []
    index = 0
    info = SP_DEVINFO_DATA()
    info.cbSize = ctypes.sizeof(SP_DEVINFO_DATA)

    while setup.SetupDiEnumDeviceInfo(handle, index, ctypes.byref(info)):
        count = wintypes.DWORD()
        setup.SetupDiGetDevicePropertyKeys(handle, ctypes.byref(info), None, 0,
                                           ctypes.byref(count), 0)
        keys = (DEVPROPKEY * max(count.value, 1))()
        setup.SetupDiGetDevicePropertyKeys(handle, ctypes.byref(info), keys, count.value,
                                           ctypes.byref(count), 0)

        row = {"ClassGuid": str(info.ClassGuid), "DevInst": info.DevInst}
        row.update(_devnode(info.DevInst))
        for slot in range(count.value):
            name = KNOWN.get(str(keys[slot]))
            if name is None or name in ("IsPresent", "ProblemCode"):
                continue
            value = _property(handle, info, keys[slot])
            if value not in (None, "", []):
                row[name] = value
        result.append(row)
        index += 1

    setup.SetupDiDestroyDeviceInfoList(handle)
    return result
