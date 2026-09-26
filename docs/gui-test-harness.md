# Simple Launcher — Linux GUI test harness (AT-SPI + vision)

Portable manual for the automated GUI-test harness that drives the **Avalonia/Linux** build of
Simple Launcher on a Linux VM and checks it against `docs/manual-tests.md` (the Linux checklist).
It is written for a human or an LLM resuming the work on another machine.

The harness is **tracked in this repository** at `scripts\gui-test-harness\`. Clone the repo on the
new machine, adapt the configuration values in §4 (VM IP/credentials; the script paths derive from
`$PSScriptRoot`), and follow §5. If the scripts are ever lost, §12 and the appendices contain enough
contracts to rebuild them.

---

## 1. Architecture

```
host (Windows or any OS with PowerShell 7 + SSH)
  run-suite.ps1                scenarios (one PowerShell hashtable each)
    -> lib.ps1                 SSH session, app lifecycle, fixture, screenshots, vision
         -> SCP  atspi_nav.py + fixture.py   to the guest
         -> SSH  python3 <scenario>          (heredoc) on the guest
              -> pyatspi / AT-SPI           drives the running app
              -> prints "SL_RESULT: {json}" one line
    -> gnome-screenshot (guest) -> SCP PNG to shots\
    -> ask_vision.py (host)     -> OpenRouter vision model -> "VERDICT: PASS|FAIL"
    -> reports\run-<timestamp>.md  (AT-SPI JSON + vision answer per scenario)

guest (Linux Mint 22.3 Cinnamon, X11; user `vm`)
  ~/SimpleLauncher/SimpleLauncher.Avalonia   self-contained linux-x64 publish
  ~/.local/share/SimpleLauncher/settings.dat unified SQLite (systems, favorites, history, settings)
  ~/roms/<System>, ~/images/<System>         fixture ROMs/covers
  /home/vm/dummy-emulator.sh                 script emulator (sleeps 7 s)
  /home/vm/vision/atspi_nav.py, fixture.py   deployed by Deploy-VmNav
```

Verdict per scenario = **deterministic AND vision** (vision only when the scenario defines a
`Prompt`). Deterministic = `ok == true`, at least one check, no `exception` in `extra`.

### Quick start for an LLM agent (operational loop)

1. **Locate the harness**: `scripts\gui-test-harness\` in this repository (clone it on the new
   machine). Run all commands from the repository root, or use the absolute path of the harness.
2. **Check the environment**:
   ```powershell
   . scripts\gui-test-harness\lib.ps1
   Deploy-VmNav
   Test-VmNavHealth          # must return True (app running, AT-SPI reachable)
   ```
   If false: start the VM / re-check the IP / `Restart-VmApp -Fixture seeded` / re-apply 1080p
   (§12). Never trust a red run while `apt`/`dpkg` is active on the guest.
3. **Run the target scenarios** (subset while iterating, full pass at the end):
   ```powershell
   pwsh -NoProfile -File scripts\gui-test-harness\run-suite.ps1 -Only EDIT-01,HELP-01
   pwsh -NoProfile -File scripts\gui-test-harness\run-suite.ps1
   ```
4. **On a failure**: open the newest `reports\run-*.md`, read the AT-SPI JSON and vision answer,
   then reproduce the scenario body ad-hoc with `Invoke-VmPython` (§6). Fix the scenario (not the
   app) unless the failure is a real product bug — in that case document it as a finding.
5. **Author new scenarios** from `docs/manual-tests.md` using §7-§9. Keep one concern per
   scenario, deterministic assertions first, vision only for visual claims.
6. **When green**: update `ManualTests.md` §6 (statuses, findings, open items) and tick the
   `docs/manual-tests.md` checkboxes only for items the suite actually verified. Never weaken,
   filter or skip a scenario to make it pass.

---

## 2. Prerequisites

### Host

- PowerShell 7+ (`pwsh`) with the **Posh-SSH** module:
  ```powershell
  Install-Module Posh-SSH -Scope CurrentUser
  ```
- Python 3 (standard library only; `ask_vision.py` uses `urllib`) on `PATH`.
- User environment variable `OPENROUTER_API_KEY` (`sk-or-...`):
  ```powershell
  [Environment]::SetEnvironmentVariable('OPENROUTER_API_KEY', 'sk-or-...', 'User')
  ```
  The harness never writes the key to disk or logs.
- Network access to `https://openrouter.ai` and to the guest's SSH port.

