"""把各來源實際拿到的資料輸出為 JSON。反向解析並行處理，避免序列呼叫拖長取樣。"""

import json
import subprocess
from concurrent.futures import ThreadPoolExecutor

import dnscache_wmi
import iphlpapi as ip
import join
import nameresolution as nr

CHANNEL_SCRIPT = (
    "Get-WinEvent -ListLog * -ErrorAction SilentlyContinue | "
    "Where-Object { $_.LogName -match 'DNS|Firewall|Winsock|TCPIP|NetworkProfile|WFP|Filtering' } | "
    "Select-Object LogName,IsEnabled,RecordCount,LogMode,MaximumSizeInBytes | "
    "ConvertTo-Json -Compress"
)


def _powershell(script):
    out = subprocess.run(
        ["powershell", "-NoProfile", "-NonInteractive", "-Command", script],
        capture_output=True, text=True, encoding="utf-8")
    raw = json.loads(out.stdout or "[]")
    return raw if isinstance(raw, list) else [raw]


def log_channels():
    return [{
        "log_name": row["LogName"],
        "enabled": row["IsEnabled"],
        "record_count": row["RecordCount"],
        "log_mode": row["LogMode"],
        "max_size_bytes": row["MaximumSizeInBytes"],
    } for row in _powershell(CHANNEL_SCRIPT)]


def merged():
    """合併結果，反向解析以執行緒池並行。"""
    rows = join.build()
    addresses = sorted({r["remote_address"] for r in rows})

    with ThreadPoolExecutor(max_workers=32) as pool:
        resolved = dict(zip(addresses, pool.map(nr.reverse, addresses)))

    return [dict(r,
                 remote_name_reverse="".join(resolved[r["remote_address"]]) or None,
                 remote_name=(r["remote_name_cache"]
                              or "".join(resolved[r["remote_address"]]) or None))
            for r in rows]


payload = {
    "connections_tcp": ip.tcp_connections(),
    "endpoints_udp": ip.udp_endpoints(),
    "neighbors": ip.neighbors(),
    "interfaces": ip.interfaces(),
    "dns_cache": dnscache_wmi.entries(),
    "log_channels": log_channels(),
    "merged": merged(),
}

payload["summary"] = {
    "connections_tcp": len(payload["connections_tcp"]),
    "endpoints_udp": len(payload["endpoints_udp"]),
    "neighbors_total": len(payload["neighbors"]),
    "neighbors_with_mac": len([n for n in payload["neighbors"]
                               if n["link_layer_address"]
                               not in ("", "00:00:00:00:00:00")]),
    "interfaces": len(payload["interfaces"]),
    "dns_cache": len(payload["dns_cache"]),
    "merged_total": len(payload["merged"]),
    "merged_public": len([r for r in payload["merged"] if r["remote_kind"] == "public"]),
    "merged_public_named": len([r for r in payload["merged"]
                                if r["remote_kind"] == "public" and r["remote_name"]]),
    "merged_public_with_mac": len([r for r in payload["merged"]
                                   if r["remote_kind"] == "public" and r["remote_mac"]]),
    "merged_no_owner": len([r for r in payload["merged"] if r["process_id"] == 0]),
}

with open("dump.json", "w", encoding="utf-8") as f:
    json.dump(payload, f, ensure_ascii=False, indent=2)

print(json.dumps(payload["summary"], ensure_ascii=False, indent=2))
