"""IPHLPAPI 連線表、芳鄰表、介面表。ctypes 綁定與 C# P/Invoke 一對一對應，研究結果可直接轉寫。"""

import ctypes
import socket
from ctypes import wintypes

iphlpapi = ctypes.WinDLL("iphlpapi")
ws2_32 = ctypes.WinDLL("ws2_32")

AF_INET = 2
AF_INET6 = 23
AF_UNSPEC = 0

TCP_TABLE_OWNER_PID_ALL = 5
UDP_TABLE_OWNER_PID = 1

ERROR_INSUFFICIENT_BUFFER = 122

# 連線狀態值域。key 為作業系統回傳之數值，新增一種狀態多一列。
TCP_STATE = {
    1: "Closed", 2: "Listen", 3: "SynSent", 4: "SynReceived", 5: "Established",
    6: "FinWait1", 7: "FinWait2", 8: "CloseWait", 9: "Closing", 10: "LastAck",
    11: "TimeWait", 12: "DeleteTcb",
}

# 芳鄰狀態值域。
NEIGHBOR_STATE = {
    0: "Unreachable", 1: "Incomplete", 2: "Probe", 3: "Delay",
    4: "Stale", 5: "Reachable", 6: "Permanent", 7: "Maximum",
}

IF_OPER_STATUS = {
    1: "Up", 2: "Down", 3: "Testing", 4: "Unknown", 5: "Dormant",
    6: "NotPresent", 7: "LowerLayerDown",
}

IF_TYPE = {
    1: "Other", 6: "Ethernet", 24: "Loopback", 71: "Wireless80211",
    131: "Tunnel", 144: "IEEE1394", 237: "IEEE80216WMAN", 243: "WwanPP",
}


class MIB_TCPROW_OWNER_PID(ctypes.Structure):
    _fields_ = [
        ("dwState", wintypes.DWORD),
        ("dwLocalAddr", wintypes.DWORD),
        ("dwLocalPort", wintypes.DWORD),
        ("dwRemoteAddr", wintypes.DWORD),
        ("dwRemotePort", wintypes.DWORD),
        ("dwOwningPid", wintypes.DWORD),
    ]


class MIB_TCP6ROW_OWNER_PID(ctypes.Structure):
    _fields_ = [
        ("ucLocalAddr", ctypes.c_ubyte * 16),
        ("dwLocalScopeId", wintypes.DWORD),
        ("dwLocalPort", wintypes.DWORD),
        ("ucRemoteAddr", ctypes.c_ubyte * 16),
        ("dwRemoteScopeId", wintypes.DWORD),
        ("dwRemotePort", wintypes.DWORD),
        ("dwState", wintypes.DWORD),
        ("dwOwningPid", wintypes.DWORD),
    ]


class MIB_UDPROW_OWNER_PID(ctypes.Structure):
    _fields_ = [
        ("dwLocalAddr", wintypes.DWORD),
        ("dwLocalPort", wintypes.DWORD),
        ("dwOwningPid", wintypes.DWORD),
    ]


class MIB_UDP6ROW_OWNER_PID(ctypes.Structure):
    _fields_ = [
        ("ucLocalAddr", ctypes.c_ubyte * 16),
        ("dwLocalScopeId", wintypes.DWORD),
        ("dwLocalPort", wintypes.DWORD),
        ("dwOwningPid", wintypes.DWORD),
    ]


class SOCKADDR_IN(ctypes.Structure):
    _fields_ = [
        ("sin_family", ctypes.c_ushort),
        ("sin_port", ctypes.c_ushort),
        ("sin_addr", ctypes.c_ubyte * 4),
        ("sin_zero", ctypes.c_ubyte * 8),
    ]


class SOCKADDR_IN6(ctypes.Structure):
    _fields_ = [
        ("sin6_family", ctypes.c_ushort),
        ("sin6_port", ctypes.c_ushort),
        ("sin6_flowinfo", wintypes.ULONG),
        ("sin6_addr", ctypes.c_ubyte * 16),
        ("sin6_scope_id", wintypes.ULONG),
    ]


