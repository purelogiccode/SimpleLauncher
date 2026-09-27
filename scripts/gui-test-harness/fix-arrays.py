#!/usr/bin/env python3
"""Normalize scalar FileFormatsToSearch/FileFormatsToLaunch into arrays for the seeded systems."""
import json
import sqlite3

DB = "/home/vm/.local/share/SimpleLauncher/settings.dat"


def as_list(value):
    if value is None:
        return None
    if isinstance(value, list):
        return value
    return [value]


conn = sqlite3.connect(DB)
conn.execute("PRAGMA busy_timeout = 5000")
fixed = []
with conn:
    for name, config_json in conn.execute("SELECT SystemName, ConfigJson FROM Systems"):
        config = json.loads(config_json)
        changed = False
        for field in ("FileFormatsToSearch", "FileFormatsToLaunch", "SystemFolders"):
            value = config.get(field)
            if value is not None and not isinstance(value, list):
                config[field] = as_list(value)
                changed = True
        if changed:
            conn.execute("UPDATE Systems SET ConfigJson = ? WHERE SystemName = ?",
                         (json.dumps(config), name))
            fixed.append(name)
conn.close()
print(json.dumps({"fixed": fixed}))
