param(
    [string[]]$Only,
    [string]$Model = 'xiaomi/mimo-v2.6-flash',
    [switch]$SkipRestart
)

. "$PSScriptRoot\lib.ps1"
New-Item -ItemType Directory -Path $script:ReportRoot -Force | Out-Null

if ($Only) {
    $Only = @($Only | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
}

Deploy-VmNav

# Shared Python prelude: imports, Nav instance, helpers, try:
$pyCommon = @'
import glob, json, os, re, sqlite3, subprocess, sys, time, traceback
sys.path.insert(0, "/home/vm/vision")
from atspi_nav import Nav
import pyatspi
n = Nav()
checks, extra = {}, {}
def xdo(cmd):
    subprocess.run(["bash", "-lc", "export DISPLAY=:0 XAUTHORITY=/home/vm/.Xauthority; " + cmd])
def db():
    return sqlite3.connect("/home/vm/.local/share/SimpleLauncher/settings.dat")
def enabled(name, role="push button"):
    e = n.find(name=name, role=role)
    return None if e is None else n.is_enabled(e)
def finish():
    print("SL_RESULT: " + json.dumps({"ok": all(checks.values()), "checks": checks, "extra": extra}))
try:
'@ + "`n"

$pyTail = @'

except Exception as e:
    extra["exception"] = repr(e); traceback.print_exc()
finish()
'@

# Card coordinates for the 1920x1080 maximized window (first card after a
# letter filter narrows the list to a single game).
$CARD1_X = 195
$CARD1_Y = 330

$scenarios = @(
    @{
        Id      = 'EASY-01'
        Name    = 'Easy Mode: system dropdown populated and sorted'
        Fixture = 'empty'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window()
    wel = n.wait_for(name="Welcome", role="frame", timeout=25)
    checks["welcome_present"] = wel is not None
    if wel:
        yes = n.find(name="Yes", role="push button")
        checks["welcome_yes_found"] = yes is not None
        if yes: n.click(yes)
    easy = n.wait_for(name="Add New System", role="frame", timeout=30)
    checks["easy_mode_open"] = easy is not None
    if easy:
        # The system list loads from local XML/API/fallback; wait for the loading overlay to clear.
        deadline = time.time() + 90
        while time.time() < deadline:
            if n.find(name="Loading configuration", contains=True) is None:
                break
            time.sleep(1.0)
        combo = None
        deadline = time.time() + 45
        while time.time() < deadline and not combo:
            combo = n.find(id="SystemNameCombo")
            if not combo: time.sleep(1.0)
        checks["combo_present"] = combo is not None
        if combo:
            items = []
            deadline = time.time() + 40
            while time.time() < deadline:
                n.expand(combo); time.sleep(1.2)
                items = [e for e in n.find_all(role="list item")
                         if e.name and e.extents
                         and e.extents[1] > combo.extents[1] + combo.extents[3] - 5]
                if len(items) >= 5:
                    break
                n.press("Escape"); time.sleep(0.8)
            names = [e.name for e in items]
            checks["combo_populated"] = len(names) >= 5
            checks["combo_sorted"] = names[:10] == sorted(names)[:10]
            extra["first_five"] = names[:5]
            extra["item_roles"] = sorted({e.role for e in items})
        add = n.find(name="Add System", role="push button")
        checks["add_system_present"] = add is not None
        if add:
            checks["add_system_disabled"] = not add.acc.getState().contains(pyatspi.STATE_ENABLED)
        dl = n.find(name="Download Emulator", role="push button")
        if dl:
            checks["download_emulator_disabled"] = not dl.acc.getState().contains(pyatspi.STATE_ENABLED)
        deadline = time.time() + 20
        while time.time() < deadline:
            toast = [e for e in n.snapshot() if e.name and "attention" in e.name.lower()]
            if not toast:
                break
            time.sleep(1.0)
'@ + $pyTail
        Shot    = 'EASY-01-dropdown'
        Prompt  = @'
The "Add New System" window of Simple Launcher (Avalonia, Linux) should be open, with its "Select a system to add" dropdown expanded.
Expected:
- The dropdown lists game systems (e.g. Amstrad CPC, Arcade, Atari 2600, Commodore 64...) in alphabetical order and is not empty.
- The "Download Emulator" and "Download Core" buttons appear disabled because no system is selected.
- An "Add System" button exists and appears disabled.
Note on disabled styling: this app renders disabled buttons with a pale, faded fill and faded label text (low contrast); enabled buttons use the solid accent colour with high-contrast text. Judge "disabled" by the faded/low-contrast appearance, not by the presence of a blue hue.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report: first five system names visible, whether the buttons look disabled, and any mismatch. Be brief.
'@
    },
    @{
        Id      = 'EASY-02'
        Name    = 'Easy Mode: selecting a system enables its download buttons'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_menu("Edit System"); time.sleep(0.8)
    n.click_menu_item("Add New System")
    combo = n.wait_for(id="SystemNameCombo", timeout=25)
    checks["combo_present"] = combo is not None
    if combo:
        n.expand(combo); time.sleep(1.5)
        pick = [e for e in n.snapshot()
                if e.name == "Atari 2600" and e.extents and e.extents[1] > 380]
        checks["pick_found"] = bool(pick)
        if pick:
            n.click(pick[0]); time.sleep(2.5)
            extra["buttons"] = {k: enabled(k) for k in
                                ("Add System", "Download Emulator", "Download Core")}
            checks["add_system_disabled"] = enabled("Add System") is False
            checks["download_emulator_enabled"] = enabled("Download Emulator") is True
            checks["download_core_enabled"] = enabled("Download Core") is True
'@ + $pyTail
        Shot    = 'EASY-02-selected'
        Prompt  = @'
The "Add New System" window of Simple Launcher (Avalonia, Linux) should be open with "Atari 2600" selected in the "Select a system to add" dropdown.
Expected:
- "Download Emulator" and "Download Core" buttons now look enabled (solid accent colour, high-contrast text).
- "Add System" still looks disabled (faded, low contrast) because the emulator is not downloaded yet.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report the button appearances and any mismatch. Be brief.
'@
    },
    @{
        Id      = 'MENU-00'
        Name    = 'Main window renders; menu bar has no Windows-only Tools'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.close_welcome(); n.press("Escape"); n.close_dialogs(); n.activate_window(); n.maximize_window()
    frame = n.wait_for(name="Simple Launcher", role="frame", timeout=15)
    checks["window_present"] = frame is not None
    ext = n.frame_extents()
    tops = {}
    for e in n.find_all(role="menu item"):
        if e.extents and ext and e.extents[1] < ext[1] + 40 and e.name:
            tops[e.name] = e.extents[0]
    got = [k for k, _ in sorted(tops.items(), key=lambda kv: kv[1])]
    expected = ["Options", "Edit System", "Select Window", "Donate", "About"]
    checks["menu_exact"] = got == expected
    checks["no_tools_menu"] = "Tools" not in tops
    extra["menu"] = got
'@ + $pyTail
        Shot    = 'MENU-00-main'
        Prompt  = @'
This is the main window of Simple Launcher (Avalonia build) on Linux Mint.
Expected:
- The main window is rendered (no blank/garbled areas).
- Window title is "Simple Launcher".
- The menu bar contains exactly these top-level items: Options, Edit System, Select Window, Donate, About.
- There is NO "Tools" menu (Tools is Windows-only and must be hidden on Linux).
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then list any mismatches briefly.
'@
    },
    @{
        Id      = 'MENU-01'
        Name    = 'Options menu contents (Linux set)'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.close_welcome(); n.press("Escape"); n.close_dialogs(); n.activate_window(); n.maximize_window()
    n.open_menu("Options")
    items = sorted({e.name for e in n.popup_items() if e.name})
    expected = {"Language", "Theme", "Set Button Size", "Set Button Aspect Ratio",
                "Set Number of Games Per Page", "View Mode", "Show Games", "Filename Preferences",
                "Edit Links", "Gamepad Support", "Fuzzy Image Matching", "Sound Configuration",
                "Retro Achievements Settings", "Overlay Button Settings"}
    checks["all_expected_present"] = expected.issubset(set(items))
    checks["no_windows_only_item"] = not any("Inject Emulator Config" in x for x in items)
    checks["exact_set"] = set(items) == expected
    extra["items"] = items
'@ + $pyTail
        Shot    = 'MENU-01-options'
        Prompt  = @'
This is the "Options" dropdown menu of Simple Launcher (Avalonia, Linux).
Expected items (all must be present): Language, Theme, Set Button Size, Set Button Aspect Ratio, Set Number of Games Per Page, View Mode, Show Games, Filename Preferences, Edit Links, Gamepad Support, Fuzzy Image Matching, Sound Configuration, Retro Achievements Settings, Overlay Button Settings.
An item "Inject Emulator Config" must NOT be present (Windows-only, hidden on Linux).
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then list any missing or unexpected items. Be brief.
'@
    },
    @{
        Id      = 'MENU-02'
        Name    = 'About menu contents'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.close_welcome(); n.press("Escape"); n.close_dialogs(); n.activate_window(); n.maximize_window()
    n.open_menu("About")
    items = sorted({e.name for e in n.popup_items() if e.name})
    expected = {"About", "Support", "Open AppData Path", "Exit"}
    checks["exact_set"] = set(items) == expected
    extra["items"] = items
'@ + $pyTail
        Shot    = 'MENU-02-about'
        Prompt  = @'
This is the "About" dropdown menu of Simple Launcher (Avalonia, Linux).
Expected items: About, Support, Open AppData Path, Exit.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then list any missing or unexpected items. Be brief.
'@
    },
    @{
        Id      = 'DIALOG-01'
        Name    = 'About window opens and shows a version'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.close_welcome(); n.press("Escape"); n.close_dialogs(); n.activate_window(); n.maximize_window()
    n.open_menu("About")
    n.click_menu_item("About")
    frame = n.wait_for(name="About", role="frame", timeout=15)
    checks["about_open"] = frame is not None
    if frame:
        ext = frame.extents
        labels = [e.name for e in n.snapshot()
                  if e.role == "label" and e.name and e.extents
                  and e.extents[0] >= ext[0] and e.extents[1] >= ext[1]]
        extra["labels"] = labels[:12]
        checks["has_version_string"] = any(re.search(r"\d+\.\d+", x) for x in labels)
'@ + $pyTail
        Shot    = 'DIALOG-01-about-window'
        Prompt  = @'
An About window/dialog for Simple Launcher (Avalonia, Linux) should now be open.
Expected: it shows the application name ("Simple Launcher") and a version string, plus a close control.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report: the exact version text visible and any visible buttons. Be brief.
'@
    },
    @{
        Id      = 'DIALOG-02'
        Name    = 'Sound Configuration window opens'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.close_welcome(); n.press("Escape"); n.close_dialogs(); n.activate_window(); n.maximize_window()
    n.open_menu("Options")
    n.click_menu_item("Sound Configuration")
    time.sleep(0.7)
    n.click_menu_item("Sound Configuration")
    frame = n.wait_for(name="Sound Configuration", role="frame", timeout=20)
    checks["window_open"] = frame is not None
    if frame:
        ext = frame.extents
        labels = [e.name for e in n.snapshot()
                  if e.role in ("label", "check box") and e.name and e.extents
                  and e.extents[0] >= ext[0] and e.extents[1] >= ext[1]]
        extra["labels"] = labels[:15]
        checks["has_controls"] = len(labels) >= 3
'@ + $pyTail
        Shot    = 'DIALOG-02-sound'
        Prompt  = @'
A "Sound Configuration" window for Simple Launcher (Avalonia, Linux) should now be open (opened from Options > Sound Configuration).
Expected: controls for enabling sounds, the current sound/notification file, and Save/Cancel or OK buttons.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then list the visible controls and any mismatch. Be brief.
'@
    },
    @{
        Id      = 'DIALOG-03'
        Name    = 'Edit System menu structure; no Windows-only scan option'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.close_welcome(); n.press("Escape"); n.close_dialogs(); n.activate_window(); n.maximize_window()
    n.open_menu("Edit System")
    items = [e.name for e in n.popup_items() if e.name]
    expected = {"Add New System", "Edit System", "Download Image Pack",
                "Rescan RetroAchievements for the Selected System"}
    checks["exact_set"] = set(items) == expected
    checks["no_windows_scan"] = not any("Scan for Microsoft Windows" in x for x in items)
    extra["items"] = items
    n.click_menu_item("Add New System")
    easy = n.wait_for(name="Add New System", role="frame", timeout=20)
    checks["easy_mode_opens"] = easy is not None
'@ + $pyTail
        Shot    = 'DIALOG-03-edit-system-menu'
    },
    @{
        Id      = 'SYS-01'
        Name    = 'Configured systems load; grid renders games and covers'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.ensure_grid_view()
    checks["grid_present"] = n.find(id="GameGridView") is not None
    names = sorted({e.name for e in n.find_all(role="list item") if e.name})
    extra["pre_render_items"] = names
    checks["test_games_listed"] = {"Alpha Quest", "Beta Blaster",
                                   "Captain Nemo (USA)", "Zipped Quest"}.issubset(set(names))
    n.filter_letter("All", wait=3.0)
    count = n.pagination_count()
    extra["pagination_all"] = count
    extra["status_left"] = n.status_left()
    checks["all_games_counted"] = count is not None and count >= 4
    checks["status_shows_all"] = (n.status_left() or "").startswith("All")
'@ + $pyTail
        Shot    = 'SYS-01-grid'
        Prompt  = @'
The main window of Simple Launcher (Avalonia, Linux) should show the game grid for a test library (4 games of the selected test system).
Expected:
- Four game cards are visible with filename labels: Captain Nemo (USA), Beta Blaster, Zipped Quest, Alpha Quest (any left-to-right order).
- Alpha Quest shows a solid blue cover; Beta Blaster a solid orange/yellow cover.
- Captain Nemo (USA) and Zipped Quest show a dark placeholder image (they have no cover art).
- The status bar at the bottom reports "Displaying files 1 to 4 out of 4 total".
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report the visible card labels and any mismatch. Be brief.
'@
    },
    @{
        Id      = 'SYS-02'
        Name    = 'System with an invalid ROM folder reports the error'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    for _ in range(4):
        if n.find(id="GameGridView") is None and n.find(name="Select System", role="label"):
            break
        n.press("Escape"); time.sleep(1.3)
    n.click_card("Broken System", timeout=25); time.sleep(3.0)
    frames = [e.name for e in n.find_all(role="frame") if e.name != "Simple Launcher"]
    extra["frames"] = frames
    checks["error_dialog"] = "Errors" in frames
    labels = [e.name for e in n.find_all(role="label") if e.name and "not valid" in e.name.lower()]
    extra["error_text"] = labels[:1]
    checks["folder_reported"] = any("System Folder path is not valid" in x for x in labels)
    checks["image_folder_reported"] = any("System Image Folder path is not valid" in x for x in labels)
'@ + $pyTail
        Shot    = 'SYS-02-broken-system'
        Prompt  = @'
An error dialog for Simple Launcher (Avalonia, Linux) should be visible after selecting a system whose ROM folder does not exist.
Expected: a dialog titled "Errors" (or similar) reporting that the "System Folder path is not valid or does not exist" and that the "System Image Folder path is not valid or does not exist", with an OK button.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report the visible error text and any mismatch. Be brief.
'@
    },
    @{
        Id      = 'FILTER-01'
        Name    = 'Filter bar: letter filter narrows the list, All resets it'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.ensure_grid_view()
    n.filter_letter("All", wait=3.0)
    extra["count_all"] = n.pagination_count()
    checks["all_shows_everything"] = (n.pagination_count() or 0) >= 4
    n.filter_letter("B")
    extra["count_b"] = n.pagination_count()
    extra["status_b"] = n.status_left()
    checks["b_filters_to_one"] = n.pagination_count() == 1
    checks["b_sets_status"] = "B" in (n.status_left() or "")
'@ + $pyTail
        Shot    = 'FILTER-01-letter-b'
        Prompt  = @'
The game grid of Simple Launcher (Avalonia, Linux) should now be filtered by the letter "B".
Expected:
- Exactly one game card is visible: "Beta Blaster" (solid orange/yellow cover).
- The letter button "B" in the filter bar at the top is highlighted/selected (accent colour).
- The status bar at the bottom reports "Displaying files 1 to 1 out of 1 total".
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report what is visible and any mismatch. Be brief.
'@
    },
    @{
        Id      = 'SEARCH-01'
        Name    = 'Search: matching, no-match message, clearing restores'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.filter_letter("All", wait=3.0)
    box = n.find(id="SearchBox")
    n.set_text(box, "Alpha"); time.sleep(0.8)
    n.press("Return"); time.sleep(2.5)
    extra["count_alpha"] = n.pagination_count()
    checks["alpha_filters_one"] = n.pagination_count() == 1
    box = n.find(id="SearchBox")
    n.set_text(box, "zzz"); time.sleep(0.8)
    n.press("Return"); time.sleep(2.5)
    labels = [e.name for e in n.find_all(role="label") if e.name]
    extra["no_match"] = [x for x in labels if "no games" in x.lower()
                         or "no games matched" in x.lower()][:2]
    extra["no_match_pagination"] = n.pagination_count()
    checks["no_match_message"] = (any("no games" in x.lower() for x in labels)
                                  and n.pagination_count() == 0)
    box = n.find(id="SearchBox")
    n.set_text(box, ""); time.sleep(0.8)
    n.press("Return"); time.sleep(2.5)
    extra["count_cleared"] = n.pagination_count()
    checks["clear_restores"] = (n.pagination_count() or 0) >= 4
'@ + $pyTail
    },
    @{
        Id      = 'VIEW-01'
        Name    = 'Toggle view mode: grid -> list'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.ensure_grid_view()
    n.filter_letter("All", wait=3.0)
    n.click(n.find(id="NavToggleViewModeButton")); time.sleep(2.5)
    checks["datagrid_present"] = n.find(id="GameDataGrid") is not None
    extra["cells"] = [e.name for e in n.find_all(role="table cell")][:6]
'@ + $pyTail
        Shot    = 'VIEW-01-list'
        Prompt  = @'
The main window of Simple Launcher (Avalonia, Linux) should now show the game library in LIST view (not grid view).
Expected: a data table/list with four rows (Alpha Quest, Beta Blaster, Captain Nemo (USA), Zipped Quest) and column headers such as FileName, Folder Path, Times Played, PlayTime, Preview Image.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report the visible rows/columns and any mismatch. Be brief.
'@
    },
    @{
        Id      = 'VIEW-02'
        Name    = 'View Mode menu checkmarks track the selected view; grid restored'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    if n.find(id="GameDataGrid") is not None:
        n.click(n.find(id="NavToggleViewModeButton")); time.sleep(2.0)
    checks["grid_restored"] = n.find(id="GameGridView") is not None
    n.open_menu("Options"); time.sleep(0.8)
    n.click_menu_item("View Mode"); time.sleep(1.0)
    checked = [e.name for e in n.popup_items() if e.name and n.is_checked(e) and e.extents[0] > 100]
    extra["checked_initial"] = checked
    checks["grid_checked"] = "Grid View" in checked
    n.click_menu_item("List View"); time.sleep(1.8)
    n.open_menu("Options"); time.sleep(0.8)
    n.click_menu_item("View Mode"); time.sleep(1.0)
    checked2 = [e.name for e in n.popup_items() if e.name and n.is_checked(e) and e.extents[0] > 100]
    extra["checked_after"] = checked2
    checks["list_checked"] = "List View" in checked2
    n.click_menu_item("Grid View"); time.sleep(1.8)
    n.press("Escape"); time.sleep(0.5)
'@ + $pyTail
    },
    @{
        Id      = 'THEME-01'
        Name    = 'Theme menu: checkmark and switching to Dark'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.open_menu("Options"); time.sleep(0.8)
    n.click_menu_item("Theme"); time.sleep(1.0)
    n.click_menu_item("Base Theme"); time.sleep(1.0)
    checked = [e.name for e in n.popup_items() if e.name and n.is_checked(e) and e.extents[0] > 100]
    extra["initial_checked"] = checked
    checks["one_initial_checkmark"] = len(checked) == 1
    n.click_menu_item("Dark"); time.sleep(2.5)
    n.open_menu("Options"); time.sleep(0.8)
    n.click_menu_item("Theme"); time.sleep(1.0)
    n.click_menu_item("Base Theme"); time.sleep(1.0)
    checked2 = [e.name for e in n.popup_items() if e.name and n.is_checked(e) and e.extents[0] > 100]
    extra["after_checked"] = checked2
    checks["dark_checked"] = "Dark" in checked2
    n.press("Escape"); time.sleep(0.5)
    c = db()
    rows = c.execute("select Key, Value from AppSettings where Key like '%heme%'").fetchall()
    c.close()
    extra["theme_setting"] = rows
    checks["theme_saved"] = any("dark" in str(v).lower() for _, v in rows)
'@ + $pyTail
        Shot    = 'THEME-01-dark'
        Prompt  = @'
Simple Launcher (Avalonia, Linux) should now be in DARK theme.
Expected: the whole window (menu bar, toolbar, game grid background, status bar) uses dark colours with light text; there must be no white/light background panels mixed in.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report whether the UI is consistently dark and any mismatch. Be brief.
'@
    },
    @{
        Id      = 'THEME-02'
        Name    = 'Theme persists across restart; switch back to Adaptive'
        Fixture = 'seeded'
        Restart = $true
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.open_menu("Options"); time.sleep(0.8)
    n.click_menu_item("Theme"); time.sleep(1.0)
    n.click_menu_item("Base Theme"); time.sleep(1.0)
    checked = [e.name for e in n.popup_items() if e.name and n.is_checked(e) and e.extents[0] > 100]
    extra["after_restart_checked"] = checked
    checks["dark_persisted"] = "Dark" in checked
    n.click_menu_item("Adaptive (Sync with Windows)"); time.sleep(2.5)
    n.open_menu("Options"); time.sleep(0.8)
    n.click_menu_item("Theme"); time.sleep(1.0)
    n.click_menu_item("Base Theme"); time.sleep(1.0)
    checked2 = [e.name for e in n.popup_items() if e.name and n.is_checked(e) and e.extents[0] > 100]
    extra["adaptive_checked"] = checked2
    checks["adaptive_checked"] = "Adaptive (Sync with Windows)" in checked2
    n.press("Escape"); time.sleep(0.5)
'@ + $pyTail
        Shot    = 'THEME-02-adaptive'
        Prompt  = @'
Simple Launcher (Avalonia, Linux) should now be back in the ADAPTIVE theme, following the machine theme (this VM uses a light system theme).
Expected: the UI is light with dark text and readable controls; no mixed dark/light panels.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report whether the UI is consistently light and any mismatch. Be brief.
'@
    },
    @{
        Id      = 'EDIT-01'
        Name    = 'Edit System window opens with the selected system loaded'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.click(n.find(id="NavEditSystemButton")); time.sleep(3.0)
    frame = n.wait_for(name="Edit _System", role="frame", timeout=15)
    checks["edit_open"] = frame is not None
    labels = {e.name for e in n.find_all(role="label") if e.name}
    extra["key_labels"] = sorted(x for x in labels if x in
                                 ("System Name", "System Folder (ROMs)", "Save or Update System",
                                  "Delete", "Close", "＋ Add New", "❓ Help"))
    checks["key_labels"] = {"System Name", "System Folder (ROMs)",
                            "Save or Update System"}.issubset(labels)
    entries = [n.read_text(e) for e in n.find_all(role="entry")]
    extra["entry_values"] = [t for t in entries if t][:8]
    checks["name_loaded"] = any((t or "").strip() == "Test System" for t in entries)
    checks["help_button"] = n.find(name="Help", contains=True) is not None
'@ + $pyTail
        Shot    = 'EDIT-01-window'
        Prompt  = @'
The "Edit System" window of Simple Launcher (Avalonia, Linux) should be open for a system called "Test System".
Expected in the visible area:
- Fields are populated: System Name = "Test System", System Folder (ROMs) = "/home/vm/roms/Test System".
- Buttons: "+ Add New", "Save or Update System", "Delete", "? Help", "Close".
- The right-hand pane titled "Developer Suggestion" shows the help text "No information available for system Test System".
- The System Image Folder field and the emulator section are further down the scrollable form and are NOT expected to be visible without scrolling.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report the visible field values and buttons, and any mismatch. Be brief.
'@
    },
    @{
        Id      = 'HELP-01'
        Name    = 'Edit System help pane shows help content'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.click(n.find(id="NavEditSystemButton")); time.sleep(3.0)
    checks["help_button"] = n.find(name="Help", contains=True) is not None
    labels = [e.name for e in n.find_all(role="label") if e.name]
    checks["help_pane_title"] = any("Developer Suggestion" in t for t in labels)
    checks["help_content_shown"] = any("No information available for system Test System" in t for t in labels)
    extra["help_texts"] = [t for t in labels if len(t) > 15][:6]
'@ + $pyTail
        Shot    = 'HELP-01-pane'
        Prompt  = @'
The "Edit System" window of Simple Launcher (Avalonia, Linux) should show a Help pane (titled "Developer Suggestion") with parameter documentation text, or a "no details available" style message for an unknown system.
Expected: a readable help panel with text such as "No information available for system Test System", not an empty or blank area.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then describe the help content briefly. Be brief.
'@
    },
    @{
        Id      = 'DIALOG-04'
        Name    = 'Fuzzy matching threshold dialog: slider and persistence'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.open_menu("Options"); time.sleep(0.8)
    n.click_menu_item("Fuzzy Image Matching"); time.sleep(1.0)
    n.click_menu_item("SetThreshold"); time.sleep(2.0)
    frame = n.wait_for(name="Set Fuzzy Matching Threshold", role="frame", timeout=15)
    checks["dialog_open"] = frame is not None
    slider = n.find(name="New Threshold:", role="slider")
    checks["slider_present"] = slider is not None
    if slider:
        v0 = n.read_value(slider)
        extra["initial_value"] = v0
        checks["initial_80"] = v0 is not None and abs(v0 - 0.8) < 0.06
        n.set_value(slider, 0.9); time.sleep(1.0)
        fresh = n.find(name="New Threshold:", role="slider")
        v1 = n.read_value(fresh) if fresh else None
        extra["after_set"] = v1
        if v1 is None or abs(v1 - 0.9) > 0.06:
            ext = (fresh or slider).extents
            n.click_at(int(ext[0] + ext[2] * 0.95), int(ext[1] + ext[3] / 2)); time.sleep(0.8)
            fresh = n.find(name="New Threshold:", role="slider")
            v1 = n.read_value(fresh) if fresh else None
            extra["after_click"] = v1
        checks["slider_moved"] = v1 is not None and abs(v1 - 0.9) < 0.06
        ok = n.find(name="OK", role="push button")
        if ok: n.click(ok); time.sleep(1.5)
        n.open_menu("Options"); time.sleep(0.8)
        n.click_menu_item("Fuzzy Image Matching"); time.sleep(1.0)
        n.click_menu_item("SetThreshold"); time.sleep(2.0)
        slider2 = n.find(name="New Threshold:", role="slider")
        v2 = n.read_value(slider2) if slider2 else None
        extra["reopen_value"] = v2
        checks["persisted_90"] = v2 is not None and abs(v2 - 0.9) < 0.06
        if slider2:
            n.set_value(slider2, 0.8); time.sleep(0.8)
        ok2 = n.find(name="OK", role="push button")
        if ok2: n.click(ok2); time.sleep(1.0)
        # Reopen so the screenshot shows the dialog (next scenario closes it).
        n.open_menu("Options"); time.sleep(0.8)
        n.click_menu_item("Fuzzy Image Matching"); time.sleep(1.0)
        n.click_menu_item("SetThreshold"); time.sleep(2.0)
'@ + $pyTail
        Shot    = 'DIALOG-04-threshold'
        Prompt  = @'
The "Set Fuzzy Matching Threshold" dialog of Simple Launcher (Avalonia, Linux) should be visible.
Expected: a slider labeled "New Threshold:" showing a percentage (80% or 90%), a "Current Threshold:" label, explanatory text about low/high thresholds, and OK/Cancel buttons.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report the visible threshold values and any mismatch. Be brief.
'@
    },
    @{
        Id      = 'DIALOG-05'
        Name    = 'Gamepad dead zone dialog: sliders and cancel'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.open_menu("Options"); time.sleep(0.8)
    n.click_menu_item("Gamepad Support"); time.sleep(1.0)
    n.click_menu_item("Set Gamepad DeadZone"); time.sleep(2.0)
    frame = n.wait_for(name="Set Gamepad DeadZone", role="frame", timeout=15)
    checks["dialog_open"] = frame is not None
    sx = n.find(name="Dead Zone X", role="slider")
    sy = n.find(name="Dead Zone Y", role="slider")
    checks["sliders_present"] = sx is not None and sy is not None
    extra["values"] = [n.read_value(sx) if sx else None, n.read_value(sy) if sy else None]
    cancel = n.find(name="Cancel", role="push button")
    checks["cancel_present"] = cancel is not None
    if cancel: n.click(cancel); time.sleep(1.0)
    checks["closed"] = n.wait_gone(name="Set Gamepad DeadZone", role="frame", timeout=8)
'@ + $pyTail
    },
    @{
        Id      = 'DIALOG-06'
        Name    = 'Edit Links window opens with URL fields'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.open_menu("Options"); time.sleep(0.8)
    n.click_menu_item("Edit Links"); time.sleep(0.8)
    n.click_menu_item("Edit Links"); time.sleep(2.5)
    frame = n.wait_for(name="Edit Links", role="frame", timeout=15)
    checks["window_open"] = frame is not None
    entries = [n.read_text(e) for e in n.find_all(role="entry")]
    extra["urls"] = [t for t in entries if t][:6]
    checks["has_url_fields"] = len([t for t in entries if t and "http" in t]) >= 2
    buttons = {e.name for e in n.find_all(role="push button") if e.name}
    extra["buttons"] = sorted(b for b in buttons if b in ("Save", "Cancel", "Revert", "Reset"))
    checks["has_actions"] = bool(buttons & {"Save", "Cancel", "Revert", "Reset"})
'@ + $pyTail
        Shot    = 'DIALOG-06-links'
        Prompt  = @'
The "Edit Links" window of Simple Launcher (Avalonia, Linux) should be open.
Expected: text fields containing URL templates (e.g. video/info link templates starting with https://), plus Save/Cancel/Revert buttons.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report the visible fields and buttons, and any mismatch. Be brief.
'@
    },
    @{
        Id      = 'SOUND-01'
        Name    = 'Sound Configuration: disabling sounds disables its controls'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.open_menu("Options"); time.sleep(0.8)
    n.click_menu_item("Sound Configuration"); time.sleep(0.8)
    n.click_menu_item("Sound Configuration"); time.sleep(2.5)
    frame = n.wait_for(name="Sound Configuration", role="frame", timeout=15)
    checks["window_open"] = frame is not None
    cb = n.find(name="Enable notification sound", role="check box")
    checks["checkbox_present"] = cb is not None
    if cb:
        checks["initially_checked"] = n.is_checked(cb)
        n.toggle(cb); time.sleep(1.0)
        cb2 = n.find(name="Enable notification sound", role="check box")
        extra["after_toggle"] = {
            "checked": n.is_checked(cb2) if cb2 else None,
            "choose_enabled": enabled("Choose file"),
            "play_enabled": enabled("Play current sound")}
        checks["toggle_unchecks"] = cb2 is not None and not n.is_checked(cb2)
        checks["controls_disabled"] = enabled("Choose file") is False and enabled("Play current sound") is False
        if cb2: n.toggle(cb2); time.sleep(1.0)
        checks["controls_reenabled"] = enabled("Choose file") is True and enabled("Play current sound") is True
    cancel = n.find(name="Cancel", role="push button")
    if cancel: n.click(cancel); time.sleep(1.0)
    checks["closed"] = n.wait_gone(name="Sound Configuration", role="frame", timeout=8)
'@ + $pyTail
    },
    @{
        Id      = 'ABOUT-02'
        Name    = 'About window version + Update History renders WhatsNew'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.open_menu("About"); time.sleep(0.8)
    n.click_menu_item("About"); time.sleep(2.5)
    about = n.wait_for(name="About", role="frame", timeout=15)
    checks["about_open"] = about is not None
    labels = [e.name for e in n.find_all(role="label") if e.name]
    checks["version_shown"] = any("Version" in x and re.search(r"\d+\.\d+", x) for x in labels)
    hist = n.find(name="Update History", role="push button")
    checks["history_button"] = hist is not None
    if hist:
        n.click(hist); time.sleep(3.0)
        hf = n.wait_for(name="Update History", role="frame", timeout=15)
        checks["history_open"] = hf is not None
        if hf:
            texts = [e.name for e in n.find_all(role="label") if e.name and len(e.name) > 20]
            extra["history_texts"] = texts[:4]
            checks["history_has_text"] = len(texts) >= 1
'@ + $pyTail
        Shot    = 'ABOUT-02-history'
        Prompt  = @'
The "Update History" window of Simple Launcher (Avalonia, Linux) should be open.
Expected: rendered markdown content from the What's New file (headings, version numbers, bullet lists) - readable text, not an empty window.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report the visible content briefly. Be brief.
'@
    },
    @{
        Id      = 'SUPPORT-01'
        Name    = 'Support window opens and validates an empty request'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.open_menu("About"); time.sleep(0.8)
    n.click_menu_item("Support"); time.sleep(3.0)
    sup = n.wait_for(name="Support", role="frame", timeout=15)
    checks["support_open"] = sup is not None
    send = n.find(name="Send Support Request", role="push button")
    checks["send_present"] = send is not None
    if send:
        n.click(send); time.sleep(2.5)
        labels = [e.name for e in n.find_all(role="label") if e.name]
        extra["validation_labels"] = [x for x in labels
                                      if "please enter" in x.lower() or "provide" in x.lower()][:3]
        checks["validation_shown"] = any("please enter the name" in x.lower()
                                         or "enter the details" in x.lower() for x in labels)
'@ + $pyTail
        Shot    = 'SUPPORT-01-window'
        Prompt  = @'
The "Support" window of Simple Launcher (Avalonia, Linux) should be visible, after clicking "Send Support Request" with an empty form.
Expected: the support form (fields for name/email/description) is still shown and a validation message asks the user to fill the first missing field (e.g. "Please enter the name.").
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report the visible message and form fields. Be brief.
'@
    },
    @{
        Id      = 'FAV-01'
        Name    = 'Favorites: add via context menu, page lists the game'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.ensure_grid_view()
    n.filter_letter("A")
    n.click_at(195, 330, button=3)
    n.click_context_item("Add To Favorites")
    n.wait_extra_frames_gone(timeout=10)
    c = db()
    favs = c.execute("select FileName, SystemName from Favorites").fetchall()
    c.close()
    extra["favorites_after_add"] = favs
    checks["favorite_added"] = any(f[0] == "Alpha Quest.nes" for f in favs)
    n.click(n.find(id="NavFavoritesButton")); time.sleep(4.0)
    checks["favorites_page_open"] = (n.find(name="List of your favorite games") is not None
                                     or n.find(name="Favorites", role="label") is not None)
'@ + $pyTail
        Shot    = 'FAV-01-page'
        Prompt  = @'
The Favorites page of Simple Launcher (Avalonia, Linux) should be open.
Expected: the page is titled "Favorites" and lists one favorite game, "Alpha Quest", with its system "Test System".
The preview image area may be empty/black until the row is selected - that is acceptable (cover rendering is verified elsewhere).
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report the visible rows and any mismatch. Be brief.
'@
    },
    @{
        Id      = 'FAV-02'
        Name    = 'Favorites: remove via context menu'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.press("Escape"); time.sleep(1.5)
    n.open_system("Test System", timeout=40)
    n.ensure_grid_view()
    n.filter_letter("A")
    n.click_at(195, 330, button=3)
    n.click_context_item("Remove From Favorites")
    n.wait_extra_frames_gone(timeout=10)
    c = db()
    favs = c.execute("select FileName from Favorites").fetchall()
    c.close()
    extra["favorites_after_remove"] = favs
    checks["favorite_removed"] = len(favs) == 0
'@ + $pyTail
    },
    @{
        Id      = 'HIST-01'
        Name    = 'Launching a game records play history'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.ensure_grid_view()
    n.filter_letter("A")
    n.click_at(195, 330, button=3)
    n.click_context_item("Launch Game")
    time.sleep(12.0)
    log = ""
    if os.path.exists("/tmp/dummy-emulator.log"):
        log = open("/tmp/dummy-emulator.log").read()
    checks["emulator_started"] = "Alpha Quest" in log
    c = db()
    rows = c.execute("select FileName, TimesPlayed, TotalPlayTime from PlayHistory").fetchall()
    c.close()
    extra["history_rows"] = rows
    checks["history_recorded"] = any(os.path.basename(r[0]) == "Alpha Quest.nes" and r[1] >= 1 for r in rows)
    n.click(n.find(id="NavHistoryButton")); time.sleep(2.5)
    checks["history_page_open"] = n.find(name="Play History", role="label") is not None
'@ + $pyTail
        Shot    = 'HIST-01-page'
        Prompt  = @'
The Play History page of Simple Launcher (Avalonia, Linux) should be open.
Expected: it lists the game "Alpha Quest" with a play count of 1 and a play time of several seconds (e.g. 00:00:07), and column headers such as date, times played and total play time.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report the visible rows and any mismatch. Be brief.
'@
    },
    @{
        Id      = 'ZIP-01'
        Name    = 'ZIP ROM launch extracts to temp and cleans up after exit'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.ensure_grid_view()
    n.filter_letter("Z")
    n.click_at(195, 330, button=3)
    n.click_context_item("Launch Game")
    time.sleep(3.0)
    log = ""
    if os.path.exists("/tmp/dummy-emulator.log"):
        log = open("/tmp/dummy-emulator.log").read().strip().splitlines()
    launch_path = log[-1] if log else ""
    extra["launch_path"] = launch_path
    checks["extracted_to_temp"] = "/tmp/SimpleLauncher" in launch_path
    checks["rom_name_kept"] = "Zipped Quest" in launch_path
    time.sleep(9.0)
    leftover = os.listdir("/tmp/SimpleLauncher") if os.path.isdir("/tmp/SimpleLauncher") else []
    extra["temp_leftover"] = leftover[:5]
    checks["temp_cleaned"] = not leftover
'@ + $pyTail
    },
    @{
        Id      = 'WATCH-01'
        Name    = 'File watcher auto-refreshes the grid on external changes'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.filter_letter("All", wait=3.0)
    base = n.pagination_count()
    extra["base_count"] = base
    newfile = "/home/vm/roms/Test System/Delta Force.nes"
    with open(newfile, "wb") as fh:
        fh.write(b"NES\x1aDelta" + b"\x00" * 32)
    ok_add = False
    for _ in range(20):
        time.sleep(1.0)
        if (n.pagination_count() or 0) == (base or 0) + 1:
            ok_add = True
            break
    checks["watcher_added"] = ok_add
    if os.path.exists(newfile):
        os.remove(newfile)
    ok_del = False
    for _ in range(20):
        time.sleep(1.0)
        if (n.pagination_count() or 0) == base:
            ok_del = True
            break
    checks["watcher_removed"] = ok_del
    extra["final_count"] = n.pagination_count()
'@ + $pyTail
    },
    @{
        Id         = 'DEBUG-01'
        Name       = 'Debug window opens with -debug and shows live log lines'
        Fixture    = 'seeded'
        LaunchArgs = '-debug'
        Py         = $pyCommon + @'
    n.activate_window(); n.maximize_window()
    frame = n.wait_for(name="Debugger", role="frame", timeout=25)
    checks["debug_window"] = frame is not None
    if frame:
        xdo("xdotool search --name '^Debugger$' windowactivate; sleep 0.6")
        box = n.find(name="Debug log")
        text = n.read_text(box) if box else None
        extra["debug_text_head"] = (text or "")[:200]
        checks["has_log_lines"] = bool((text or "").strip())
    logs = glob.glob("/home/vm/.local/share/SimpleLauncher/*.log")
    extra["log_files"] = logs[:5]
'@ + $pyTail
        Shot       = 'DEBUG-01-window'
        Prompt     = @'
The Debug Window of Simple Launcher (Avalonia, Linux) should be open (the app was started with the -debug flag).
Expected: a window titled "Debugger" with a read-only log area listing log lines with timestamps and levels.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report the visible log lines briefly. Be brief.
'@
    },
    @{
        Id      = 'ROMHIST-01'
        Name    = 'ROM History context item on a non-MAME game reports no history'
        Fixture = 'seeded'
        Py      = $pyCommon + @'
    n.activate_window(); n.maximize_window(); n.close_dialogs(); time.sleep(0.5)
    n.open_system("Test System", timeout=40)
    n.ensure_grid_view()
    n.filter_letter("A")
    n.click_at(195, 330, button=3)
    n.click_context_item("Open ROM History")
    time.sleep(3.0)
    labels = [e.name for e in n.find_all(role="label") if e.name]
    extra["history_labels"] = [x for x in labels
                               if "history" in x.lower() or "not found" in x.lower()][:5]
    frames = [e.name for e in n.find_all(role="frame") if e.name != "Simple Launcher"]
    extra["frames"] = frames
    checks["message_or_dialog"] = bool(extra["history_labels"]) or bool(frames)
'@ + $pyTail
        Shot    = 'ROMHIST-01-message'
        Prompt  = @'
Simple Launcher (Avalonia, Linux) should show a message about ROM history after using "Open ROM History" on a game that is not a MAME game.
Expected: a dialog or prompt stating that no ROM history was found (possibly offering a Google search), or the ROM History window with a "no history" message. It must not crash or show a blank window.
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report what is visible. Be brief.
'@
    }
)

$results = @()
$currentFixture = $null
$currentArgs = ''
foreach ($scenario in $scenarios) {
    if ($Only -and $scenario.Id -notin $Only) { continue }
    $fixture = if ($scenario.Fixture) { $scenario.Fixture } else { 'seeded' }
    $launchArgs = if ($scenario.LaunchArgs) { $scenario.LaunchArgs } else { '' }
    $needRestart = (-not $SkipRestart) -and
        ($fixture -ne $currentFixture -or $scenario.Restart -or $launchArgs -ne $currentArgs)
    if ($needRestart) {
        Restart-VmApp -SettleSeconds 10 -Fixture $fixture -Arguments $launchArgs
        $currentFixture = $fixture
        $currentArgs = $launchArgs
    }
    Write-Host "=== $($scenario.Id) - $($scenario.Name) [fixture=$fixture] ==="
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $run = Invoke-VmPython -Script $scenario.Py -TimeoutSec 300
    $parse = Get-AtspiResult -Output $run.Output
    $checkCount = if ($parse -and $parse.checks) { @($parse.checks.PSObject.Properties).Count } else { 0 }
    $hasException = [bool]($parse -and $parse.extra -and $parse.extra.exception)
    $detVerdict = if ($parse -and $parse.ok -and $checkCount -gt 0 -and -not $hasException) { 'PASS' } else { 'FAIL' }
    $vision = ''
    $visionVerdict = ''
    $image = ''
    if ($scenario.Shot) { $image = Save-VmShot -Name $scenario.Shot }
    if ($scenario.Prompt -and $image) {
        $vision = Invoke-VisionCheck -Image $image -Prompt $scenario.Prompt -Model $Model
        $visionVerdict = Get-SuiteVerdict -Text $vision
    }
    $verdict = if ($detVerdict -eq 'PASS' -and ($visionVerdict -eq '' -or $visionVerdict -eq 'PASS')) { 'PASS' } else { 'FAIL' }
    $stopwatch.Stop()
    $results += [pscustomobject]@{
        Id            = $scenario.Id
        Name          = $scenario.Name
        Fixture       = $fixture
        Verdict       = $verdict
        Deterministic = $detVerdict
        Vision        = $visionVerdict
        Seconds       = [math]::Round($stopwatch.Elapsed.TotalSeconds, 1)
        Image         = $image
        Atspi         = ($parse | ConvertTo-Json -Depth 8 -Compress)
        Raw           = $run.Output
        VisionRaw     = $vision
    }
    Write-Host "-> $verdict (det=$detVerdict vision=$visionVerdict, $($results[-1].Seconds)s)"
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$reportPath = Join-Path $script:ReportRoot "run-$timestamp.md"
$lines = @("# Simple Launcher AT-SPI + vision run - $timestamp", '', "Model: $Model", '')
foreach ($result in $results) {
    $lines += "## $($result.Id) - $($result.Name)"
    $lines += ''
    $lines += "- Fixture: $($result.Fixture)"
    $lines += "- Verdict: **$($result.Verdict)** (deterministic: $($result.Deterministic), vision: $($result.Vision), $($result.Seconds)s)"
    if ($result.Image) { $lines += "- Screenshot: ``$($result.Image)``" }
    $lines += ''
    $lines += 'AT-SPI result:'
    $lines += '```json'
    $lines += $result.Atspi
    $lines += '```'
    if ($result.VisionRaw) {
        $lines += 'Vision:'
        $lines += '```'
        $lines += $result.VisionRaw
        $lines += '```'
    }
    $lines += ''
}
$lines += '## Summary'
$lines += ''
foreach ($result in $results) {
    $lines += "- $($result.Id): $($result.Verdict) - $($result.Name) [$($result.Seconds)s]"
}
Set-Content -LiteralPath $reportPath -Value ($lines -join "`n") -Encoding utf8
Write-Host "report: $reportPath"
Close-VmSession