### Guest (Linux VM or bare machine)

- X11 session (Wayland works through Xwayland) with a user logged in on `DISPLAY=:0`
  (autologin recommended so the app can run headless-of-user).
- Packages: `at-spi2-core python3-pyatspi xdotool wmctrl gnome-screenshot openssh-server`.
  On Mint 22.3 most are present; `at-spi-bus-launcher` starts with the session.
- An accessibility bus available to the SSH session; the harness exports
  `DISPLAY=:0 XAUTHORITY=/home/vm/.Xauthority XDG_RUNTIME_DIR=/run/user/1000
  DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/1000/bus` for every guest command.
- The app deployed at `~/SimpleLauncher/SimpleLauncher.Avalonia` (self-contained publish, see §4).

### App payload (built on the host)

```powershell
dotnet publish SimpleLauncher.Avalonia\SimpleLauncher.Avalonia.csproj -c Release `
  -f net10.0 -r linux-x64 --self-contained true -o D:\payload\linux-x64-annot
tar -czf D:\payload\payload-annot.tar.gz -C D:\payload\linux-x64-annot .
# copy to the guest, extract over the existing app folder, chmod +x
```
`D:\payload` is any host folder outside the repo (the publish output is not committed).
`-f net10.0` is required (the project is multi-TFM; otherwise NETSDK1129).

---

## 3. Harness files

| File | Purpose | Size |
|---|---|---|
| `run-suite.ps1` | Scenario suite + report writer. Scenarios are PowerShell hashtables (`Id`, `Name`, `Fixture`, `Restart`, `LaunchArgs`, `Py`, `Shot`, `Prompt`) | ~50 KB |
| `lib.ps1` | Dot-sourced by the suite: SSH session, app lifecycle, fixture application, screenshots, vision call, result parsing. Also usable interactively (`. scripts\gui-test-harness\lib.ps1`) | ~7 KB |
| `atspi_nav.py` | AT-SPI navigation library (deployed to `/home/vm/vision/`); `Nav` class + `Entry`; also a CLI (`tree`, `ready`, `dump`, `selftest`) | ~23 KB |
| `fixture.py` | Seeds/clears the guest unified DB and files (deployed to `/home/vm/vision/`); commands `seed`, `empty`, `clean-state`, `dump` | ~7 KB |
| `ask_vision.py` | One-shot OpenRouter vision call: `python ask_vision.py <png> "<prompt>" [--model M] [--max-tokens N] [--temperature T]`; reads `OPENROUTER_API_KEY`; prints the model text plus a `--- usage ---` JSON line | ~3 KB |

The scripts resolve their own locations from `$PSScriptRoot`, so the checkout can live anywhere.
Only the guest-side paths (§4) and the VM IP/credentials need adapting. `shots\` and `reports\` are
created on demand and are **gitignored** (evidence stays local).

---

## 4. Configuration to adapt on a new machine

Edit `lib.ps1`:

| Setting | Meaning |
|---|---|
| `$script:VmHostAddress` | Guest IP (DHCP; re-check after each VM boot) |
| `$script:VmUserName` / `$script:VmPassword` | SSH credentials (`vm`/`vm` on the reference VM) |
| `$script:DefaultModel` | Vision model (`xiaomi/mimo-v2.6-flash`) |
| `$script:VmEnv` | Guest session exports (display, X authority, DBus) |

Derived from `$PSScriptRoot` (no edit needed when cloning elsewhere): `$script:HarnessRoot`,
`$script:ShotRoot` (`shots\`), `$script:ReportRoot` (`reports\`), `$script:AskScript`,
`$script:NavScript`, `$script:FixtureScript`.

Guest paths hardcoded in `lib.ps1` / `fixture.py` / scenarios (replace `vm` if the user differs):

| Path | Meaning |
|---|---|
| `/home/vm/SimpleLauncher/SimpleLauncher.Avalonia` | app binary (start/stop identity) |
| `/home/vm/SimpleLauncher` | app folder (`parameters.md`, `WhatsNew.md`, `samples\`, `emulators\`) |
| `/home/vm/.local/share/SimpleLauncher/settings.dat` | unified SQLite (systems/favorites/history/settings) |
| `/home/vm/roms/<System>`, `/home/vm/images/<System>` | fixture ROMs and covers |
| `/home/vm/dummy-emulator.sh`, `/tmp/dummy-emulator.log` | fixture emulator and its launch log |
| `/home/vm/vision` | deployed `atspi_nav.py` + `fixture.py` |

App identity for kill/start is the exact exe path
(`pgrep -f "/home/vm/SimpleLauncher/SimpleLauncher.Avalonia$"` + `/proc/<pid>/exe` check), so the
SSH shell never matches itself.

---

## 5. First-time guest setup

1. SSH works with password auth (`New-SSHSession`).
2. Session environment: the guest must have an active X session on `:0` (autologin) and
   `at-spi2-core` running. Verify:
   ```bash
   DISPLAY=:0 XAUTHORITY=/home/vm/.Xauthority python3 -c "import pyatspi; print(pyatspi.Registry.getDesktop(0).childCount)"
   ```
3. App deployed (see §2) and executable.
4. Console resolution: card coordinates assume 1080p. The reference VM re-applies it with an
   `xrandr-1080p` autostart; after a manual change use:
   ```bash
   DISPLAY=:0 XAUTHORITY=/home/vm/.Xauthority xrandr --output Virtual-1 --mode 1920x1080
   ```
   On Hyper-V this can also be made persistent with
   `Set-VMVideo -VMName LinuxMint -HorizontalResolution 1920 -VerticalResolution 1080`.
5. Deploy the harness helpers (also done automatically at every suite start):
   ```powershell
   . scripts\gui-test-harness\lib.ps1
   Deploy-VmNav             # creates /home/vm/vision and SCPs the helper scripts
   Test-VmNavHealth         # -> True when the app frame is in the AT-SPI tree
   ```
6. Run the app once before the first fixture seed: `fixture.py` writes `settings.dat`, which the app
   creates on its first run. `Set-VmFixture` warns when the DB is missing; run `Restart-VmApp` once.

---

## 6. Running tests

```powershell
# all scenarios (restarts the app per fixture; ~40-60 min with vision)
pwsh -NoProfile -File scripts\gui-test-harness\run-suite.ps1

