"""對每一條連線回答兩件事：對方是誰、本機端是什麼。用盡所有已驗證的取得方式。"""

import json
import socket
import ssl
import subprocess
from concurrent.futures import ThreadPoolExecutor

from cryptography import x509
from cryptography.x509.oid import NameOID, ExtensionOID

import dnscache_wmi
import iphlpapi as ip
import nameresolution as nr

UNSPECIFIED = ("0.0.0.0", "::")
MULTICAST_PREFIX = ("01:00:5E", "33:33", "FF:FF:FF")

PROCESS_SCRIPT = (
    "Get-CimInstance Win32_Process | "
    "Select-Object ProcessId,Name,ExecutablePath,CommandLine,ParentProcessId | "
    "ConvertTo-Json -Compress -Depth 2"
)

SERVICE_SCRIPT = (
    "Get-CimInstance Win32_Service | Where-Object { $_.ProcessId -ne 0 } | "
    "Select-Object ProcessId,Name,DisplayName,StartName | ConvertTo-Json -Compress"
)

SIGNATURE_SCRIPT = (
    "$PATHS | ForEach-Object { "
    "  $s = Get-AuthenticodeSignature $_ -ErrorAction SilentlyContinue; "
    "  [PSCustomObject]@{ Path=$_; Status=[string]$s.Status; "
    "    Signer=$(if($s.SignerCertificate){$s.SignerCertificate.Subject}else{$null}) } "
    "} | ConvertTo-Json -Compress"
)

# TTL 起始值對應的作業系統族。key 為觀測值不超過的起始值，新增一族多一列。
TTL_ORIGIN = {64: "Linux/Android/macOS/嵌入式", 128: "Windows", 255: "網路設備/IoT"}


def _rdn(name, oid):
    """憑證名稱中的一個欄位。取不到為空序列。"""
    return "".join(a.value for a in name.get_attributes_for_oid(oid)[:1]) or None


def _san(cert):
    """憑證涵蓋的名稱。無此擴充為空清單。"""
    return [name
            for ext in [e for e in cert.extensions
                        if e.oid == ExtensionOID.SUBJECT_ALTERNATIVE_NAME]
            for name in ext.value.get_values_for_type(x509.DNSName)]


def _powershell(script):
    """PowerShell 預設以主控台 ANSI 編碼輸出，含非 ASCII 的命令列會解碼失敗，故明確設為 UTF-8。"""
    prefix = "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; "
    out = subprocess.run(
        ["powershell", "-NoProfile", "-NonInteractive", "-Command", prefix + script],
        capture_output=True, text=True, encoding="utf-8", errors="replace")
    raw = json.loads(out.stdout or "[]")
    return raw if isinstance(raw, list) else [raw]


def _pick(table, key):
    return [value for k, value in table.items() if k == key]


def _certificate(target):
    """取對方出示的憑證。無主機名稱時不送 SNI，多數 CDN 會因此拒絕交握。"""
    address, port, server_name = target
    context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
    context.check_hostname = False
    context.verify_mode = ssl.CERT_NONE

    try:
        with socket.create_connection((address, port), timeout=3) as raw:
            with context.wrap_socket(raw, server_hostname=server_name or None) as tls:
                cert = x509.load_der_x509_certificate(tls.getpeercert(binary_form=True))
                return {
                    "subject": _rdn(cert.subject, NameOID.COMMON_NAME),
                    "organization": _rdn(cert.subject, NameOID.ORGANIZATION_NAME),
                    "country": _rdn(cert.subject, NameOID.COUNTRY_NAME),
                    "issuer": _rdn(cert.issuer, NameOID.ORGANIZATION_NAME),
                    "not_after": cert.not_valid_after_utc.isoformat(),
                    "san": _san(cert)[:4],
                    "protocol": tls.version(),
                    "error": None,
                }
    except Exception as error:
        return {"subject": None, "organization": None, "country": None,
                "issuer": None, "not_after": None, "san": [],
                "protocol": None, "error": type(error).__name__}


def _ttl(address):
    out = subprocess.run(["ping", "-n", "1", "-w", "700", address],
                         capture_output=True, text=True, errors="ignore")
    hits = [line.upper().split("TTL=")[1].split()[0]
            for line in out.stdout.splitlines() if "TTL=" in line.upper()]
    return "".join(hits[:1]) or None


def _os_family(ttl):
    origins = [o for o in sorted(TTL_ORIGIN) if int(ttl) <= o]
    return "".join(TTL_ORIGIN[o] for o in origins[:1]) or None


def _mdns(address):
    out = _powershell(
        "(Resolve-DnsName -Name '%s' -LlmnrNetbiosOnly -ErrorAction SilentlyContinue)"
        ".NameHost | ConvertTo-Json -Compress" % address)
    return "".join(str(x) for x in out if x) or None


