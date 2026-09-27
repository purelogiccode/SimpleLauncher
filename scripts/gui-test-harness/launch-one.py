#!/usr/bin/env python3
"""Launch a single system on a freshly started app (caller restarts the app first)."""
import json
import subprocess
import sys
import time

sys.path.insert(0, "/home/vm/vision")
from atspi_nav import Nav  # noqa: E402

ENV = "DISPLAY=:0 XAUTHORITY=/home/vm/.Xauthority"
SYSTEM = sys.argv[1]
GAME = sys.argv[2]
WAIT = int(sys.argv[3]) if len(sys.argv) > 3 else 60


def sh(cmd, t=150):
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True, timeout=t)
    return (r.stdout + r.stderr).strip()


def windows():
    out = sh(f"{ENV} wmctrl -l")
    return [l.split(None, 3)[3] for l in out.splitlines() if len(l.split(None, 3)) == 4]


def letter_bar(n):
    return [e for e in n.find_all(name="All", role="push button") if e.extents]


n = Nav()
print(json.dumps(n.ensure_ready()), flush=True)
n.close_dialogs()
rec = {"system": SYSTEM, "game": GAME}
# Fresh app: click the system card directly and wait for the letter bar.
n.click_card(SYSTEM)
deadline = time.time() + 25
opened = False
while time.time() < deadline:
    if letter_bar(n):
        opened = True
        break
    time.sleep(0.6)
rec["opened"] = opened
print("opened:", opened, "system:", n.current_system(), flush=True)
if not opened:
    print(json.dumps(rec), flush=True)
    sys.exit(1)
hits = letter_bar(n)
n.click(hits[0])
time.sleep(2.5)
rec["pagination"] = n.pagination_count()
menu = []
for attempt in range(6):
    cards = [e for e in n.find_all(role="list item") if e.name == GAME and e.extents]
    if cards:
        n.right_click(cards[0])
    else:
        n.click_at(195, 330, button=3)
    time.sleep(1.6)
    menu = [e.name for e in n.popup_items()]
    if "Launch Game" in menu:
        break
    if any(name in menu for name in ("Select System", "Edit System", "Delete System")):
        n.press("Escape")
        time.sleep(0.8)
        n.click_card(SYSTEM)
        time.sleep(3)
        hits = letter_bar(n)
        if hits:
            n.click(hits[0])
            time.sleep(2.5)
    else:
        time.sleep(1.2)
rec["menu_launch"] = "Launch Game" in menu
if "Launch Game" in menu:
    before = set(windows())
    n.click_context_item("Launch Game")
    if SYSTEM == "Microsoft DOS":
        deadline = time.time() + 45
        while time.time() < deadline:
            items = [e for e in n.find_all(role="list item") if e.extents]
            if items:
                names = [e.name for e in items]
                n.click(items[0])
                time.sleep(0.8)
                for btn in ("Launch", "OK"):
                    hits = [e for e in n.find_all(name=btn, role="push button") if e.extents]
                    if hits:
                        n.click(hits[0])
                        break
                rec["dos_picked"] = names[:6]
                break
            time.sleep(1)
    deadline = time.time() + WAIT
    while time.time() < deadline:
        new = [w for w in windows() if w not in before and "Simple Launcher" not in w]
        if new:
            break
        time.sleep(2)
    time.sleep(4)
    rec["windows_all"] = [w for w in windows() if "Simple Launcher" not in w]
    rec["procs"] = sh("pgrep -af 'RetroArch-Linux-x86_64.AppImage|redream|ymir-sdl3|azahar|Cemu_2.6/Cemu|"
                      "PPSSPP|pcsx2|rpcs3|DuckStation|xemu-0.8.136|supermodel|dosbox|openmsx' | grep -v pgrep | head -3")
shot = f"/home/vm/vision/shots/probe-j-{SYSTEM.replace(' ', '_')}.png"
sh(f"{ENV} gnome-screenshot -f {shot}")
rec["shot"] = shot
print(json.dumps(rec), flush=True)
sh("pkill -f '[r]edream'; pkill -f '[y]mir-sdl3'; pkill -f '[a]zahar'; pkill -f '[C]emu_2.6/Cemu'; "
   "pkill -f '[P]PSSPP'; pkill -f '[p]csx2'; pkill -f '[r]pcs3'; pkill -f '[D]uckStation'; "
   "pkill -f '[x]emu-0.8.136'; pkill -f '[s]upermodel'; pkill -f '[d]osbox'; "
   "pkill -f '[R]etroArch-Linux-x86_64.AppImage'; pkill -f '[o]penmsx'; true")