class SOCKADDR_INET(ctypes.Union):
    _fields_ = [
        ("Ipv4", SOCKADDR_IN),
        ("Ipv6", SOCKADDR_IN6),
        ("si_family", ctypes.c_ushort),
    ]


class MIB_IPNET_ROW2(ctypes.Structure):
    _fields_ = [
        ("Address", SOCKADDR_INET),
        ("InterfaceIndex", wintypes.ULONG),
        ("InterfaceLuid", ctypes.c_ulonglong),
        ("PhysicalAddress", ctypes.c_ubyte * 32),
        ("PhysicalAddressLength", wintypes.ULONG),
        ("State", ctypes.c_int),
        ("Flags", ctypes.c_ubyte),
        ("ReachabilityTime", wintypes.ULONG),
    ]


class MIB_IPNET_TABLE2(ctypes.Structure):
    _fields_ = [
        ("NumEntries", wintypes.ULONG),
        ("Table", MIB_IPNET_ROW2 * 1),
    ]


class SOCKET_ADDRESS(ctypes.Structure):
    _fields_ = [
        ("lpSockaddr", ctypes.POINTER(SOCKADDR_INET)),
        ("iSockaddrLength", ctypes.c_int),
    ]


class IP_ADAPTER_UNICAST_ADDRESS(ctypes.Structure):
    pass


IP_ADAPTER_UNICAST_ADDRESS._fields_ = [
    ("Length", wintypes.ULONG),
    ("Flags", wintypes.DWORD),
    ("Next", ctypes.POINTER(IP_ADAPTER_UNICAST_ADDRESS)),
    ("Address", SOCKET_ADDRESS),
    ("PrefixOrigin", ctypes.c_int),
    ("SuffixOrigin", ctypes.c_int),
    ("DadState", ctypes.c_int),
    ("ValidLifetime", wintypes.ULONG),
    ("PreferredLifetime", wintypes.ULONG),
    ("LeaseLifetime", wintypes.ULONG),
    ("OnLinkPrefixLength", ctypes.c_ubyte),
]


class IP_ADAPTER_ADDRESS_COMMON(ctypes.Structure):
    pass


IP_ADAPTER_ADDRESS_COMMON._fields_ = [
    ("Length", wintypes.ULONG),
    ("Flags", wintypes.DWORD),
    ("Next", ctypes.POINTER(IP_ADAPTER_ADDRESS_COMMON)),
    ("Address", SOCKET_ADDRESS),
]


class IP_ADAPTER_ADDRESSES(ctypes.Structure):
    pass


IP_ADAPTER_ADDRESSES._fields_ = [
    ("Length", wintypes.ULONG),
    ("IfIndex", wintypes.DWORD),
    ("Next", ctypes.POINTER(IP_ADAPTER_ADDRESSES)),
    ("AdapterName", ctypes.c_char_p),
    ("FirstUnicastAddress", ctypes.POINTER(IP_ADAPTER_UNICAST_ADDRESS)),
    ("FirstAnycastAddress", ctypes.POINTER(IP_ADAPTER_ADDRESS_COMMON)),
    ("FirstMulticastAddress", ctypes.POINTER(IP_ADAPTER_ADDRESS_COMMON)),
    ("FirstDnsServerAddress", ctypes.POINTER(IP_ADAPTER_ADDRESS_COMMON)),
    ("DnsSuffix", wintypes.LPWSTR),
    ("Description", wintypes.LPWSTR),
    ("FriendlyName", wintypes.LPWSTR),
    ("PhysicalAddress", ctypes.c_ubyte * 8),
    ("PhysicalAddressLength", wintypes.ULONG),
    ("Flags", wintypes.DWORD),
    ("Mtu", wintypes.ULONG),
    ("IfType", wintypes.DWORD),
    ("OperStatus", ctypes.c_int),
    ("Ipv6IfIndex", wintypes.DWORD),
    ("ZoneIndices", wintypes.DWORD * 16),
    ("FirstPrefix", ctypes.c_void_p),
    ("TransmitLinkSpeed", ctypes.c_ulonglong),
    ("ReceiveLinkSpeed", ctypes.c_ulonglong),
    ("FirstWinsServerAddress", ctypes.c_void_p),
    ("FirstGatewayAddress", ctypes.POINTER(IP_ADAPTER_ADDRESS_COMMON)),
    ("Ipv4Metric", wintypes.ULONG),
    ("Ipv6Metric", wintypes.ULONG),
    ("Luid", ctypes.c_ulonglong),
    ("Dhcpv4Server", SOCKET_ADDRESS),
    ("CompartmentId", wintypes.DWORD),
    ("NetworkGuid", ctypes.c_ubyte * 16),
    ("ConnectionType", ctypes.c_int),
    ("TunnelType", ctypes.c_int),
    ("Dhcpv6Server", SOCKET_ADDRESS),
    ("Dhcpv6ClientDuid", ctypes.c_ubyte * 130),
    ("Dhcpv6ClientDuidLength", wintypes.ULONG),
    ("Dhcpv6Iaid", wintypes.ULONG),
    ("FirstDnsSuffix", ctypes.c_void_p),
]