def _signatures(paths):
    quoted = ",".join("'%s'" % p.replace("'", "''") for p in paths)
    script = SIGNATURE_SCRIPT.replace("$PATHS", "@(%s)" % quoted)
    return {s["Path"]: s for s in _powershell(script)}


def local_devices():
    """同網段裝置。身分為實體位址，排除本機自己的網卡與群播廣播。"""
    own = {i["link_layer_address"] for i in ip.interfaces() if i["link_layer_address"]}

    candidates = [n for n in ip.neighbors()
                  if n["link_layer_address"]
                  and n["link_layer_address"] != "00:00:00:00:00:00"
                  and n["link_layer_address"] not in own
                  and not n["link_layer_address"].startswith(MULTICAST_PREFIX)]

    grouped = {}
    for n in candidates:
        grouped.setdefault(n["link_layer_address"], []).append(n)

    def describe(mac, entries):
        addresses = sorted({e["address"] for e in entries})
        routable = [a for a in addresses if not a.startswith("fe80")][:1]
        ttl = "".join(_ttl(a) or "" for a in routable)
        return {
            "實體位址": mac,
            "位址": addresses,
            "芳鄰狀態": sorted({e["state"] for e in entries}),
            "介面": sorted({e["interface_index"] for e in entries}),
            "是否為路由器": any(e["is_router"] for e in entries),
            "TTL": ttl or None,
            "作業系統族": "".join(_os_family(t) or "" for t in [ttl] if t) or None,
            "主機名稱": "".join(_mdns(a) or "" for a in routable) or None,
            "製造商": None,
        }

    return [describe(mac, entries) for mac, entries in sorted(grouped.items())]


def connections():
    conns = [c for c in ip.tcp_connections() if c["remote_address"] not in UNSPECIFIED]

    processes = {p["ProcessId"]: p for p in _powershell(PROCESS_SCRIPT)}
    services = {}
    for s in _powershell(SERVICE_SCRIPT):
        services.setdefault(s["ProcessId"], []).append(s)

    used = sorted({processes[c["process_id"]]["ExecutablePath"]
                   for c in conns
                   for _ in _pick(processes, c["process_id"])
                   if processes[c["process_id"]]["ExecutablePath"]})
    signatures = _signatures(used)

    cached = dnscache_wmi.address_to_name()
    interfaces = {addr: i for i in ip.interfaces() for addr in i["ip_addresses"]}

    addresses = sorted({c["remote_address"] for c in conns})
    with ThreadPoolExecutor(max_workers=32) as pool:
        reverse = dict(zip(addresses, pool.map(nr.reverse, addresses)))

    def name_of(address):
        return "".join(_pick(cached, address)) or "".join(reverse[address][:1]) or None

    targets = [(c["remote_address"], c["remote_port"], name_of(c["remote_address"]))
               for c in conns]
    with ThreadPoolExecutor(max_workers=16) as pool:
        certificates = dict(zip([(t[0], t[1]) for t in targets],
                                pool.map(_certificate, targets)))

    def row(c):
        process = _pick(processes, c["process_id"])
        path = "".join(p["ExecutablePath"] or "" for p in process) or None
        signature = _pick(signatures, path)
        cert = certificates[(c["remote_address"], c["remote_port"])]
        return {
            "本機行程": {
                "pid": c["process_id"],
                "名稱": "".join(p["Name"] for p in process) or None,
                "路徑": path,
                "命令列": "".join(p["CommandLine"] or "" for p in process) or None,
                "父行程": "".join(str(p["ParentProcessId"]) for p in process) or None,
                "服務": [s["Name"] for group in _pick(services, c["process_id"]) for s in group],
                "簽章": "".join(s["Status"] for s in signature) or None,
                "簽署者": "".join(s["Signer"] or "" for s in signature) or None,
            },
            "通道": {
                "協定": c["protocol"],
                "狀態": c["state"],
                "本機": "%s:%s" % (c["local_address"], c["local_port"]),
                "介面": "".join(i["name"] for i in _pick(interfaces, c["local_address"])) or None,
            },
            "對方": {
                "位址": c["remote_address"],
                "連接埠": c["remote_port"],
                "網域名稱": name_of(c["remote_address"]),
                "憑證主體": cert["subject"],
                "憑證組織": cert["organization"],
                "憑證國別": cert["country"],
                "憑證簽發者": cert["issuer"],
                "憑證涵蓋": cert["san"],
                "TLS": cert["protocol"],
                "憑證取得失敗": cert["error"],
            },
        }

    return [row(c) for c in conns]


if __name__ == "__main__":
    payload = {"同網段裝置": local_devices(), "對外連線": connections()}
    with open("identity.json", "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=2)
    print("裝置 %d 台、連線 %d 條 -> identity.json"
          % (len(payload["同網段裝置"]), len(payload["對外連線"])))
