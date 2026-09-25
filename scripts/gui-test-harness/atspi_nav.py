"""AT-SPI navigation helpers for the SimpleLauncher vision harness.

Runs on the Linux Mint guest (needs python3-pyatspi and a running at-spi bus).
Key findings baked in (see ManualTests.md):
- Avalonia 12.1 exposes its tree unconditionally on X11; at-spi2-core is enough.
- AutomationId falls back to x:Name, so most selectors exist without app changes.
- MenuItemAutomationPeer implements only IToggleProvider: menu items have no
  AT-SPI action, so we click them via their extents.
- Cached accessible handles go stale after layout changes: always re-resolve.
"""

import json
import os
import re
import subprocess
import sys
import time

os.environ.setdefault("DISPLAY", ":0")

import pyatspi  # noqa: E402

APP_NAME = "SimpleLauncher.Avalonia"
WINDOW_TITLE = "Simple Launcher"
DEFAULT_TIMEOUT = 12.0
POLL_INTERVAL = 0.35


def sh(cmd, timeout=30):
    return subprocess.run(["bash", "-lc", cmd], capture_output=True, text=True, timeout=timeout)


def _safe(fn, default=""):
    try:
        return fn() or default
    except Exception:
        return default


class Entry:
    __slots__ = ("acc", "role", "name", "id", "extents", "depth")

    def __init__(self, acc, role, name, aid, extents, depth):
        self.acc = acc
        self.role = role
        self.name = name
        self.id = aid
        self.extents = extents
        self.depth = depth

    def to_dict(self):
        return {"role": self.role, "name": self.name, "id": self.id, "extents": self.extents}

    def __repr__(self):
        return f"<{self.role} name={self.name!r} id={self.id!r} ext={self.extents}>"


def _extents(acc):
    try:
        r = acc.queryComponent().getExtents(pyatspi.DESKTOP_COORDS)
        if r.width <= 0 or r.height <= 0 or r.x < -10000:
            return None
        return [r.x, r.y, r.width, r.height]
    except Exception:
        return None


def _valid_rect(ext):
    return bool(ext) and ext[2] > 0 and ext[3] > 0 and ext[0] >= 0 and ext[1] >= 0