# subsets while iterating (comma-separated, also -Only A,B from -File)
pwsh -NoProfile -File scripts\gui-test-harness\run-suite.ps1 -Only EDIT-01,HELP-01

# different vision model, or skip the restart (assume the app is already in the right state)
pwsh -NoProfile -File scripts\gui-test-harness\run-suite.ps1 -Model 'google/gemini-3.8-flash'
pwsh -NoProfile -File scripts\gui-test-harness\run-suite.ps1 -SkipRestart
```

Run these from the repository root (or pass the harness's absolute path). The suite prints
`=== <Id> - <Name> [fixture=...] ===` and `-> PASS (det=PASS vision=PASS, Ns)` per scenario, then
the report path. Reports contain the AT-SPI JSON and the raw vision answer.

### Ad-hoc checks (no suite run)

```powershell
. scripts\gui-test-harness\lib.ps1
Deploy-VmNav
Test-VmNavHealth
Restart-VmApp -Fixture seeded              # seed DB + start app (10 s settle)
$shot = Save-VmShot -Name 'adhoc'
Invoke-VisionCheck -Image $shot -Prompt 'Expected ... First line: VERDICT: PASS or VERDICT: FAIL'

# one-off AT-SPI snippet on the guest (heredoc, python3, pyatspi available)
$py = @'
import sys, json
sys.path.insert(0, "/home/vm/vision")
from atspi_nav import Nav
n = Nav()
print(json.dumps([e.to_dict() for e in n.snapshot()][:20], indent=1))
'@
Invoke-VmPython -Script $py
```

`atspi_nav.py` also runs standalone on the guest:
`python3 /home/vm/vision/atspi_nav.py dump` (full tree JSON), `tree` (indented text),
`ready` (activate/maximize/dismiss Welcome), `selftest` (API smoke test).

---

## 7. Fixture contract (`fixture.py`)

Runs **on the guest**, only while the app is stopped (SQLite WAL). Seeded data:

| System | ROM folder | Games | Emulator |
|---|---|---|---|
| Test System | `/home/vm/roms/Test System` | Alpha Quest, Beta Blaster, Captain Nemo (USA), Zipped Quest (.zip with a .nes inside) | Dummy Emulator (`/home/vm/dummy-emulator.sh`, args `"%ROM%"`) |
| Second System | `/home/vm/roms/Second System` | Sonic | Dummy Emulator |
| Broken System | `/home/vm/roms/Does Not Exist` (missing) | - | Dummy Emulator |

Covers: `Alpha Quest.png` (blue), `Beta Blaster.png` (orange), `Sonic.png` (green) under
`/home/vm/images/<System>/`; Captain Nemo and Zipped Quest intentionally have no cover (dark
placeholder). `corrupt.png` is an invalid image, not referenced.

- `fixture.py seed` — writes files + dummy emulator, upserts the three systems into
  `Systems(SystemName, ConfigJson)`, clears Favorites/PlayHistory, removes `/tmp/SimpleLauncher`.
  `ConfigJson` is the `SystemConfigData` shape (PascalCase, case-insensitive on read):
  `SystemName`, `SystemFolders[]`, `SystemImageFolder`, `FileFormatsToSearch[]`,
  `FileFormatsToLaunch[]`, `ExtractFileBeforeLaunch`, `GroupByFolder`, `DisableRecursiveSearch`,
  `Emulators[]` (`EmulatorName`, `EmulatorLocation`, `EmulatorParameters`, ...).
- `fixture.py empty` — deletes all systems (first-run/Welcome state).
- `fixture.py clean-state` — clears favorites/history/temp, keeps systems.
- `fixture.py dump` — prints Systems/Favorites/PlayHistory/AppSettings/EmulatorSettings as JSON.

The dummy emulator logs `epoch <args>` to `/tmp/dummy-emulator.log` and **sleeps 7 s** — play
history is recorded only when play time is > 5 s
(`SimpleLauncher.Avalonia/Services/GameLauncher/LauncherService.cs:675`).

`lib.ps1` maps the scenario value `seeded` to the `seed` command; `Restart-VmApp -Fixture X`
stops the app, applies the fixture, starts the app and waits for the window.

---

## 8. Writing a scenario (the core skill for an LLM)

A scenario is a PowerShell hashtable in `run-suite.ps1`:

| Field | Meaning |
|---|---|
| `Id` / `Name` | Report identity (`AREA-NN`; keep ids stable) |
| `Fixture` | `empty` or `seeded` (default). A change restarts the app with that fixture |
| `Restart` | `$true` forces an app restart first (used for persistence checks) |
| `LaunchArgs` | Extra app args (e.g. `-debug`); a change restarts the app |
| `Py` | Guest Python script; must print exactly one `SL_RESULT: {...}` line |
| `Shot` | Optional PNG base name (taken after the script finishes, copied to `shots\`) |
| `Prompt` | Optional vision question; omit for purely deterministic scenarios |

`Py` is built as `$pyCommon + @'...'@ + $pyTail`: `$pyCommon` imports and defines `n`, `checks`,
`extra`, `db()`, `enabled(name)`, `finish()` and opens `try:`; the body is indented 4 spaces;
`$pyTail` closes with `except` + `finish()`.

Result contract (printed by `finish()`):

```json
SL_RESULT: {"ok": true, "checks": {"grid_present": true}, "extra": {"count": 4}}
```

Rules of thumb:

- One concern per scenario; assert with `checks`, put diagnostics in `extra`.
- Prefer **deterministic** tree assertions (`STATE_ENABLED`, item sets, labels, DB, files) over
  vision; use vision only for visual claims (rendering, palettes, legibility).
- Start every scenario with `n.activate_window(); n.maximize_window(); n.close_dialogs()`.
- Use `n.open_system("Test System")` — it returns to the card screen (Escape) and clicks the card,
  so it works from the browser, Favorites and Play History alike.
- Grid scenarios must call `n.ensure_grid_view()` (view mode persists in the DB).
- Search triggers on **Return**, not the Search button.
- Leave the UI in the state the screenshot should show; close dialogs in the *next* scenario.
- Prompt contract (parsed by `Get-SuiteVerdict`): the answer's first line must be exactly
  `VERDICT: PASS` or `VERDICT: FAIL`, then a brief report. State expected values explicitly, and
  mention unusual styling (e.g. disabled buttons are pale/faded, not grey).

Example scenario:

```powershell
@{
    Id      = 'FILTER-01'
    Name    = 'Filter bar: letter filter narrows the list, All resets it'
    Fixture = 'seeded'
    Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.ensure_grid_view()
    n.filter_letter("All", wait=3.0)
    checks["all_shows_everything"] = (n.pagination_count() or 0) >= 4
    n.filter_letter("B")
    checks["b_filters_to_one"] = n.pagination_count() == 1
    checks["b_sets_status"] = "B" in (n.status_left() or "")
'@ + $pyTail
    Shot    = 'FILTER-01-letter-b'
    Prompt  = @'
The game grid should be filtered by the letter "B": exactly one card "Beta Blaster", the "B"
button highlighted, status "Displaying files 1 to 1 out of 1 total".
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report what is visible and any mismatch. Be brief.
'@
}
```

---

## 9. `atspi_nav.py` API (deployed on the guest)

All handles are re-resolved on every call (cached AT-SPI peers go stale after layout changes).
`Entry` fields: `.acc` (pyatspi Accessible), `.role`, `.name`, `.id`, `.extents` `[x,y,w,h]`,
`.depth`, `.to_dict()`.

| Method | Purpose |
|---|---|
| `snapshot(max_nodes=3000)` | Walk the app tree, deduped `Entry` list |
| `find_all(id=,name=,role=,contains=,entries=)` / `find(...)` / `wait_for(timeout=,**kw)` / `wait_gone(...)` | Lookup; `id` falls back to `x:Name`; `contains=True` = substring match |
| `click(entry)` | Native action (`click`/`select`) else xdotool click at live extents |
| `right_click(entry)` / `double_click(entry)` / `click_at(x,y,button=,repeat=)` | Mouse-only interactions (context menus, cards) |
| `click_card(label)` | System-selection card: finds the label, clicks the button containing it |
| `open_system(label)` | Escape back to the card screen, click the card; works from any main page; waits for grid/list + scan settle |
| `ensure_grid_view()` / `browser_open()` | Toggle back to grid when the persisted mode is list |
| `filter_letter(letter)` | Click a letter button in the filter bar (`All`, `#`, `A`..`Z`) |
| `pagination_count()` / `status_left()` | Parse `PaginationLabel` total / read `StatusLeft` |
| `toggle` / `expand` / `set_text` / `read_text` / `read_value` / `set_value` | Control interfaces (EditableText/Value), xdotool fallback |
| `press(keys)` | xdotool key (activates the window first) |
| `open_menu(name)` / `popup_items()` / `click_menu_item(name)` | Menu bar + popup items (menu items have no AT-SPI action → extents clicks) |
| `click_context_item(name)` | Click an item of an open context-menu `PopupRoot` |
| `select_combo(combo, name)` | Expand a ComboBox and click the popup entry |
| `context_menu_items()` / `dismiss_popup()` | Inspect/close popups |
| `state_names(entry)` / `is_enabled` / `is_checked` / `checked_menu_item()` | AT-SPI states |
| `close_dialogs()` / `close_welcome()` / `ensure_ready()` | Deterministic app state |
| `activate_window()` / `maximize_window()` / `window_id()` / `frame_present(title)` / `frame_extents()` | Window management |
| `wait_extra_frames_gone(timeout)` | Wait for toasts/dialogs to disappear |
| `tree_text()` | Indented tree dump (debugging) |

