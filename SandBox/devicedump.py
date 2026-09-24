"""裝置樹實測值：插了什麼、插在哪、誰的驅動、何時插上、何時拔掉。"""

import json
import sys
import time

import setupapi


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    started = time.time()
    rows = setupapi.devices()
    elapsed = time.time() - started

    present = [r for r in rows if r.get("IsPresent")]
    print(f"=== 裝置 {len(rows)} 個（在線 {len(present)}、不在線 {len(rows) - len(present)}），耗時 {elapsed:.2f} 秒 ===")

    fields = {}
    for row in rows:
        for name in row:
            fields[name] = fields.get(name, 0) + 1
    print("\n--- 每個屬性的填充率")
    for name, count in sorted(fields.items(), key=lambda pair: -pair[1]):
        print(f"    {name:24} {count:>4}/{len(rows)}")

    print("\n--- 列舉器分布")
    buckets = {}
    for row in rows:
        buckets[row.get("EnumeratorName", "（無）")] = buckets.get(row.get("EnumeratorName", "（無）"), 0) + 1
    for name, count in sorted(buckets.items(), key=lambda pair: -pair[1])[:14]:
        print(f"    {count:>4}  {name}")

    removed = sorted((r for r in rows if r.get("LastRemovalDate")),
                     key=lambda r: r["LastRemovalDate"], reverse=True)
    print(f"\n--- 有拔除紀錄的裝置（共 {len(removed)} 個，最近 12 個）")
    for row in removed[:12]:
        print(f"    {row.get('FriendlyName') or row.get('DeviceDesc')}")
        print(f"        插上 {row.get('LastArrivalDate')}")
        print(f"        拔除 {row.get('LastRemovalDate')}")
        print(f"        {str(row.get('InstanceId'))[:88]}")

    usb = [r for r in rows if str(r.get("EnumeratorName")) == "USB" and r.get("IsPresent")]
    print(f"\n--- 目前在線的 USB 裝置（共 {len(usb)} 個，列出 10 個）")
    for row in usb[:10]:
        print(f"    {row.get('FriendlyName') or row.get('DeviceDesc')}")
        print(f"        匯流排回報 {row.get('BusReportedDeviceDesc')}")
        print(f"        硬體識別 {row.get('HardwareIds')}")
        print(f"        位置 {row.get('LocationInfo')}   位置路徑 {row.get('LocationPaths')}")
        print(f"        驅動 {row.get('DriverProvider')} {row.get('DriverVersion')} {row.get('DriverDate')}")
        print(f"        服務 {row.get('Service')}   INF {row.get('DriverInfPath')}")
        print(f"        容器 {row.get('ContainerId')}")
        print(f"        父 {row.get('Parent')}")
        print(f"        首次安裝 {row.get('FirstInstallDate')}   最近插上 {row.get('LastArrivalDate')}")

    bad = [r for r in rows if r.get("HasProblem")]
    print(f"\n--- 有問題碼的裝置（共 {len(bad)} 個）")
    for row in bad[:12]:
        print(f"    problem={row.get('ProblemCode')}  {row.get('FriendlyName') or row.get('DeviceDesc')}")
        print(f"        {str(row.get('InstanceId'))[:88]}")

    providers = {}
    for row in rows:
        providers[row.get("DriverProvider", "（無驅動）")] = providers.get(row.get("DriverProvider", "（無驅動）"), 0) + 1
    print("\n--- 驅動提供者分布（前 12）")
    for name, count in sorted(providers.items(), key=lambda pair: -pair[1])[:12]:
        print(f"    {count:>4}  {name}")

    print("\n--- 可安全移除的裝置（外接判定的來源）")
    removable = [r for r in rows if r.get("SafeRemovalRequired") or str(r.get("RemovalPolicy")) in ("2", "3")]
    for row in removable[:12]:
        print(f"    RemovalPolicy={row.get('RemovalPolicy')} Safe={row.get('SafeRemovalRequired')} "
              f"{row.get('FriendlyName') or row.get('DeviceDesc')}")
    print(f"    （共 {len(removable)} 個）")

    json.dump(rows, open("devices.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"\n寫入 devices.json（{len(rows)} 個裝置）")


if __name__ == "__main__":
    main()
