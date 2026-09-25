#!/usr/bin/env python3
"""Fixture manager for the SimpleLauncher GUI harness (runs on the Linux VM).

Usage: fixture.py <command>
  seed         Create systems/ROMs/images/dummy emulator in the unified DB
  empty        Remove all systems (first-run state)
  clean-state  Clear favorites/play history/temp artifacts (keep systems)
  dump         Print Systems/Favorites/PlayHistory/AppSettings rows as JSON
"""
import json
import os
import shutil
import sqlite3
import struct
import subprocess
import sys
import zipfile
import zlib

APP_DIR = "/home/vm/SimpleLauncher"
APPDATA = "/home/vm/.local/share/SimpleLauncher"
DB = os.path.join(APPDATA, "settings.dat")
ROMS = "/home/vm/roms"
IMAGES = "/home/vm/images"
EMULATOR = "/home/vm/dummy-emulator.sh"
EMULATOR_LOG = "/tmp/dummy-emulator.log"

SYSTEMS = [
    {
        "SystemName": "Test System",
        "SystemFolders": [f"{ROMS}/Test System"],
        "SystemImageFolder": f"{IMAGES}/Test System",
        "FileFormatsToSearch": [".nes", ".zip"],
        "FileFormatsToLaunch": [".nes", ".zip"],
        "ExtractFileBeforeLaunch": True,
        "GroupByFolder": False,
        "DisableRecursiveSearch": False,
        "Emulators": [
            {
                "EmulatorName": "Dummy Emulator",
                "EmulatorLocation": EMULATOR,
                "EmulatorParameters": '"%ROM%"',
                "ReceiveANotificationOnEmulatorError": True,
            }
        ],
    },
    {
        "SystemName": "Second System",
        "SystemFolders": [f"{ROMS}/Second System"],
        "SystemImageFolder": f"{IMAGES}/Second System",
        "FileFormatsToSearch": [".nes"],
        "FileFormatsToLaunch": [".nes"],
        "ExtractFileBeforeLaunch": False,
        "GroupByFolder": False,
        "DisableRecursiveSearch": False,
        "Emulators": [
            {
                "EmulatorName": "Dummy Emulator",
                "EmulatorLocation": EMULATOR,
                "EmulatorParameters": '"%ROM%"',
                "ReceiveANotificationOnEmulatorError": True,
            }
        ],
    },
    {
        "SystemName": "Broken System",
        "SystemFolders": [f"{ROMS}/Does Not Exist"],
        "SystemImageFolder": f"{IMAGES}/Broken System",
        "FileFormatsToSearch": [".nes"],
        "FileFormatsToLaunch": [".nes"],
        "ExtractFileBeforeLaunch": False,
        "GroupByFolder": False,
        "DisableRecursiveSearch": False,
        "Emulators": [
            {
                "EmulatorName": "Dummy Emulator",
                "EmulatorLocation": EMULATOR,
                "EmulatorParameters": '"%ROM%"',
                "ReceiveANotificationOnEmulatorError": True,
            }
        ],
    },
]


def make_png(path, rgb=(200, 60, 60), size=64):
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(
            ">I", zlib.crc32(tag + data) & 0xFFFFFFFF
        )

    raw = b"".join(b"\x00" + bytes(rgb) * size for _ in range(size))
    png = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(raw))
        + chunk(b"IEND", b"")
    )
    with open(path, "wb") as handle:
        handle.write(png)


def write_roms():
    test_roms = f"{ROMS}/Test System"
    second_roms = f"{ROMS}/Second System"
    os.makedirs(test_roms, exist_ok=True)
    os.makedirs(second_roms, exist_ok=True)
    for name in ("Alpha Quest.nes", "Beta Blaster.nes", "Captain Nemo (USA).nes"):
        with open(os.path.join(test_roms, name), "wb") as handle:
            handle.write(b"NES\x1a" + name.encode() + b"\x00" * 64)
    with open(os.path.join(second_roms, "Sonic.nes"), "wb") as handle:
        handle.write(b"NES\x1a" + b"Sonic" + b"\x00" * 64)
    zip_path = os.path.join(test_roms, "Zipped Quest.zip")
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_STORED) as archive:
        archive.writestr("Zipped Quest.nes", b"NES\x1aZippedQuest" + b"\x00" * 64)

    test_images = f"{IMAGES}/Test System"
    os.makedirs(test_images, exist_ok=True)
    make_png(os.path.join(test_images, "Alpha Quest.png"), (30, 120, 200))
    make_png(os.path.join(test_images, "Beta Blaster.png"), (200, 140, 30))
    os.makedirs(f"{IMAGES}/Second System", exist_ok=True)
    make_png(f"{IMAGES}/Second System/Sonic.png", (30, 180, 90))
    with open(os.path.join(test_images, "corrupt.png"), "wb") as handle:
        handle.write(b"not a real png")


def write_emulator():
    script = (
        "#!/bin/bash\n"
        f'echo "$(date +%s) $*" >> {EMULATOR_LOG}\n'
        "sleep 7\n"
    )
    with open(EMULATOR, "w") as handle:
        handle.write(script)
    os.chmod(EMULATOR, 0o755)


def connect():
    connection = sqlite3.connect(DB)
    connection.execute("PRAGMA busy_timeout = 5000")
    return connection


def seed():
    write_roms()
    write_emulator()
    connection = connect()
    with connection:
        connection.execute("DELETE FROM Favorites")
        connection.execute("DELETE FROM PlayHistory")
        connection.execute("DELETE FROM Systems")
        for system in SYSTEMS:
            connection.execute(
                "INSERT OR REPLACE INTO Systems (SystemName, ConfigJson) VALUES (?, ?)",
                (system["SystemName"], json.dumps(system)),
            )
    connection.close()
    for path in (EMULATOR_LOG,):
        if os.path.exists(path):
            os.remove(path)
    shutil.rmtree("/tmp/SimpleLauncher", ignore_errors=True)
    print(json.dumps({"seeded": [s["SystemName"] for s in SYSTEMS]}))


def empty():
    connection = connect()
    with connection:
        connection.execute("DELETE FROM Systems")
        connection.execute("DELETE FROM Favorites")
        connection.execute("DELETE FROM PlayHistory")
    connection.close()
    print(json.dumps({"systems": []}))


def clean_state():
    connection = connect()
    with connection:
        connection.execute("DELETE FROM Favorites")
        connection.execute("DELETE FROM PlayHistory")
    connection.close()
    for path in (EMULATOR_LOG,):
        if os.path.exists(path):
            os.remove(path)
    shutil.rmtree("/tmp/SimpleLauncher", ignore_errors=True)
    print(json.dumps({"cleaned": True}))


def dump():
    connection = connect()
    result = {}
    for table in ("Systems", "Favorites", "PlayHistory", "AppSettings", "EmulatorSettings"):
        rows = connection.execute(f"SELECT * FROM {table}").fetchall()
        columns = [d[0] for d in connection.execute(f"SELECT * FROM {table} LIMIT 0").description]
        result[table] = [dict(zip(columns, row)) for row in rows]
    connection.close()
    print(json.dumps(result, default=str))


if __name__ == "__main__":
    command = sys.argv[1] if len(sys.argv) > 1 else "dump"
    {"seed": seed, "empty": empty, "clean-state": clean_state, "dump": dump}[command]()