class Nav:
    def __init__(self, app_name=APP_NAME, window_title=WINDOW_TITLE):
        self.app_name = app_name
        self.window_title = window_title
        self._last_activate = 0.0

    # -- low level ---------------------------------------------------------

    def _children(self, acc):
        try:
            return list(acc)
        except Exception:
            return []

    def app(self):
        for a in self._children(pyatspi.Registry.getDesktop(0)):
            if _safe(lambda: a.name) == self.app_name:
                return a
        return None

    def snapshot(self, max_nodes=3000):
        """Walk the app tree, returning deduped Entry list. Retries once on empty."""
        for attempt in range(3):
            app = self.app()
            if app is None:
                time.sleep(POLL_INTERVAL)
                continue
            entries = []
            seen = set()
            errors = 0
            stack = [(app, 0)]
            while stack and len(entries) < max_nodes:
                acc, depth = stack.pop()
                try:
                    role = _safe(acc.getRoleName)
                    name = _safe(lambda: acc.name)
                    aid = _safe(acc.get_accessible_id)
                    ext = _extents(acc)
                except Exception:
                    errors += 1
                    continue
                key = (role, name, aid, tuple(ext) if ext else None)
                if key not in seen:
                    seen.add(key)
                    entries.append(Entry(acc, role, name, aid, ext, depth))
                stack.extend((c, depth + 1) for c in reversed(self._children(acc)))
            if entries:
                return entries
            time.sleep(POLL_INTERVAL)
        return []

    # -- lookup ------------------------------------------------------------

    def find_all(self, id=None, name=None, role=None, contains=False, entries=None):
        entries = entries if entries is not None else self.snapshot()
        out = []
        for e in entries:
            if id is not None and e.id != id:
                continue
            if name is not None:
                if contains:
                    if name.lower() not in e.name.lower():
                        continue
                elif e.name != name:
                    continue
            if role is not None and role.lower() not in e.role.lower():
                continue
            out.append(e)
        return out

    def find(self, **kwargs):
        result = self.find_all(**kwargs)
        return result[0] if result else None

    def wait_for(self, timeout=DEFAULT_TIMEOUT, **kwargs):
        deadline = time.time() + timeout
        last = None
        while time.time() < deadline:
            entries = self.snapshot()
            result = self.find_all(entries=entries, **kwargs)
            if result:
                return result[0]
            last = entries
            time.sleep(POLL_INTERVAL)
        return None

    def wait_gone(self, timeout=DEFAULT_TIMEOUT, **kwargs):
        deadline = time.time() + timeout
        while time.time() < deadline:
            if not self.find(**kwargs):
                return True
            time.sleep(POLL_INTERVAL)
        return False

    # -- actions -----------------------------------------------------------

    def action_names(self, entry):
        try:
            q = entry.acc.queryAction()
            return [q.getName(i) for i in range(q.nActions)]
        except Exception:
            return []

    def do_action(self, entry, action):
        q = entry.acc.queryAction()
        for i in range(q.nActions):
            if q.getName(i) == action:
                q.doAction(i)
                return True
        raise RuntimeError(f"action {action!r} not available on {entry!r}; have {self.action_names(entry)}")

    def click(self, entry):
        """Prefer a native AT-SPI action, fall back to clicking the element's center."""
        actions = self.action_names(entry)
        if "click" in actions:
            self.do_action(entry, "click")
            return "action:click"
        if "select" in actions:
            self.do_action(entry, "select")
            return "action:select"
        ext = _extents(entry.acc) or entry.extents
        if not _valid_rect(ext):
            raise RuntimeError(f"cannot click {entry!r}: no valid extents and no action")
        if time.time() - self._last_activate > 2.0:
            self.activate_window()
        x, y, w, h = ext
        sh(f"xdotool mousemove {x + w // 2} {y + h // 2}; sleep 0.25; xdotool click 1")
        return "extents"

    def click_name(self, name, timeout=DEFAULT_TIMEOUT, errors="raise", **kwargs):
        """Find by (partial) name and click; used for menu items which lack actions."""
        entries = self.wait_for(timeout=timeout, name=name, **kwargs)
        if entries is None:
            if errors == "raise":
                raise RuntimeError(f"element not found: name={name!r}")
            return False
        self.click(entries)
        return True

    def right_click(self, entry):
        """Right-click the element center (context menus have no AT-SPI action)."""
        ext = _extents(entry.acc) or entry.extents
        if not _valid_rect(ext):
            raise RuntimeError(f"cannot right-click {entry!r}: no valid extents")
        self.activate_window()
        x, y, w, h = ext
        sh(f"xdotool mousemove {x + w // 2} {y + h // 2}; sleep 0.25; xdotool click 3")
        time.sleep(0.8)
        return "extents"

    def double_click(self, entry):
        ext = _extents(entry.acc) or entry.extents
        if not _valid_rect(ext):
            raise RuntimeError(f"cannot double-click {entry!r}: no valid extents")
        self.activate_window()
        x, y, w, h = ext
        sh(f"xdotool mousemove {x + w // 2} {y + h // 2}; sleep 0.25; xdotool click --repeat 2 --delay 120 1")
        return "extents"

    def click_card(self, label, timeout=DEFAULT_TIMEOUT):
        """Click a system-selection card: the unnamed button containing the label.

        The cards are built in code with a StackPanel content, so their AT-SPI
        name is the content type ("Avalonia.Controls.StackPanel"): locate the
        label and click the nearest push button whose extents contain it.
        """
        deadline = time.time() + timeout
        while time.time() < deadline:
            entries = self.snapshot()
            targets = [e for e in entries if e.role == "label" and e.name == label and e.extents]
            if targets:
                tx, ty = targets[0].extents[0], targets[0].extents[1]
                buttons = [e for e in entries if e.role == "push button" and e.extents
                           and e.extents[0] <= tx <= e.extents[0] + e.extents[2]
                           and e.extents[1] <= ty <= e.extents[1] + e.extents[3]]
                if buttons:
                    buttons.sort(key=lambda e: e.extents[2] * e.extents[3])
                    self.click(buttons[0])
                    time.sleep(1.0)
                    return buttons[0]
            time.sleep(POLL_INTERVAL)
        raise RuntimeError(f"system card {label!r} not found")

    def ensure_grid_view(self):
        """Switch back to grid view when the persisted mode is list."""
        if self.find(id="GameDataGrid") is not None:
            self.click(self.find(id="NavToggleViewModeButton"))
            time.sleep(1.8)
        return self.find(id="GameGridView") is not None

    def browser_open(self):
        return (self.find(id="GameGridView") is not None
                or self.find(id="GameDataGrid") is not None)

    def open_system(self, label, timeout=DEFAULT_TIMEOUT):
        """Select a system: from the card screen when present, else assume it is
        already loaded. Waits for the game grid/list afterwards."""
        if not self.browser_open():
            self.click_card(label, timeout=timeout)
        deadline = time.time() + timeout
        while time.time() < deadline:
            if self.browser_open():
                time.sleep(1.0)
                return True
            time.sleep(0.5)
        raise RuntimeError(f"game browser did not appear for system {label!r}")

    def state_names(self, entry):
        try:
            states = entry.acc.getState().getStates()
        except Exception:
            return set()
        names = set()
        for state in states:
            try:
                names.add(pyatspi.stateToString(state))
            except Exception:
                pass
        return names

    def is_enabled(self, entry):
        return "enabled" in self.state_names(entry)

    def is_checked(self, entry):
        return "checked" in self.state_names(entry)

    def checked_menu_item(self):
        for e in self.popup_items():
            if e.name and self.is_checked(e):
                return e
        return None

    def toggle(self, entry):
        if "toggle" in self.action_names(entry):
            self.do_action(entry, "toggle")
            return "action:toggle"
        return self.click(entry)

    def expand(self, entry):
        actions = self.action_names(entry)
        if "expand or collapse" in actions:
            self.do_action(entry, "expand or collapse")
            return "action:expand"
        return self.click(entry)

    def set_text(self, entry, text):
        try:
            entry.acc.queryEditableText().setTextContents(text)
            return "editable"
        except Exception:
            self.click(entry)
            sh("xdotool key ctrl+a; sleep 0.1; xdotool type --delay 25 -- " + json.dumps(text))
            return "xdotool"

    def read_text(self, entry):
        try:
            t = entry.acc.queryText()
            return t.getText(0, t.characterCount)
        except Exception:
            return None

    def read_value(self, entry):
        try:
            return entry.acc.queryValue().currentValue
        except Exception:
            return None

    def set_value(self, entry, value):
        entry.acc.queryValue().currentValue = value
        return "value"

    def frame_present(self, title):
        return self.find(name=title, role="frame") is not None

    def frame_extents(self):
        e = self.find(name=self.window_title, role="frame")
        return e.extents if e else None

    # -- menus -------------------------------------------------------------

    def menubar_extents(self):
        for e in self.find_all(role="menu"):
            if e.extents:
                return e.extents
        ext = self.frame_extents()
        return [ext[0], ext[1], ext[2], 32] if ext else None

    def popup_items(self, roles=("menu item",)):
        """Open popup entries below the menu bar (deduped)."""
        mb = self.menubar_extents()
        if not mb:
            return []
        y0 = mb[1] + mb[3]
        out, seen = [], set()
        for e in self.find_all(role=None):
            if e.role not in roles:
                continue
            if not (e.extents and e.extents[1] >= y0):
                continue
            key = (e.name, tuple(e.extents))
            if key not in seen:
                seen.add(key)
                out.append(e)
        return out

    def open_menu(self, name):
        """Click a top-level menu bar item (above the popup area)."""
        mb = self.menubar_extents()
        y0 = mb[1] + mb[3] if mb else 0
        tops = [e for e in self.find_all(name=name, role="menu item")
                if e.extents and e.extents[1] < y0]
        if not tops:
            raise RuntimeError(f"top-level menu {name!r} not found")
        self.click(tops[0])
        time.sleep(0.7)
        return tops[0]

    def select_combo(self, combo, name, timeout=DEFAULT_TIMEOUT):
        """Expand a ComboBox and click the popup entry with the given name."""
        self.expand(combo)
        deadline = time.time() + timeout
        while time.time() < deadline:
            candidates = [e for e in self.snapshot()
                          if e.name == name and e.extents and e.role not in
                          ("label", "panel", "push button", "image")]
            candidates = [e for e in candidates if e.extents[1] > combo.extents[1] + combo.extents[3] - 5]
            if candidates:
                candidates.sort(key=lambda e: (e.extents[0], e.extents[1]))
                self.click(candidates[0])
                time.sleep(0.8)
                return candidates[0]
            time.sleep(POLL_INTERVAL)
        self.press("Escape")
        raise RuntimeError(f"combo entry {name!r} not found")

    def context_menu_items(self):
        """Items of an open context menu (popup entries in the app tree)."""
        time.sleep(0.4)
        return [e for e in self.popup_items() if e.name]

    def click_context_item(self, name, timeout=DEFAULT_TIMEOUT):
        """Click an item of an open context menu (PopupRoot)."""
        item = self.wait_for(name=name, role="menu item", timeout=timeout)
        if item is None:
            raise RuntimeError(f"context item {name!r} not found")
        self.click(item)
        time.sleep(0.8)
        return item

    def dismiss_popup(self):
        self.press("Escape")
        time.sleep(0.4)

    def click_at(self, x, y, button=1, repeat=1):
        """Click absolute screen coordinates (cards lose their AT-SPI names after
        the grid re-renders, so fixed card coordinates are used)."""
        self.activate_window()
        sh(f"xdotool mousemove {x} {y}; sleep 0.25; "
           f"xdotool click --repeat {repeat} --delay 120 {button}")
        time.sleep(0.7)

    def filter_letter(self, letter, wait=2.2):
        """Click a letter button in the filter bar (y above the toolbar)."""
        hits = [e for e in self.find_all(name=letter, role="push button")
                if e.extents and e.extents[1] < 170]
        if not hits:
            raise RuntimeError(f"letter button {letter!r} not found")
        self.click(hits[0])
        time.sleep(wait)
        return hits[0]

    def pagination_count(self):
        entry = self.find(id="PaginationLabel")
        if not entry or not entry.name:
            return None
        match = re.search(r"out of (\d+) total", entry.name)
        return int(match.group(1)) if match else None

    def status_left(self):
        entry = self.find(id="StatusLeft")
        return entry.name if entry else None

    def wait_extra_frames_gone(self, timeout=15):
        deadline = time.time() + timeout
        while time.time() < deadline:
            frames = [e.name for e in self.find_all(role="frame")
                      if e.name and e.name != self.window_title]
            if not frames:
                return True
            time.sleep(0.5)
        return False

    def click_menu_item(self, name, timeout=DEFAULT_TIMEOUT):
        """Click a popup entry by name; when a submenu duplicates the name,
        click the rightmost (flyout) entry."""
        deadline = time.time() + timeout
        while time.time() < deadline:
            hits = [e for e in self.popup_items() if e.name == name]
            if hits:
                hits.sort(key=lambda e: e.extents[0])
                self.click(hits[-1])
                time.sleep(0.5)
                return hits[-1]
            time.sleep(POLL_INTERVAL)
        raise RuntimeError(f"menu item {name!r} not found (open menus: "
                           f"{[e.name for e in self.popup_items()]})")

    def close_dialogs(self):
        """Close every frame except the main window (Escape, then WM close)."""
        for _ in range(3):
            frames = [e for e in self.find_all(role="frame")
                      if e.name != self.window_title and e.name != "PopupRoot"]
            if not frames:
                return True
            for f in frames:
                self.activate_window()
                self.press("Escape")
                time.sleep(0.3)
                title = f.name.replace("'", "")
                sh(f"xdotool search --name '^{title}$' windowclose 2>/dev/null")
            time.sleep(0.5)
        return not any(e.name != self.window_title for e in self.find_all(role="frame"))

    # -- environment -------------------------------------------------------

    def window_id(self):
        r = sh(f"xdotool search --name '^{self.window_title}$' | head -1")
        return r.stdout.strip()

    def activate_window(self):
        wid = self.window_id()
        if not wid:
            return None
        sh(f"xdotool windowactivate {wid}")
        time.sleep(0.4)
        self._last_activate = time.time()
        return wid

    def maximize_window(self):
        sh(f"wmctrl -r '{self.window_title}' -b add,maximized_vert,maximized_horz")
        time.sleep(1.2)

    def press(self, keys):
        self.activate_window()
        sh(f"xdotool key {keys}")
        time.sleep(0.35)

    def close_welcome(self):
        """Dismiss the modal first-run Welcome dialog (blocks all clicks)."""
        if not self.frame_present("Welcome"):
            return "absent"
        for button in ("No", "Close", "OK"):
            hits = self.find_all(name=button, role="push button")
            if hits:
                self.click(hits[0])
                if self.wait_gone(timeout=5, name="Welcome", role="frame"):
                    return f"clicked:{button}"
        self.activate_window()
        self.press("Escape")
        if self.wait_gone(timeout=5, name="Welcome", role="frame"):
            return "escape"
        return "stuck"

    def ensure_ready(self):
        """Deterministic app state: window activated + maximized, Welcome dismissed."""
        wid = self.activate_window()
        if not wid:
            return {"error": "window not found"}
        self.maximize_window()
        self.press("Escape")
        welcome = self.close_welcome()
        self.snapshot()  # warm the tree once the layout has settled
        return {"window": wid, "welcome": welcome}

    # -- reporting ---------------------------------------------------------

    def tree_text(self, max_depth=99):
        entries = self.snapshot()
        lines = []
        for e in entries:
            if e.depth > max_depth:
                continue
            indent = "  " * e.depth
            lines.append(f"{indent}[{e.role}] {e.name!r} id={e.id!r} ext={e.extents}")
        return "\n".join(lines)