---

## 10. Vision integration

`ask_vision.py` posts one base64 PNG + prompt to OpenRouter
(`https://openrouter.ai/api/v1/chat/completions`) and prints the text answer plus a usage JSON.
Defaults: model `xiaomi/mimo-v2.6-flash`, `temperature 0`, `--max-tokens` raised by `lib.ps1`
(`Invoke-VisionCheck`, default 8000) because reasoning models spend tokens on hidden reasoning.

Cost observed: **USD 0.0004-0.0007 per scenario** (~2.2k prompt tokens image + up to 4k completion).
A 30-scenario pass is a few cents. If `content` comes back empty, raise `--max-tokens`.

---

## 11. Guest quirks and lessons (baked into the harness)

- **Avalonia AT-SPI server** starts unconditionally on X11 (Avalonia 12.1); `at-spi2-core` is
  enough. `AutomationId` falls back to `x:Name`.
- **BUG-02 (upstream)**: dismissing the first modal (`Welcome`) collapses `MainContentGrid`'s
  children from the tree while menus/dialogs stay exposed. Restarting the app restores the tree;
  the seeded fixture avoids the Welcome dialog entirely.
- **Grid peers go stale**: after the first render / a letter click, game list items lose their
  names in the tree. Use `PaginationLabel`, `StatusLeft`, the DB and the emulator log for
  assertions; interact with cards via fixed coordinates (1920x1080 maximized: first card after a
  letter filter ≈ `(195, 330)`).
