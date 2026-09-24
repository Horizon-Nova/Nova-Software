"""CIM／WMI 橋接。以 PowerShell 取得，回 Python 物件。

主控台預設以 ANSI 輸出，含非 ASCII 欄位時解碼失敗會使整批為空，
故每次都明設 UTF-8 並以 replace 收尾。
"""

import json
import subprocess

PREFIX = "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; $ErrorActionPreference='Stop'; "


def run(script):
    """執行一段 PowerShell，回 (成功, 結果或錯誤字串)。"""
    completed = subprocess.run(
        ["powershell", "-NoProfile", "-NonInteractive", "-Command", PREFIX + script],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    ok = completed.returncode == 0
    return (ok, completed.stdout if ok else completed.stderr.strip())


def query(class_name, namespace="root/cimv2", properties=None, where=None):
    """查一個 CIM 類別，回 (成功, 列清單或錯誤字串)。"""
    select = "*" if properties is None else ",".join(properties)
    filter_part = "" if where is None else f" -Filter \"{where}\""
    script = (
        f"Get-CimInstance -Namespace {namespace} -ClassName {class_name}{filter_part}"
        f" | Select-Object {select}"
        f" | ConvertTo-Json -Depth 3 -Compress"
    )
    ok, out = run(script)
    if not ok:
        return (False, out.splitlines()[0] if out else "unknown error")
    out = out.strip()
    if not out:
        return (True, [])
    data = json.loads(out)
    return (True, data if isinstance(data, list) else [data])


def clean(row):
    """去掉 CIM 附帶的中繼欄位與空值。"""
    drop = ("PSComputerName", "CimClass", "CimInstanceProperties", "CimSystemProperties")
    return {k: v for k, v in row.items() if k not in drop and v not in (None, "", [])}