def main(argv):
    nav = Nav()
    cmd = argv[1] if len(argv) > 1 else "tree"
    if cmd == "tree":
        print(nav.tree_text())
    elif cmd == "ready":
        print(json.dumps(nav.ensure_ready()))
    elif cmd == "dump":
        entries = nav.snapshot()
        print(json.dumps([e.to_dict() for e in entries], indent=1))
    elif cmd == "selftest":
        print(json.dumps(selftest(nav), indent=1))
    else:
        print(f"unknown command: {cmd}", file=sys.stderr)
        return 2
    return 0


def selftest(nav):
    """Phase-0 checks against a running app; returns a report dict."""
    report = {}
    report["ready"] = nav.ensure_ready()
    t0 = time.time()
    entries = nav.snapshot()
    report["snapshot_ms"] = round((time.time() - t0) * 1000)
    report["nodes"] = len(entries)

    t0 = time.time()
    box = nav.wait_for(id="SearchBox", timeout=8)
    report["find_by_id_ms"] = round((time.time() - t0) * 1000)
    if box:
        nav.set_text(box, "mario")
        time.sleep(0.3)
        fresh = nav.find(id="SearchBox")
        report["searchbox_text"] = nav.read_text(fresh) if fresh else None
        nav.set_text(fresh, "") if fresh else None

    slider = nav.wait_for(id="CardSizeSlider", timeout=8)
    if slider:
        report["slider_value"] = nav.read_value(slider)

    combo = nav.wait_for(id="SystemComboBox", timeout=8)
    report["combo"] = combo.to_dict() if combo else None
    if combo:
        nav.expand(combo)
        time.sleep(0.6)
        report["combo_after_expand_still_open"] = bool(nav.find(id="SystemComboBox"))
        nav.press("Escape")

    report["menu"] = {}
    nav.press("Escape")
    opt = nav.find(name="Options", role="menu item", entries=nav.snapshot())
    if opt:
        t0 = time.time()
        nav.click(opt)
        report["menu"]["open_ms"] = round((time.time() - t0) * 1000)
        time.sleep(0.7)
        items = nav.find_all(role="menu item", entries=nav.snapshot())
        names = sorted({e.name for e in items if e.name and e.name not in ("Options", "Edit System", "Select Window", "Donate", "About")})
        report["menu"]["items"] = names
        theme = nav.find(name="Theme", role="menu item")
        if theme:
            nav.click(theme)
            time.sleep(0.7)
            subs = nav.find_all(role="menu item", entries=nav.snapshot())
            report["menu"]["submenu"] = sorted({e.name for e in subs if e.name in ("Base Theme", "Accent Colors")})
        nav.press("Escape")
    return report


if __name__ == "__main__":
    sys.exit(main(sys.argv))