def _port(raw):
    """連接埠在表中為網路位元組序，取低 16 位元後轉主機序。"""
    return socket.ntohs(raw & 0xFFFF)


def _ipv4(raw):
    return socket.inet_ntop(socket.AF_INET, raw.to_bytes(4, "little"))


def _ipv6(buf):
    return socket.inet_ntop(socket.AF_INET6, bytes(buf))


def _mac(buf, length):
    return ":".join("%02X" % b for b in buf[:length])


def _pick(table, key):
    """以 key 取值，取不到為空序列。"""
    return [value for k, value in table.items() if k == key]


def _label(table, key):
    return "".join(_pick(table, key)) or str(key)


def _call_table(fn, family, table_class, row_type):
    """兩段式呼叫：先問長度，再取內容。回傳 (筆數, 位元組緩衝區)。"""
    size = wintypes.DWORD(0)
    fn(None, ctypes.byref(size), False, family, table_class, 0)
    buf = ctypes.create_string_buffer(size.value)
    fn(buf, ctypes.byref(size), False, family, table_class, 0)
    count = ctypes.cast(buf, ctypes.POINTER(wintypes.DWORD))[0]
    rows = ctypes.cast(
        ctypes.byref(buf, ctypes.sizeof(wintypes.DWORD)),
        ctypes.POINTER(row_type),
    )
    return count, rows


def tcp_connections():
    """TCP 連線清單。v4 與 v6 為同一種列，差別僅在位址取法。"""
    count4, rows4 = _call_table(
        iphlpapi.GetExtendedTcpTable, AF_INET, TCP_TABLE_OWNER_PID_ALL, MIB_TCPROW_OWNER_PID)
    v4 = [{
        "protocol": "TCP",
        "local_address": _ipv4(rows4[i].dwLocalAddr),
        "local_port": _port(rows4[i].dwLocalPort),
        "remote_address": _ipv4(rows4[i].dwRemoteAddr),
        "remote_port": _port(rows4[i].dwRemotePort),
        "state": _label(TCP_STATE, rows4[i].dwState),
        "process_id": rows4[i].dwOwningPid,
    } for i in range(count4)]

    count6, rows6 = _call_table(
        iphlpapi.GetExtendedTcpTable, AF_INET6, TCP_TABLE_OWNER_PID_ALL, MIB_TCP6ROW_OWNER_PID)
    v6 = [{
        "protocol": "TCP",
        "local_address": _ipv6(rows6[i].ucLocalAddr),
        "local_port": _port(rows6[i].dwLocalPort),
        "remote_address": _ipv6(rows6[i].ucRemoteAddr),
        "remote_port": _port(rows6[i].dwRemotePort),
        "state": _label(TCP_STATE, rows6[i].dwState),
        "process_id": rows6[i].dwOwningPid,
    } for i in range(count6)]

    return v4 + v6


