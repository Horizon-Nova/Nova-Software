"""誰開著哪個裝置：系統控制代碼表。

「哪個軟體正在用這個 USB 裝置」不在任何 WMI 類別裡。
唯一的通用來源是控制代碼表——每個行程開著的核心物件，其中檔案型物件的名稱即裝置路徑。

NtQueryObject 對具名管道可能永久阻塞，故以另一條執行緒帶逾時執行。
"""

import ctypes
import threading
from ctypes import wintypes

ntdll = ctypes.WinDLL("ntdll", use_last_error=True)
kernel = ctypes.WinDLL("kernel32", use_last_error=True)

SystemExtendedHandleInformation = 64
ObjectNameInformation = 1
ObjectTypeInformation = 2

STATUS_INFO_LENGTH_MISMATCH = 0xC0000004
PROCESS_DUP_HANDLE = 0x0040
DUPLICATE_SAME_ACCESS = 0x0002


class SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX(ctypes.Structure):
    _fields_ = [
        ("Object", ctypes.c_void_p),
        ("UniqueProcessId", ctypes.c_size_t),
        ("HandleValue", ctypes.c_size_t),
        ("GrantedAccess", wintypes.ULONG),
        ("CreatorBackTraceIndex", wintypes.USHORT),
        ("ObjectTypeIndex", wintypes.USHORT),
        ("HandleAttributes", wintypes.ULONG),
        ("Reserved", wintypes.ULONG),
    ]


class SYSTEM_HANDLE_INFORMATION_EX(ctypes.Structure):
    _fields_ = [
        ("NumberOfHandles", ctypes.c_size_t),
        ("Reserved", ctypes.c_size_t),
        ("Handles", SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX * 1),
    ]


class UNICODE_STRING(ctypes.Structure):
    _fields_ = [("Length", wintypes.USHORT),
                ("MaximumLength", wintypes.USHORT),
                ("Buffer", ctypes.c_void_p)]


kernel.OpenProcess.restype = wintypes.HANDLE
kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
kernel.GetCurrentProcess.restype = wintypes.HANDLE
kernel.DuplicateHandle.argtypes = [wintypes.HANDLE, wintypes.HANDLE, wintypes.HANDLE,
                                   ctypes.POINTER(wintypes.HANDLE), wintypes.DWORD,
                                   wintypes.BOOL, wintypes.DWORD]
kernel.CloseHandle.argtypes = [wintypes.HANDLE]


def handle_table():
    """全系統的控制代碼表。"""
    size = 1 << 22
    while True:
        buffer = ctypes.create_string_buffer(size)
        needed = wintypes.ULONG()
        # NTSTATUS 為無號值；ctypes 預設回傳有號整數，須先遮罩再比對。
        status = ntdll.NtQuerySystemInformation(SystemExtendedHandleInformation,
                                                buffer, size, ctypes.byref(needed)) & 0xFFFFFFFF
        if status == 0:
            break
        if status != STATUS_INFO_LENGTH_MISMATCH:
            raise OSError(f"NtQuerySystemInformation 0x{status:08X}")
        size = max(needed.value, size * 2)

    header = SYSTEM_HANDLE_INFORMATION_EX.from_buffer(buffer)
    count = header.NumberOfHandles
    array = (SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX * count).from_buffer(
        buffer, SYSTEM_HANDLE_INFORMATION_EX.Handles.offset)
    return list(array)


def _query(duplicated, kind, size):
    out = ctypes.create_string_buffer(size)
    length = wintypes.ULONG()
    status = ntdll.NtQueryObject(wintypes.HANDLE(duplicated), kind, out, size,
                                 ctypes.byref(length)) & 0xFFFFFFFF
    return (status, out)


def _text(out):
    unicode_string = UNICODE_STRING.from_buffer_copy(bytes(out)[:ctypes.sizeof(UNICODE_STRING)])
    if unicode_string.Length == 0 or not unicode_string.Buffer:
        return ""
    return ctypes.wstring_at(unicode_string.Buffer, unicode_string.Length // 2)


def query_with_timeout(duplicated, kind, seconds=0.2):
    """NtQueryObject 對具名管道可能永久阻塞，逾時即放棄該個控制代碼。"""
    box = {}

    def run():
        status, out = _query(duplicated, kind, 4096)
        box["value"] = _text(out) if status == 0 else None

    worker = threading.Thread(target=run, daemon=True)
    worker.start()
    worker.join(seconds)
    return box.get("value")


def open_processes(process_ids):
    """能開啟的行程。開不了者不在回傳的字典裡。"""
    opened = {}
    for process_id in process_ids:
        handle = kernel.OpenProcess(PROCESS_DUP_HANDLE, False, process_id)
        opened.update({process_id: handle for _ in range(1) if handle})
    return opened


def names(entries, opened, limit):
    """把控制代碼複製到本行程再問名稱。回 (行程識別, 型別, 名稱) 清單。"""
    me = kernel.GetCurrentProcess()
    result = []

    for entry in entries[:limit]:
        source = opened.get(entry.UniqueProcessId)
        if source is None:
            continue
        duplicated = wintypes.HANDLE()
        ok = kernel.DuplicateHandle(source, wintypes.HANDLE(entry.HandleValue), me,
                                    ctypes.byref(duplicated), 0, False, DUPLICATE_SAME_ACCESS)
        if not ok:
            continue
        kind = query_with_timeout(duplicated.value, ObjectTypeInformation)
        name = query_with_timeout(duplicated.value, ObjectNameInformation)
        kernel.CloseHandle(duplicated)
        result.append((entry.UniqueProcessId, kind, name))
    return result
