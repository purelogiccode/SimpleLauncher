#!/usr/bin/env python3
"""Keep the base systems plus the requested batch from vm-systems2.json (app must be stopped)."""
import json
import sqlite3
import sys

DB = "/home/vm/.local/share/SimpleLauncher/settings.dat"
SRC = "/home/vm/vision/vm-systems2.json"
BASE = {"Test System", "Second System", "Broken System", "Arcade"}

batch = set(sys.argv[1:])
if not batch:
    sys.exit("usage: seed-batch2.py <SystemName> [<SystemName> ...]")

systems = json.load(open(SRC, encoding="utf-8"))
conn = sqlite3.connect(DB)
conn.execute("PRAGMA busy_timeout = 5000")
with conn:
    names = [r[0] for r in conn.execute("SELECT SystemName FROM Systems")]
    for name in names:
        if name not in batch and name not in BASE:
            conn.execute("DELETE FROM Systems WHERE SystemName = ?", (name,))
    seeded = []
    for system in systems:
        if system["SystemName"] in batch:
            conn.execute(
                "INSERT OR REPLACE INTO Systems (SystemName, ConfigJson) VALUES (?, ?)",
                (system["SystemName"], json.dumps(system)),
            )
            seeded.append(system["SystemName"])
conn.close()
print(json.dumps({"seeded": sorted(seeded), "missing": sorted(batch - set(seeded))}))