def udp_endpoints():
    """UDP 端點清單。UDP 無連線狀態與遠端位址。"""
    count4, rows4 = _call_table(
        iphlpapi.GetExtendedUdpTable, AF_INET, UDP_TABLE_OWNER_PID, MIB_UDPROW_OWNER_PID)
    v4 = [{
        "protocol": "UDP",
        "local_address": _ipv4(rows4[i].dwLocalAddr),
        "local_port": _port(rows4[i].dwLocalPort),
        "remote_address": None,
        "remote_port": None,
        "state": None,
        "process_id": rows4[i].dwOwningPid,
    } for i in range(count4)]

    count6, rows6 = _call_table(
        iphlpapi.GetExtendedUdpTable, AF_INET6, UDP_TABLE_OWNER_PID, MIB_UDP6ROW_OWNER_PID)
    v6 = [{
        "protocol": "UDP",
        "local_address": _ipv6(rows6[i].ucLocalAddr),
        "local_port": _port(rows6[i].dwLocalPort),
        "remote_address": None,
        "remote_port": None,
        "state": None,
        "process_id": rows6[i].dwOwningPid,
    } for i in range(count6)]

    return v4 + v6


def neighbors():
    """芳鄰表。同網段對方的實體位址由此而來。"""
    table = ctypes.POINTER(MIB_IPNET_TABLE2)()
    iphlpapi.GetIpNetTable2(AF_UNSPEC, ctypes.byref(table))

    count = table.contents.NumEntries
    rows = ctypes.cast(
        ctypes.byref(table.contents, MIB_IPNET_TABLE2.Table.offset),
        ctypes.POINTER(MIB_IPNET_ROW2),
    )

    family_address = {
        AF_INET: lambda row: socket.inet_ntop(socket.AF_INET, bytes(row.Address.Ipv4.sin_addr)),
        AF_INET6: lambda row: _ipv6(row.Address.Ipv6.sin6_addr),
    }

    result = [{
        "address": to_address(rows[i]),
        "link_layer_address": _mac(rows[i].PhysicalAddress, rows[i].PhysicalAddressLength),
        "state": _label(NEIGHBOR_STATE, rows[i].State),
        "interface_index": rows[i].InterfaceIndex,
        "is_router": bool(rows[i].Flags & 1),
    } for i in range(count)
        for to_address in _pick(family_address, rows[i].Address.si_family)]

    iphlpapi.FreeMibTable(table)
    return result


def _address_list(head):
    """走訪 SOCKET_ADDRESS 鏈結串列，取出位址字串。"""
    families = {
        AF_INET: lambda sa: socket.inet_ntop(socket.AF_INET, bytes(sa.contents.Ipv4.sin_addr)),
        AF_INET6: lambda sa: socket.inet_ntop(socket.AF_INET6, bytes(sa.contents.Ipv6.sin6_addr)),
    }
    result = []
    node = head
    while node:
        sa = node.contents.Address.lpSockaddr
        result.extend(fn(sa) for fn in _pick(families, sa.contents.si_family))
        node = node.contents.Next
    return result


def interfaces():
    """本機網路裝置。"""
    size = wintypes.ULONG(0)
    flags = 0x0010 | 0x0080  # SKIP_ANYCAST | INCLUDE_GATEWAYS
    iphlpapi.GetAdaptersAddresses(AF_UNSPEC, flags, None, None, ctypes.byref(size))
    buf = ctypes.create_string_buffer(size.value)
    iphlpapi.GetAdaptersAddresses(AF_UNSPEC, flags, None, buf, ctypes.byref(size))

    result = []
    node = ctypes.cast(buf, ctypes.POINTER(IP_ADAPTER_ADDRESSES))
    while node:
        a = node.contents
        result.append({
            "interface_index": a.IfIndex,
            "name": a.FriendlyName,
            "description": a.Description,
            "link_layer_address": _mac(a.PhysicalAddress, a.PhysicalAddressLength),
            "type": _label(IF_TYPE, a.IfType),
            "status": _label(IF_OPER_STATUS, a.OperStatus),
            "mtu": a.Mtu,
            "transmit_link_speed": a.TransmitLinkSpeed,
            "receive_link_speed": a.ReceiveLinkSpeed,
            "dns_suffix": a.DnsSuffix,
            "ip_addresses": _address_list(a.FirstUnicastAddress),
            "gateways": _address_list(a.FirstGatewayAddress),
            "dns_servers": _address_list(a.FirstDnsServerAddress),
        })
        node = a.Next
    return result
