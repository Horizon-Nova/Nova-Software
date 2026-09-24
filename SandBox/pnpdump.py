"""PnP 裝置樹：插了什麼、插在哪、誰的驅動、何時插上、何時拔掉。

Win32_PnPEntity 只有目前在線的裝置且沒有父子關係。
裝置的到達與移除時刻在 DEVPKEY 屬性裡，那是「這台機器上插過什麼」的唯一來源。

逐裝置逐屬性各呼叫一次會是數千次 CIM 往返；改以一次管線取回全部屬性再於本端分組。
屬性以定位字元分隔輸出：三千餘筆的 ConvertTo-Json 會使 PowerShell 以非零碼結束且不留訊息。
"""

import json
import sys

import cim

KEYS = [
    "DEVPKEY_Device_Manufacturer",
    "DEVPKEY_Device_DriverProvider",
    "DEVPKEY_Device_DriverVersion",
    "DEVPKEY_Device_DriverDate",
    "DEVPKEY_Device_Parent",
    "DEVPKEY_Device_ContainerId",
    "DEVPKEY_Device_LocationInfo",
    "DEVPKEY_Device_BusReportedDeviceDesc",
    "DEVPKEY_Device_InstallDate",
    "DEVPKEY_Device_FirstInstallDate",
    "DEVPKEY_Device_LastArrivalDate",
    "DEVPKEY_Device_LastRemovalDate",
    "DEVPKEY_Device_ProblemCode",
    "DEVPKEY_Device_Removable",
]

DEVICE_SCRIPT = """
Get-PnpDevice | ForEach-Object {
  ($_.InstanceId, $_.FriendlyName, $_.Class, $_.Status, $_.Present, $_.Problem) -join "`t"
}
"""

PROPERTY_SCRIPT = """
$ErrorActionPreference='Continue'
$keys = @(#KEYS#)
Get-PnpDevice | Get-PnpDeviceProperty -KeyName $keys -ErrorAction SilentlyContinue |
  Where-Object { $null -ne $_.Data -and "$($_.Data)" -ne '' } |
  ForEach-Object { ($_.InstanceId, $_.KeyName, "$($_.Data)") -join "`t" }
exit 0
"""

SUMMARY_SCRIPT = """
$all = Get-PnpDevice
[pscustomobject]@{
  Total = $all.Count
  Present = ($all | Where-Object Present).Count
  Absent = ($all | Where-Object { -not $_.Present }).Count
  Error = ($all | Where-Object { $_.Status -eq 'Error' }).Count
  Usb = ($all | Where-Object { $_.InstanceId -like 'USB\\*' }).Count
  UsbPresent = ($all | Where-Object { $_.InstanceId -like 'USB\\*' -and $_.Present }).Count
  Classes = ($all | Group-Object Class | Sort-Object Count -Descending |
             Select-Object -First 12 @{n='Class';e={$_.Name}}, Count)
} | ConvertTo-Json -Depth 4 -Compress
"""

DEVICE_COLUMNS = ["InstanceId", "FriendlyName", "Class", "Status", "Present", "Problem"]


def lines_of(script):
    ok, out = cim.run(script)
    return (ok, [line.split("\t") for line in out.splitlines() if line.strip()] if ok else out[:300])


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    ok, out = cim.run(SUMMARY_SCRIPT)
    print("=== 裝置樹規模 ===")
    print("   ", out.strip()[:700] if ok else f"失敗 {out[:200]}")

    ok, device_lines = lines_of(DEVICE_SCRIPT)
    if not ok:
        print("裝置清單失敗:", device_lines)
        return

    ok, property_lines = lines_of(PROPERTY_SCRIPT.replace("#KEYS#", ",".join(f"'{k}'" for k in KEYS)))
    if not ok:
        print("屬性取得失敗:", property_lines)
        return
    print(f"\n=== 裝置 {len(device_lines)} 個，屬性 {len(property_lines)} 筆 ===")

    merged = {}
    for parts in device_lines:
        row = dict(zip(DEVICE_COLUMNS, parts))
        merged[row["InstanceId"]] = row
    for parts in property_lines:
        instance, key, value = parts[0], parts[1], "\t".join(parts[2:])
        row = merged.get(instance)
        if row is None:
            continue
        row[key.replace("DEVPKEY_Device_", "").replace("DEVPKEY_", "")] = value

    rows = list(merged.values())
    fields = {}
    for row in rows:
        for key, value in row.items():
            fields[key] = fields.get(key, 0) + (1 if value not in (None, "", "False") else 0)
    print("\n--- 每個屬性的填充率")
    for key, count in sorted(fields.items(), key=lambda pair: -pair[1]):
        print(f"    {key:24} {count:>4}/{len(rows)}")

    removed = sorted((r for r in rows if r.get("LastRemovalDate")),
                     key=lambda r: r["LastRemovalDate"], reverse=True)
    print(f"\n--- 有拔除紀錄的裝置（共 {len(removed)} 個，列出 14 個）")
    for row in removed[:14]:
        print(f"    {row.get('FriendlyName')}")
        print(f"        插上 {row.get('LastArrivalDate')}    拔除 {row.get('LastRemovalDate')}")
        print(f"        {row['InstanceId'][:96]}")

    usb = [r for r in rows if r["InstanceId"].startswith("USB\\") and r.get("Present") == "True"]
    print(f"\n--- 目前在線的 USB 裝置（共 {len(usb)} 個，列出 10 個）")
    for row in usb[:10]:
        print(f"    {row.get('FriendlyName')}")
        print(f"        匯流排回報 {row.get('BusReportedDeviceDesc')}    位置 {row.get('LocationInfo')}")
        print(f"        驅動 {row.get('DriverProvider')} {row.get('DriverVersion')} {row.get('DriverDate')}")
        print(f"        容器 {row.get('ContainerId')}    首次安裝 {row.get('FirstInstallDate')}")
        print(f"        父 {row.get('Parent')}")

    bad = [r for r in rows if r.get("Status") == "Error" or str(r.get("ProblemCode", "0")) != "0"]
    print(f"\n--- 有問題碼的裝置（共 {len(bad)} 個）")
    for row in bad[:12]:
        print(f"    {str(row.get('Status')):8} problem={row.get('ProblemCode')}  {row.get('FriendlyName')}")

    providers = {}
    for row in rows:
        name = row.get("DriverProvider", "（無驅動）")
        providers[name] = providers.get(name, 0) + 1
    print("\n--- 驅動提供者分布（前 12）")
    for name, count in sorted(providers.items(), key=lambda pair: -pair[1])[:12]:
        print(f"    {count:>4}  {name}")

    json.dump(rows, open("pnp.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"\n寫入 pnp.json（{len(rows)} 個裝置）")


if __name__ == "__main__":
    main()
