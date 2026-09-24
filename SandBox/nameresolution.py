"""名稱來源：DNS 快取列舉、快取查詢、反向解析。三者皆免提權。"""

import ctypes
import socket
from ctypes import wintypes

dnsapi = ctypes.WinDLL("dnsapi")
ws2_32 = ctypes.WinDLL("ws2_32")

DNS_TYPE_A = 1
DNS_TYPE_PTR = 12
DNS_TYPE_AAAA = 28
DNS_QUERY_NO_WIRE_QUERY = 0x00000010

NI_NAMEREQD = 0x04
NI_MAXHOST = 1025

# 查詢型別值域。key 為 DNS 協定定義之數值，新增一種型別多一列。
DNS_TYPE = {1: "A", 2: "NS", 5: "CNAME", 6: "SOA", 12: "PTR", 15: "MX",
            16: "TXT", 28: "AAAA", 33: "SRV", 65: "HTTPS"}


class DNS_CACHE_ENTRY(ctypes.Structure):
    pass


DNS_CACHE_ENTRY._fields_ = [
    ("pNext", ctypes.POINTER(DNS_CACHE_ENTRY)),
    ("pszName", wintypes.LPWSTR),
    ("wType", ctypes.c_ushort),
    ("wDataLength", ctypes.c_ushort),
    ("dwFlags", wintypes.DWORD),
]


class DNS_A_DATA(ctypes.Structure):
    _fields_ = [("IpAddress", ctypes.c_ubyte * 4)]


class DNS_AAAA_DATA(ctypes.Structure):
    _fields_ = [("Ip6Address", ctypes.c_ubyte * 16)]


class DNS_PTR_DATA(ctypes.Structure):
    _fields_ = [("pNameHost", wintypes.LPWSTR)]


class DNS_RECORD_DATA(ctypes.Union):
    _fields_ = [("A", DNS_A_DATA), ("AAAA", DNS_AAAA_DATA), ("PTR", DNS_PTR_DATA)]


class DNS_RECORD(ctypes.Structure):
    pass


DNS_RECORD._fields_ = [
    ("pNext", ctypes.POINTER(DNS_RECORD)),
    ("pName", wintypes.LPWSTR),
    ("wType", ctypes.c_ushort),
    ("wDataLength", ctypes.c_ushort),
    ("Flags", wintypes.DWORD),
    ("dwTtl", wintypes.DWORD),
    ("dwReserved", wintypes.DWORD),
    ("Data", DNS_RECORD_DATA),
]


class SOCKADDR_IN(ctypes.Structure):
    _fields_ = [
        ("sin_family", ctypes.c_short),
        ("sin_port", ctypes.c_ushort),
        ("sin_addr", ctypes.c_ubyte * 4),
        ("sin_zero", ctypes.c_ubyte * 8),
    ]


class SOCKADDR_IN6(ctypes.Structure):
    _fields_ = [
        ("sin6_family", ctypes.c_short),
        ("sin6_port", ctypes.c_ushort),
        ("sin6_flowinfo", wintypes.ULONG),
        ("sin6_addr", ctypes.c_ubyte * 16),
        ("sin6_scope_id", wintypes.ULONG),
    ]


def _pick(table, key):
    return [value for k, value in table.items() if k == key]


def cache_entries():
    """列舉 DNS 快取中的名稱。此 API 只給名稱與型別，位址需另行以快取查詢取得。"""
    head = ctypes.POINTER(DNS_CACHE_ENTRY)()
    dnsapi.DnsGetCacheDataTable(ctypes.byref(head))

    result = []
    node = head
    while node:
        e = node.contents
        result.append({
            "name": e.pszName,
            "type": "".join(_pick(DNS_TYPE, e.wType)) or str(e.wType),
            "type_code": e.wType,
        })
        node = e.pNext
    return result


def _records(name, dns_type):
    """對快取發出查詢，不走網路。取不到為空序列。"""
    out = ctypes.POINTER(DNS_RECORD)()
    status = dnsapi.DnsQuery_W(
        ctypes.c_wchar_p(name), ctypes.c_ushort(dns_type),
        wintypes.DWORD(DNS_QUERY_NO_WIRE_QUERY), None,
        ctypes.byref(out), None)

    # 每種記錄型別的取值方式。key 是 DNS 協定定義的型別碼，新增一種型別多一列。
    readers = {
        DNS_TYPE_A: lambda r: socket.inet_ntop(socket.AF_INET, bytes(r.Data.A.IpAddress)),
        DNS_TYPE_AAAA: lambda r: socket.inet_ntop(socket.AF_INET6, bytes(r.Data.AAAA.Ip6Address)),
        DNS_TYPE_PTR: lambda r: r.Data.PTR.pNameHost,
    }

    result = []
    node = out
    while node:
        r = node.contents
        result.extend({
            "name": r.pName,
            "address": read(r),
            "ttl": r.dwTtl,
            "type": "".join(_pick(DNS_TYPE, r.wType)),
        } for read in _pick(readers, r.wType))
        node = r.pNext

    dnsapi.DnsRecordListFree(out, 1)
    return [entry for entry in result for _ in _pick({0: 0}, status)]


def cache_addresses():
    """快取中每個名稱對應的位址。以快取項目自己帶的型別去查——快取同時含正向與反向項目，
    用固定型別查會對 PTR 項目回傳無紀錄。"""
    entries = {(entry["name"], entry["type_code"]) for entry in cache_entries()}
    return [record
            for name, dns_type in entries
            for record in _records(name, dns_type)]


def _sockaddr(address):
    """位址字串轉 sockaddr。家族由位址本身決定，非由呼叫端指定。"""
    builders = {
        socket.AF_INET: lambda packed: (SOCKADDR_IN(
            sin_family=socket.AF_INET, sin_port=0,
            sin_addr=(ctypes.c_ubyte * 4)(*packed)), ctypes.sizeof(SOCKADDR_IN)),
        socket.AF_INET6: lambda packed: (SOCKADDR_IN6(
            sin6_family=socket.AF_INET6, sin6_port=0,
            sin6_addr=(ctypes.c_ubyte * 16)(*packed)), ctypes.sizeof(SOCKADDR_IN6)),
    }
    family = [f for f in builders
              for _ in [1] if _try_pack(f, address) is not None][:1]
    return [builders[f](_try_pack(f, address)) for f in family]


def _try_pack(family, address):
    packed = [socket.inet_pton(family, address)
              for _ in range(1) if _valid(family, address)]
    return packed[0] if packed else None


def _valid(family, address):
    try:
        socket.inet_pton(family, address)
        return True
    except (OSError, ValueError):
        return False


def reverse(address):
    """反向解析。此呼叫可能走網路，有延遲。取不到名稱為空序列。"""
    host = ctypes.create_unicode_buffer(NI_MAXHOST)
    return [host.value
            for sa, size in _sockaddr(address)
            for status in [ws2_32.GetNameInfoW(
                ctypes.byref(sa), size, host, NI_MAXHOST, None, 0, NI_NAMEREQD)]
            if status == 0]