- **Context menus** open as a `PopupRoot` frame whose menu items are exposed; right-click must be
  xdotool at live extents.
- **Two-click submenus**: `Edit Links` and `Sound Configuration` are flyout parents whose child
  repeats the name — click the parent, then the rightmost match.
- **System switching**: the status bar (`System:` label) is the authoritative loaded system; the
  `SystemComboBox` can lag behind it. `open_system()` escapes back to the card screen and clicks the
  card. The `All` filter means "All Games" across systems, and a system's games appear only after
  that system was opened once in the session.
- **Persistence**: theme and view mode persist in `settings.dat`; scenarios must not assume the
  default. `ensure_grid_view()` fixes the view mode.
- **Play history** needs >5 s of play time; the dummy emulator sleeps 7 s.
- **Mint Update / `apt` running** starves the app and degrades AT-SPI: wait for `apt`/`dpkg` to
  finish and restart the app before trusting a red run.
- **Control naming**: system-selection cards are code-created buttons (AT-SPI name
  `Avalonia.Controls.StackPanel`), so the harness clicks the card's name label; the Edit System Help
  button is named "Open the parameters wiki" with a `? Help` content label (find it via the label).
- **One assertion per scenario**, tree assertions first, vision for visuals only.
- Keep every screenshot; the report references it.

