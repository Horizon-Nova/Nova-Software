"""哪個軟體正在用哪個裝置：控制代碼表的實測涵蓋率。

只解析檔案型物件——型別索引由第一輪取樣自外部取得，不寫死。
"""

import collections
import ctypes
import sys
import time
from ctypes import wintypes

import handles


def file_type_index(entries, opened):
    """File 物件的型別索引。索引值在不同 Windows 版本不同，故當場取得而非寫死。"""
    counter = collections.Counter()
    for entry in entries[:3000]:
        source = opened.get(entry.UniqueProcessId)
        if source is None:
            continue
        duplicated = wintypes.HANDLE()
        ok = handles.kernel.DuplicateHandle(
            source, wintypes.HANDLE(entry.HandleValue), handles.kernel.GetCurrentProcess(),
            ctypes.byref(duplicated), 0, False, handles.DUPLICATE_SAME_ACCESS)
        if not ok:
            continue
        kind = handles.query_with_timeout(duplicated.value, handles.ObjectTypeInformation)
        handles.kernel.CloseHandle(duplicated)
        counter.update({(entry.ObjectTypeIndex, kind): 1})
    files = [index for (index, kind), _ in counter.items() if kind == "File"]
    return files[0] if files else None


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    entries = handles.handle_table()
    by_process = collections.Counter(entry.UniqueProcessId for entry in entries)
    opened = handles.open_processes(list(by_process))
    reachable = [entry for entry in entries if entry.UniqueProcessId in opened]

    print(f"=== 控制代碼 {len(entries)} 個，涉及 {len(by_process)} 個行程 ===")
    print(f"    可開啟的行程 {len(opened)}/{len(by_process)}")
    print(f"    可觸及的控制代碼 {len(reachable)}/{len(entries)}")

    index = file_type_index(reachable, opened)
    print(f"\n=== File 物件的型別索引 = {index} ===")

    files = [entry for entry in reachable if entry.ObjectTypeIndex == index]
    print(f"    可觸及的檔案型控制代碼 {len(files)} 個")

    started = time.time()
    me = handles.kernel.GetCurrentProcess()
    named = []
    for entry in files:
        duplicated = wintypes.HANDLE()
        ok = handles.kernel.DuplicateHandle(
            opened[entry.UniqueProcessId], wintypes.HANDLE(entry.HandleValue), me,
            ctypes.byref(duplicated), 0, False, handles.DUPLICATE_SAME_ACCESS)
        if not ok:
            continue
        name = handles.query_with_timeout(duplicated.value, handles.ObjectNameInformation)
        handles.kernel.CloseHandle(duplicated)
        named.extend((entry.UniqueProcessId, name) for _ in range(1) if name)
    print(f"    解析出名稱 {len(named)} 個，耗時 {time.time() - started:.2f} 秒")

    devices = [(pid, name) for pid, name in named
               if name.startswith("\\Device\\") and not name.startswith("\\Device\\NamedPipe")]
    print(f"\n--- \\Device\\ 底下的物件 {len(devices)} 個，依裝置分組")
    owners = collections.defaultdict(set)
    for pid, name in devices:
        owners[name].add(pid)
    for name, pids in sorted(owners.items(), key=lambda pair: -len(pair[1]))[:24]:
        print(f"    {len(pids):>3} 個行程  {name}")

    print("\n--- 名稱中帶 USB 或 HID 者")
    marked = [(pid, name) for pid, name in named if "USB" in name.upper() or "HID" in name.upper()]
    for pid, name in marked[:20]:
        print(f"    pid={pid:<8} {name}")
    print(f"    （共 {len(marked)} 個）")

    print("\n--- 磁碟區上的檔案（前 12，看得出誰在讀哪一顆碟）")
    volumes = [(pid, name) for pid, name in named if name.startswith("\\Device\\HarddiskVolume")]
    for pid, name in volumes[:12]:
        print(f"    pid={pid:<8} {name[:100]}")
    print(f"    （共 {len(volumes)} 個）")


if __name__ == "__main__":
    main()
