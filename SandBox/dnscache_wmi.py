"""DNS 快取（WMI MSFT_DNSClientCache）。DnsQuery_W 的 NO_WIRE_QUERY 對快取中的名稱回 9701，
故正向對照只能由此取得。C# 端對應 System.Management 或 Microsoft.Management.Infrastructure。"""

import json
import subprocess

SCRIPT = (
    "Get-DnsClientCache | Select-Object Entry,Name,Type,Data,DataLength,Section,Status,TimeToLive "
    "| ConvertTo-Json -Compress"
)

DNS_TYPE = {1: "A", 2: "NS", 5: "CNAME", 6: "SOA", 12: "PTR", 15: "MX",
            16: "TXT", 28: "AAAA", 33: "SRV", 65: "HTTPS"}


def _pick(table, key):
    return [value for k, value in table.items() if k == key]


def entries():
    """快取中的每一筆紀錄。Entry 是被查詢的名稱，Name 是實際擁有該紀錄的名稱（CNAME 追蹤後）。"""
    out = subprocess.run(
        ["powershell", "-NoProfile", "-NonInteractive", "-Command", SCRIPT],
        capture_output=True, text=True, encoding="utf-8")

    raw = json.loads(out.stdout or "[]")
    rows = raw if isinstance(raw, list) else [raw]

    return [{
        "entry": row["Entry"],
        "name": row["Name"],
        "type": "".join(_pick(DNS_TYPE, row["Type"])) or str(row["Type"]),
        "type_code": row["Type"],
        "data": row["Data"],
        "ttl": row["TimeToLive"],
        "status": row["Status"],
    } for row in rows]


def address_to_name():
    """位址 → 名稱。取被查詢的名稱（entry）而非 CNAME 鏈末端，那才是使用者意圖連的目標。"""
    forward = [row for row in entries() if row["type_code"] in (1, 28)]
    return {row["data"]: row["entry"] for row in forward}
