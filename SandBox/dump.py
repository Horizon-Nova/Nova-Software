"""把四個來源實際拿到的資料原樣輸出，供檢視。輸出為 UTF-8 檔案，避免主控台編碼干擾。"""

import io
import sys

import dnscache_wmi
import iphlpapi as ip
import join
import nameresolution as nr

out = io.StringIO()


def w(text=""):
    out.write(text + "\n")


def table(rows, columns):
    widths = [max([len(c[0])] + [len(str(r[c[1]] if r[c[1]] is not None else "-"))
                                 for r in rows] or [len(c[0])]) for c in columns]
    w("  " + "  ".join(c[0].ljust(widths[i]) for i, c in enumerate(columns)))
    w("  " + "  ".join("-" * widths[i] for i in range(len(columns))))
    for r in rows:
        w("  " + "  ".join(
            str(r[c[1]] if r[c[1]] is not None else "-").ljust(widths[i])
            for i, c in enumerate(columns)))


w("=" * 100)
w("一　連線表　GetExtendedTcpTable / GetExtendedUdpTable")
w("=" * 100)
tcp = ip.tcp_connections()
udp = ip.udp_endpoints()
w("TCP %d 條、UDP %d 個端點" % (len(tcp), len(udp)))
w()
w("[TCP 全欄位，有遠端對象者]")
outbound = [c for c in tcp if c["remote_address"] not in ("0.0.0.0", "::")]
table(outbound, [("protocol", "protocol"), ("local_address", "local_address"),
                 ("local_port", "local_port"), ("remote_address", "remote_address"),
                 ("remote_port", "remote_port"), ("state", "state"),
                 ("process_id", "process_id")])
w()
w("[TCP 監聽中]")
listening = [c for c in tcp if c["state"] == "Listen"][:20]
table(listening, [("local_address", "local_address"), ("local_port", "local_port"),
                  ("state", "state"), ("process_id", "process_id")])
w()
w("[UDP 端點，前 20]")
table(udp[:20], [("local_address", "local_address"), ("local_port", "local_port"),
                 ("process_id", "process_id")])

w()
w("=" * 100)
w("二　芳鄰表　GetIpNetTable2")
w("=" * 100)
nb = ip.neighbors()
real = [n for n in nb if n["link_layer_address"]
        and n["link_layer_address"] != "00:00:00:00:00:00"]
w("總計 %d 筆，其中有實體位址且非全零者 %d 筆" % (len(nb), len(real)))
w()
table(real[:30], [("address", "address"), ("link_layer_address", "link_layer_address"),
                  ("state", "state"), ("interface_index", "interface_index"),
                  ("is_router", "is_router")])

w()
w("=" * 100)
w("三　介面表　GetAdaptersAddresses")
w("=" * 100)
for i in ip.interfaces():
    w("[%s] %s" % (i["interface_index"], i["name"]))
    w("    description          %s" % i["description"])
    w("    link_layer_address   %s" % i["link_layer_address"])
    w("    type                 %s" % i["type"])
    w("    status               %s" % i["status"])
    w("    mtu                  %s" % i["mtu"])
    w("    transmit_link_speed  %s" % i["transmit_link_speed"])
    w("    receive_link_speed   %s" % i["receive_link_speed"])
    w("    dns_suffix           %s" % (i["dns_suffix"] or "-"))
    w("    ip_addresses         %s" % (", ".join(i["ip_addresses"]) or "-"))
    w("    gateways             %s" % (", ".join(i["gateways"]) or "-"))
    w("    dns_servers          %s" % (", ".join(i["dns_servers"]) or "-"))
    w()

w("=" * 100)
w("四　DNS 快取　MSFT_DNSClientCache")
w("=" * 100)
cache = dnscache_wmi.entries()
w("總計 %d 筆" % len(cache))
w()
table(cache, [("entry", "entry"), ("name", "name"), ("type", "type"),
              ("data", "data"), ("ttl", "ttl")])

w()
w("=" * 100)
w("五　合併結果：對方是誰")
w("=" * 100)
rows = join.build()
public = [r for r in rows if r["remote_kind"] == "public"]
w("有遠端對象 %d 條，其中對外 %d 條" % (len(rows), len(public)))
w()
for r in public:
    r["reverse"] = "".join(nr.reverse(r["remote_address"]))
    r["name"] = r["remote_name_cache"] or r["reverse"] or None
table(public, [("process", "process_name"), ("pid", "process_id"),
               ("local", "local_address"), ("lport", "local_port"),
               ("remote", "remote_address"), ("rport", "remote_port"),
               ("state", "state"), ("iface", "interface_name"),
               ("mac", "remote_mac"), ("name(cache)", "remote_name_cache"),
               ("name(reverse)", "reverse")])

w()
w("[本機端內部連線]")
loopback = [r for r in rows if r["remote_kind"] == "loopback"]
table(loopback, [("process", "process_name"), ("pid", "process_id"),
                 ("local", "local_address"), ("lport", "local_port"),
                 ("remote", "remote_address"), ("rport", "remote_port"),
                 ("state", "state")])

with open("dump.txt", "w", encoding="utf-8") as f:
    f.write(out.getvalue())
print("written: dump.txt  (%d lines)" % out.getvalue().count("\n"))