---

## 12. Troubleshooting

| Symptom | Fix |
|---|---|
| `Test-VmNavHealth` false | App not running (`Restart-VmApp`), wrong IP (`$script:VmHostAddress`), or no X session on `:0` |
| All checks `null` / 0.1 s runs | Python syntax error or SSH failure; run the scenario body via `Invoke-VmPython` and read the raw output |
| Screenshots at 1024x768 | Guest rebooted; re-apply `xrandr ... 1920x1080` (card coordinates assume 1080p) |
| Menus/dialogs ignore clicks | A modal is open; start with `n.close_dialogs()`, or restart the app |
| Vision says "empty content" / provider 5xx | `Invoke-VisionCheck` retries 3× automatically; raise `--max-tokens` if the model returns nothing |
| AT-SPI tree empty for new app starts | Known registration flake; `Start-VmApp` waits up to 60 s and retries (3 starts). Never kill `at-spi-bus-launcher`/`at-spi2-registryd` (leaves a stale `AT_SPI_BUS` guid); reboot the VM if it persists |
| Fixture changes not visible | The app must be **stopped** while `fixture.py` writes the DB; use `Restart-VmApp -Fixture` |
| `fixture.py` warning about the DB | Brand-new VM: run the app once so it creates `settings.dat` |
| Report says `exception` | Read `extra.exception` in the report; usually a stale selector or a modal |
| Posh-SSH session drops | `Close-VmSession`; `Invoke-VmShell` retries once automatically |

