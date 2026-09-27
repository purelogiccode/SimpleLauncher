#!/usr/bin/env python3
"""Keep the base systems + only the requested extra systems (app must be stopped).

Normalizes single-string format fields to lists: the app's SystemConfigStore uses a strict
System.Text.Json List<string> deserializer and silently skips entries whose blob has a scalar.
"""
import json
import sqlite3
import sys

DB = "/home/vm/.local/share/SimpleLauncher/settings.dat"
SRC = "/home/vm/vision/vm-systems.json"
BASE = ("Test System", "Second System", "Broken System", "Arcade")


def as_list(value):
    if value is None:
        return []
    if isinstance(value, str):
        return [value]
    return list(value)


all_systems = json.load(open(SRC, encoding="utf-8"))
wanted = set(sys.argv[1:])
if not wanted:
    print(json.dumps({"error": "no systems requested"}))
    sys.exit(1)

conn = sqlite3.connect(DB)
conn.execute("PRAGMA busy_timeout = 5000")
with conn:
    placeholders = ",".join("?" for _ in BASE)
    conn.execute(f"DELETE FROM Systems WHERE SystemName NOT IN ({placeholders})", BASE)
    for system in all_systems:
        if system["SystemName"] not in wanted:
            continue
        system["SystemFolders"] = as_list(system.get("SystemFolders"))
        system["FileFormatsToSearch"] = as_list(system.get("FileFormatsToSearch"))
        system["FileFormatsToLaunch"] = as_list(system.get("FileFormatsToLaunch"))
        conn.execute(
            "INSERT OR REPLACE INTO Systems (SystemName, ConfigJson) VALUES (?, ?)",
            (system["SystemName"], json.dumps(system)),
        )
conn.close()
print(json.dumps({"active": list(BASE) + sorted(wanted)}))
