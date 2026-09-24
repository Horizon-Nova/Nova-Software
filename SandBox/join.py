"""研究主題：四個免提權來源能否合併回答「對方是誰」，以及各欄位的填充率。"""

import ipaddress
import psutil

import dnscache_wmi
import iphlpapi as ip
import nameresolution as nr

UNSPECIFIED = ("0.0.0.0", "::")


def _pick(table, key):
    return [value for k, value in table.items() if k == key]


def _process_names():
    return {p.pid: p.info["name"] for p in psutil.process_iter(["name"])}


# 位址性質的判定次序。一個位址可同時符合多項（loopback 亦屬 private），取最先成立者。
# 新增一種性質多一列。
KIND_ORDER = ("loopback", "link_local", "multicast", "private", "public")


def _classify(address):
    """位址的性質。由位址自身的屬性決定，不由呼叫端指定。"""
    parsed = ipaddress.ip_address(address)
    flags = {
        "loopback": parsed.is_loopback,
        "link_local": parsed.is_link_local,
        "multicast": parsed.is_multicast,
        "private": parsed.is_private,
        "public": True,
    }
    return [kind for kind in KIND_ORDER if flags[kind]][0]


def outbound_connections():
    """有遠端對象的連線。UDP 無遠端位址，本研究只看 TCP。"""
    return [c for c in ip.tcp_connections()
            if c["remote_address"] not in UNSPECIFIED]


def build():
    conns = outbound_connections()
    names = _process_names()
    interfaces = {i["interface_index"]: i for i in ip.interfaces()}
    neighbor_mac = {n["address"]: n["link_layer_address"]
                    for n in ip.neighbors() if n["link_layer_address"]}
    local_ip_to_interface = {addr: i["interface_index"]
                             for i in ip.interfaces() for addr in i["ip_addresses"]}
    cached_name = dnscache_wmi.address_to_name()

    return [{
        "process_id": c["process_id"],
        "process_name": "".join(_pick(names, c["process_id"])) or None,
        "protocol": c["protocol"],
        "state": c["state"],
        "local_address": c["local_address"],
        "local_port": c["local_port"],
        "remote_address": c["remote_address"],
        "remote_port": c["remote_port"],
        "remote_kind": _classify(c["remote_address"]),
        "remote_name_cache": "".join(_pick(cached_name, c["remote_address"])) or None,
        "remote_mac": "".join(_pick(neighbor_mac, c["remote_address"])) or None,
        "interface_index": "".join(
            str(x) for x in _pick(local_ip_to_interface, c["local_address"])) or None,
        "interface_name": "".join(
            i["name"] for x in _pick(local_ip_to_interface, c["local_address"])
            for i in _pick(interfaces, x)) or None,
    } for c in conns]


def _rate(rows, field):
    filled = len([r for r in rows if r[field]])
    return "%3d/%-3d  %5.1f%%" % (filled, len(rows), 100.0 * filled / max(len(rows), 1))


def report():
    rows = build()
    print("=== 有遠端對象的 TCP 連線: %d 條 ===" % len(rows))
    print()

    print("--- 欄位填充率（僅輪詢來源，無 ETW、無反向解析）---")
    for field in ("process_name", "remote_name_cache", "remote_mac",
                  "interface_name", "state"):
        print("  %-20s %s" % (field, _rate(rows, field)))
    print()

    kinds = {}
    for r in rows:
        kinds.setdefault(r["remote_kind"], []).append(r)
    print("--- 對方性質分布 ---")
    for kind, rs in sorted(kinds.items(), key=lambda kv: -len(kv[1])):
        print("  %-12s %3d" % (kind, len(rs)))
    print()

    public = [r for r in rows if r["remote_kind"] == "public"]
    named = [r for r in public if r["remote_name_cache"]]
    print("--- 對外連線的網域名稱涵蓋 ---")
    print("  對外連線     %d" % len(public))
    print("  快取查得名稱 %s" % _rate(public, "remote_name_cache"))
    print()

    print("--- 加上反向解析後 ---")
    unnamed = [r for r in public if not r["remote_name_cache"]]
    resolved = [(r, nr.reverse(r["remote_address"])) for r in unnamed]
    got = [(r, n[0]) for r, n in resolved if n]
    print("  反向解析補上 %3d/%-3d" % (len(got), len(unnamed)))
    print("  合計具名     %3d/%-3d  %5.1f%%" % (
        len(named) + len(got), len(public),
        100.0 * (len(named) + len(got)) / max(len(public), 1)))
    print()

    print("--- 實際樣本（對外連線）---")
    for r in public[:15]:
        name = r["remote_name_cache"] or "".join(
            n for rr, n in got if rr is r) or "-"
        print("  %-22s %-16s %s:%-6s %-12s %s" % (
            (r["process_name"] or "?")[:22],
            r["remote_address"][:16] if len(r["remote_address"]) <= 16
            else r["remote_address"][:14] + "..",
            "", r["remote_port"], r["state"][:12], name[:44]))


if __name__ == "__main__":
    report()