---

## 13. Coverage map (what is automated vs manual)

Automated by the 31 scenarios (**31/31 PASS, 2026-09-26**): startup/load, system selection,
grid/list rendering, covers, filter bar, search + empty state, view-mode menu checkmarks, theme
menu + persistence, Edit System window/help pane, Easy Mode selection state, fuzzy threshold,
dead-zone, Edit Links, Sound Configuration, About/Update History, Support validation, Favorites
add/remove, Play History, ZIP extraction + temp cleanup, file watcher, Debug window + live log
lines, ROM History message, Broken System error dialog, menu structure (Windows-only items hidden).

Manual/integration by design: emulator downloads and `Stop` mid-download, store scanners, config
injection (Windows-only), RetroAchievements login/hashing, gamepad hardware, CHD/ISO/XISO mounting,
external tools, updater, Commander Genius, and anything needing real network/emulators. Update
`docs/manual-tests.md` checkboxes only for items the suite actually verified.

---

## 14. Reports and evidence

- `reports\run-<yyyyMMdd-HHmmss>.md` — one section per scenario: fixture, verdict, deterministic /
  vision results, AT-SPI JSON, raw vision answer, summary list.
- `shots\<Scenario>-<state>.png` — screenshot per scenario that defines `Shot`.
- Ad-hoc screenshots via `Save-VmShot -Name <name>`.
- Guest app log: `/tmp/simplelauncher.log`; daily logs under
  `~/.local/share/SimpleLauncher/` (also checked by DEBUG-01).

---

## Appendix A — `fixture.py` (full source)

```python
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
```

## Appendix B — `lib.ps1` function reference

| Function | Contract |
|---|---|
| `Get-VmCredential` / `Get-VmSession` / `Close-VmSession` | Cached Posh-SSH session (reconnect on failure) |
| `Invoke-VmShell -Command -TimeoutSec` | Run a shell command; returns `{Output, ExitStatus}` |
| `Invoke-VmXdo -Script -TimeoutSec` | Same but exports `$script:VmEnv` first |
| `Invoke-VmPython -Script -TimeoutSec` | `python3 -u -` heredoc with `timeout -k 5`; output combined |
| `Deploy-VmNav` | Creates `/home/vm/vision` and SCPs `atspi_nav.py` + `fixture.py` |
| `Stop-VmApp` / `Start-VmApp -Arguments -SettleSeconds` | Kill **all** instances by exact exe path; `setsid` start; `Start-VmApp` waits for the AT-SPI frame and retries (3 starts) |
| `Set-VmFixture -Name seed\|seeded\|empty\|clean-state\|dump` | Run `fixture.py` on the guest (warns when the DB is missing) |
| `Restart-VmApp -SettleSeconds -Fixture -Arguments` | Stop → fixture → start |
| `Test-VmAtspiReady` / `Test-VmNavHealth` | True when the app frame is in the AT-SPI tree |
| `Save-VmShot -Name` | `gnome-screenshot` in the guest, SCP to `shots\`; returns host path |
| `Invoke-VisionCheck -Image -Prompt -Model -MaxTokens` | Runs `ask_vision.py` (key from user env); retries provider 5xx/timeouts 3× |
| `Get-VmWindowList` | `xwininfo` top-level window list (debugging) |
| `Get-SuiteVerdict -Text` / `Get-AtspiResult -Output` | Parse `VERDICT:` and `SL_RESULT:` |

## Appendix C — scenario inventory

The live inventory (ids, coverage, fixture, pass/fail status, open items) is maintained in
`ManualTests.md` → "GUI testing runbook (Avalonia / Linux VM)" §6. Add new scenarios there when
extending coverage.
