# BugNeedFix — review of all commits after 2df0b6ea (5.8.0 gate)

Scope: `git log 2df0b6ea..HEAD` (70 commits, up to `f6246626` "prepare 5.8.0 release"),
plus the deep review of the legacy migration
`system.xml` / `settings.xml` / `favorites.dat` / `playhistory.dat` → unified `settings.dat`.
Every finding below was verified against the current tree (file:line refs are HEAD).
The WPF and Avalonia legacy migrators are line-for-line mirrors, so one path is cited
per migration finding (WPF path; Avalonia identical unless noted).

Migration entry points: `SimpleLauncher/Services/SettingsDatabase/WpfLegacyMigrator.cs`,
`SimpleLauncher.Avalonia/Services/SettingsDatabase/AvaloniaLegacyMigrator.cs`,
`SimpleLauncher.Core/Services/UnifiedSettings/UnifiedSettingsDatabase.cs`.

---

## A. Migration to settings.dat — must-fix before 5.8.0

### A1. CRITICAL — Stale shadow copy resurrects and overwrites fresh data
- Files: `SimpleLauncher/Services/SettingsDatabase/WpfLegacyMigrator.cs:323-365`
  (`ResolveLegacyFiles` / `FindNewestLegacyFile`), `:492-509` (`ShelveLegacyFile`).
- Per file type only the *newest* of portable-vs-AppData is merged, and only that one
  path is shelved to `.bak`. The older copy stays on disk.
- Failure: `favorites.dat` exists next to the exe (newer) and in AppData (older).
  Launch 1 migrates the newer copy and shelves only it. Launch 2 finds the older copy
  still present (`anyLegacy == true`), takes the merge path (`:188-290`), and the
  legacy-first-wins dedupe (`:224-228`) overwrites the just-migrated rows with stale
  values, then shelves the stale file — destroying the evidence. One launch silently
  rolls back the previous launch's import.
- Fix: resolve *both* locations per file type (merge contents from both, newer wins),
  then shelve *both*. At minimum, shelve the unpicked copy too.

### A2. HIGH — Merge has no pre-merge database backup; conflicting DB rows are destroyed
- Files: `WpfLegacyMigrator.cs:204-261` (six tables written, no backup). The only
  callers of `BackupDatabase` are the two EditSystem windows
  (`SimpleLauncher/EditSystemWindow.xaml.cs:502`,
  `SimpleLauncher.Avalonia/EditSystemWindow.axaml.cs:495`);
  `UnifiedSettingsDatabase.cs:364-380` has the `VACUUM INTO` helper but migration never uses it.
- Failure: any stale legacy files (restored folder, A1 shadow copy, files re-created
  by 5.7.x — see A10) clobber newer DB values on conflicting keys. No `.bak` of the
  DB exists to restore; DB and shelved `.bak` then both hold the stale data.
- Fix: call `UnifiedSettingsDatabase.BackupDatabase(timestamped path)` before the
  first merge write.

### A3. HIGH — "Legacy wins" means any re-merge discards newer DB edits
- Files: `WpfLegacyMigrator.cs:217-260` (legacy-first concat + overwrite loops
  `:230-232`, `:247-253`). No mtime/newer-wins check, no prompt.
- Failure: user migrates, edits theme/systems/favorites in 5.8.0, then a legacy file
  reappears (backup restore, cloud-sync conflict, 5.7.x run). Next launch silently
  reverts the conflicting rows. The `Merged` count log (`:278-281`) reports only
  net-added rows, so overwrites are invisible.
- Fix: prefer DB-wins on conflict for re-merges (append-only for new keys), or only
  overwrite when the legacy file is newer than the DB, with an Information log per
  overwritten key.

### A4. HIGH (forward-compat) — Newer-schema DB is treated as corrupt and quarantined
- Files: `UnifiedSettingsDatabase.cs:80-82` (`schemaVersion <= CurrentSchemaVersion`,
  `CurrentSchemaVersion = 1` at `:39`), `EnsureCreated :102-105` +
  `QuarantineCorruptDatabase :304-338` (moves to `.corrupt.<stamp>.bak`, recreates empty).
- Failure: a DB with `schema_version > 1` (written by a future 5.9+) fails validation
  on 5.8.0 and `EnsureCreated` — called by migration merge (`WpfLegacyMigrator.cs:215`),
  every `Load*` fallback, and `SaveToDatabase` — quarantines it and starts empty.
  Trialing a newer build then running 5.8.0 wipes the whole library/settings/favorites
  into a `.corrupt.*.bak` with only a Warning log.
- Fix: refuse to open newer schemas read-write (fail migration with `Failed`, leave
  the file untouched, log a Warning telling the user to upgrade); never quarantine a
  file whose only defect is a newer version.

### A5. HIGH — Shelving by rename leaves 5.7.x with empty data and poisons re-upgrade
- Files: shelve = `File.Move` (`WpfLegacyMigrator.cs:492-509`); fallback readers
  recreate legacy files when absent (`FavoritesManager.cs:97-138`,
  `PlayHistoryManager.cs:114-136`, `SystemManagerService.cs:203-236`).
- Failure: (a) Downgrade → 5.7.x sees no data, creates fresh empty files. User
  perceives total data loss (recoverable only by manual `.bak` rename —
  undiscoverable). (b) Worse: re-upgrade → merge path finds 5.7.x's default
  `settings.xml` with `Loaded=true` and overwrites the good DB settings with 5.7.x
  defaults (A3), while empty favorites/history merge harmlessly. The round trip
  destroys settings that survived the first migration.
- Fix: ship a downgrade note (exact `.bak` → original rename steps) in release notes
  + log the mapping at Warning during shelve; on merge, ignore `settings.xml` files
  that contain only defaults (compare against a fresh `SettingsManagerService`
  export before overwriting DB values).

### A6. MEDIUM — Database is always AppData; portable mode is silently broken
- Files: `UnifiedSettingsDatabase.cs:50-54` (`GetDatabasePath` = AppData, no portable
  branch) vs `DataFileLocation.cs:62-113` (portable-vs-AppData logic still used for
  every legacy read).
- Failure: previously a writable exe folder meant all data lived next to the exe. Now
  legacy files are drained from the portable folder into AppData on first launch and
  all later writes go to AppData. Portable user copies the app folder to a new machine
  → settings/favorites/history/systems stay behind with no warning.
- Fix (release-gating decision): either honor portable mode for `settings.dat` (DB
  next to exe when exe dir is writable and no AppData DB exists), or document the
  move loudly in What's New + log a Warning at migration stating the new canonical
  location. Do not ship silently. (Note: `docs/03-quickstart.md:47` already claims
  portable mode works — code and docs currently disagree.)

### A7. MEDIUM — No `busy_timeout`; transient lock fails hard and can resurrect legacy files
- Files: `UnifiedSettingsDatabase.cs:176-227` (`CreateOpenConnection` sets
  WAL/NORMAL/foreign_keys, never `busy_timeout`; `Pooling=false` only).
  `WriteLock` (`:41`) is per-process only. Single-instance mutex
  (`SimpleLauncher.Core/SingleInstance.cs:15`) has a `--restarting` bypass
  (`SimpleLauncher/App.xaml.cs:557`, `SimpleLauncher.Avalonia/App.axaml.cs:195-196`).
- Failure: default `busy_timeout` is 0 → any concurrent writer (restart overlap where
  the updater relaunches with `--restarting` while the old process still flushes,
  AV/indexer lock, `VACUUM INTO` backup from EditSystem) throws `SQLITE_BUSY`
  immediately. Worse: WPF `FavoritesManager.SaveFavoritesAsync`
  (`FavoritesManager.cs:197-210`) catches the DB error and **falls back to writing a
  fresh legacy `favorites.dat`** — resurrecting a file migration just shelved. Next
  launch re-merges the stale/empty file over the DB (A3).
  `SettingsManagerService.SaveAsync` swallows failures at Information
  (`SettingsManagerService.cs:793-800`), so the data loss is silent.
- Fix: `PRAGMA busy_timeout = 5000;` in `CreateOpenConnection`; add a
  `SQLITE_BUSY`-specific retry; never fall back to legacy-file writes once a valid
  DB exists (log + keep in memory instead).

### A8. MEDIUM — Fresh-path crash cleanup is a dead branch (torn DB kept, not deleted)
- Files: `WpfLegacyMigrator.cs:144-165` (catch deletes only
  `if (!dbExistedBefore && … && !IsValidDatabase)`), `:102` (`EnsureCreated` runs
  before any `Save*` and sets `schema_version`, so the DB is *valid* even if a later
  `Save*` throws → the `!IsValidDatabase` guard is false and nothing is deleted).
- Failure: crash/kill between table writes (power loss, `SQLITE_FULL`, AV lock)
  leaves a valid-but-torn DB (e.g. favorites written, systems missing). Next launch
  takes the *merge* path, not a fresh retry — recovery happens to converge via merge
  dedupe, but the code comment promises deletion-based retry, and concurrent readers
  see torn state in between.
- Fix: migrate all six tables in one SQLite transaction, or stage to a temp DB file
  and atomically move it over the target. At minimum fix the comment and add a
  kill-between-tables test.

### A9. MEDIUM — Merge of six tables is not atomic (torn DB visible across crash)
- Files: `WpfLegacyMigrator.cs:254-260` (six sequential `Save*` calls, each its own
  transaction); each `Save*` is per-table atomic
  (`UnifiedSettingsDatabase.cs:398-424`, `:481-508`, `:526-558`, `:585-623`,
  `:656-682`, `:739-770`). No cross-table transaction.
- Failure: crash after `SaveFavorites` but before `SaveAllSystems` → next launch
  merges again (converges) but any launch reading between crash and retry sees
  favorites-new/systems-old. No corruption, but no consistency guarantee.
- Fix: wrap the whole merge in a single transaction (multi-table write API on one
  connection) or stage-then-swap (see A8).

### A10. MEDIUM — WPF vs Avalonia `system.xml` parsers accept different inputs, share one DB
- Files: WPF strict `SimpleLauncher/Services/SystemManager/SystemManagerService.cs:445-602`
  (throws on missing `SystemName` `:449-451`, zero folders `:468-472`, zero
  `FormatToSearch` `:488-492`, empty `Emulators` `:545-549`) vs Avalonia lenient
  `ParseSystemElement` (`SimpleLauncher.Avalonia/Services/SystemManager/SystemManagerService.cs:262-280`,
  `ParseFoldersCompat :681-698`, `ParseListCompat :705-725`, `ParseEmulators :753-778`
  — return `[]`/`""`, never throw; accepts `<SystemFolder>a;b</…>`, comma-separated
  formats, missing `<Emulators>`).
- Failure: same `system.xml` yields different system counts per app. The Avalonia
  test fixture itself proves it: its `WriteLegacySystemXml` second block has no
  `<Emulators>` and comma-separated formats
  (`SimpleLauncher.Avalonia.Tests/LegacyMigrationTests.cs:490-497`) yet expects 2
  systems (`:51`); the WPF parser would reject that block. A re-merge/fresh migration
  with WPF of the same file imports fewer systems and overwrites/shelves: systems
  silently lost on the WPF path.
- Fix: unify on one parser (move `LoadSystemsFromPath` into Core) or make the WPF
  parser equally compat-tolerant. Add a cross-variant test migrating the same loose
  XML with both migrators and asserting equal counts.

### A11. MEDIUM — Custom `SystemXmlPath` ignored for the AppData candidate
- Files: `WpfLegacyMigrator.cs:324-344` (portable candidate honors
  `configuration["SystemXmlPath"]`; AppData candidate hardcodes
  `Path.Combine(appDataFolder, "system.xml")`); contrast
  `DataFileLocation.cs:32-45` (derives the AppData fallback from the custom name).
- Failure: with `SystemXmlPath = "my_systems.xml"`, the AppData-side file
  `my_systems.xml` is never considered. Migration "succeeds" with zero systems while
  the real systems file sits unmigrated and unreferenced.
- Fix: derive the AppData candidate from `Path.GetFileName(resolved custom path)`
  exactly like `DataFileLocation` does.

### A12. MEDIUM — Invalid-system logging violates the bug-report policy (WPF) / silent drop (Avalonia)
- Files: WPF `SystemManagerService.cs:696-700` (`logErrors?.Error` per skipped system
  during migration reads); Avalonia `SystemManagerService.cs:127-139` (collects
  `invalidErrors`, no log call; `:190` shows a dialog only when `_messageBox`
  non-null — null during migration). Standing policy (AGENTS.md): expected user-data
  conditions must be Information, never Warning/Error → `BugReportApiSink`.
- Failure: every slightly-invalid `<SystemConfig>` (missing emulator, empty folder —
  common in hand-edited files) emits an `Error` during WPF migration → fleet-wide
  bug-report spam on 5.8.0 rollout. Avalonia errs the other way: silent drop, "where
  did my system go" with zero log trace.
- Fix: log per-system skips at Information in both migrators' read path (or Debug +
  a counted Information summary "skipped N invalid systems").

### A13. MEDIUM — PlayHistory single-column key silently drops same-file/different-system rows
- Files: `UnifiedSettingsDatabase.cs:135-144` (`PlayHistory.FileName` sole PK),
  `:599-623` (dedupe on `FileName` only), `WpfLegacyMigrator.cs:226-228`. The runtime
  matcher keys history by (path, system) (`PlayHistoryManager.cs:461-463`), but the
  DB and dedupe collapse on filename alone, first-wins.
- Failure: same ROM path registered under two systems (or legacy dupes with
  different `SystemName`) → one row survives; counters/timestamps of the other
  vanish. `historyAdded` can report 0 while data was deleted.
- Fix: key history by `(FileName, SystemName)` like Favorites, or aggregate
  (`TimesPlayed` summed, max timestamp) instead of dropping.

### A14. MEDIUM — `.bak` shelving overwrites the previous backup
- Files: `WpfLegacyMigrator.cs:498-499` (`File.Move(path, backup, true)`), same in
  Avalonia `:498-499`.
- Failure: every shelve clobbers any existing `<name>.bak` with no versioning. A1's
  double-merge, or a user-kept manual `.bak`, is destroyed by the second shelve.
  Combined with A2, the last good copy can be silently replaced by stale data.
- Fix: never overwrite — `<name>.<yyyyMMddHHmmss>.bak`, or refuse + warn when a
  `.bak` already exists.

### A15. MEDIUM — First-wins vs last-wins inconsistency for duplicate system names
- Files: fresh path `WpfLegacyMigrator.cs:110-115` (`if (!ContainsKey)` → first legacy
  entry wins) vs merge path `:230-231` (`mergedSystems[name] = …` in legacy order →
  **last** legacy entry wins).
- Failure: `system.xml` with `NES` then `nes` (different folders) imports a different
  system depending on which path executed. Cross-machine behavior diverges with no
  log distinction.
- Fix: make both paths first-wins (or last-wins) and log which duplicate was dropped.

### A16. MEDIUM — Favorites DB collation disagrees with migrator dedupe (negative counts)
- Files: `UnifiedSettingsDatabase.cs:128-134` (`FileName … COLLATE NOCASE`,
  `SystemName` with **no** collation → BINARY) vs `WpfLegacyMigrator.cs:224-225` +
  `UnifiedSettingsDatabase.cs:542-546` (dedupe key `FileName+"\0"+SystemName` with
  `OrdinalIgnoreCase`).
- Failure: a DB holding `("g.zip","NES")` + `("g.zip","nes")` (two rows) merged again
  collapses to one row (data loss), and `mergedFavorites.Count -
  existingFavorites.Count` (`:266`) goes **negative**, logging e.g. "added -1 favorites".
- Fix: add `COLLATE NOCASE` to `Favorites.SystemName` (with an upgrade path like
  `UpgradeFavoritesToCompositeKey`, `:242-277`) so DB and code agree.

### A17. LOW — Corrupt legacy files are still shelved, hiding data from 5.7.x
- Files: `WpfLegacyMigrator.cs:369-414` (corrupt → `[]`, Information log), `:271-276`
  (shelve runs regardless of read success).
- Failure: a marginally-corrupt file (readable by 5.7.x's more lenient path or
  repairable) migrates zero rows but the bytes are renamed to `.bak`, which no
  5.7.x/5.8.0 reader looks at. 5.7.x downgrade also sees zero.
- Fix: only shelve files that parsed successfully; leave unreadable files in place
  with an Information log, or shelve to a distinct `.corrupt.bak` name.

### A18. LOW — Regex fallback recovery imports partial data with no marker
- Files: WPF `SystemManagerService.cs:279-333` + `LoadSystemsFromPath :704-729`;
  Avalonia `:142-176`. Structurally-corrupt XML falls back to regex-extracted
  `<SystemConfig>` blocks, migrated as authoritative, then the source is shelved.
- Failure: half-written `system.xml` (crash during save) migrates a subset; shelving
  destroys the original, so the dropped tail can never be re-examined.
- Fix: when regex recovery engages during migration, shelve to
  `system.xml.partial.<stamp>.bak` and log the recovered/dropped count.

### A19. LOW — `IsValidDatabase` has a filesystem side effect; read-only cleared on read
- Files: `UnifiedSettingsDatabase.cs:65-89` (opens with `Mode = ReadWriteCreate`,
  `:184-192` → validating a *missing* path creates a 0-byte file, then reports
  invalid); `CreateOpenConnection :182` → `ClearReadOnlyAttributes :285-302` runs on
  every open including pure reads.
- Failure: a user who deliberately set `settings.dat` read-only to freeze config
  gets it silently cleared and overwritten. (The #67097 recovery intent is correct,
  but it should apply to write paths, not reads.)
- Fix: validate with `Mode = ReadOnly`; clear read-only attributes only before write
  transactions.

### A20. LOW — `Version` field of `favorites.dat`/`playhistory.dat` is never validated
- Files: `SimpleLauncher/Services/Favorites/FavoritesManager.cs:34-35`,
  `SimpleLauncher/Services/PlayHistory/PlayHistoryManager.cs:40`,
  `WpfLegacyMigrator.cs:369-414` (no version branch). Both managers carry
  `Version = 1` (`Key(1)`), but the migrator never inspects it.
- Failure: a future/foreign file with `Version = 2` plus new fields migrates with new
  fields silently dropped (or throws → imports none but still shelved per A17).
  Empty 0-byte files also throw → import none → still shelved.
- Fix: log the observed version at Information; on unknown version, shelve to a
  distinct name and keep DB values rather than importing partial data.

### A21. LOW — `settings.xml` drift gap: duplicate casing not normalized
- Files: `SettingsManagerService.cs:530-756` (drift tolerance is otherwise good —
  XXE-hardened reader `:320-327`, DTD prohibited). Residual:
  `ImportAppSettings` round-trips through `XElement` names case-sensitively
  (`:1143-1156`), while merge dictionaries are `OrdinalIgnoreCase`
  (`WpfLegacyMigrator.cs:242-246`); `AppSettings.Key` PK is case-sensitive
  (`UnifiedSettingsDatabase.cs:117-121`) unlike `Systems` (`NOCASE`, `:146-150`).
- Failure: hand-edited `<language>` vs `<Language>` duplicates collapse
  unpredictably on merge but coexist in the DB. Cosmetic only.
- Fix: normalize app-setting keys to canonical casing on merge, or add
  `COLLATE NOCASE` to `AppSettings.Key`.

### A22. LOW — `_migrationAttempted` is in-memory only; in-process retry after `Failed` suppressed
- Files: `WpfLegacyMigrator.cs:36-37`, `:63-73` (same in Avalonia). Second call in
  the same process returns `AlreadyCurrent`/`Failed` from the guard without
  re-scanning. No persistent migration flag — idempotency relies entirely on legacy
  files disappearing via shelve.
- Failure: first attempt `Failed` (transient lock) → any in-process caller retrying
  gets `Failed` with zeros without doing work. Across launches it retries correctly,
  so impact is limited to intra-process callers/tests.
- Fix: reset the guard on `Failed`, or return the cached result instead of a
  synthesized zero-count one.

### A23. Test coverage gaps (migration)
Verified against `UnifiedSettingsDatabaseTests.cs`, `WpfLegacyMigrationTests.cs`
(`SimpleLauncher.Tests`), `LegacyMigrationTests.cs` (`SimpleLauncher.Avalonia.Tests`):
1. Dual-location logic untested (covers A1): all migration tests pass the *same*
   folder as both `dbPathOverride` and `legacyFolderOverride`, collapsing
   portable+AppData into one dir (`WpfLegacyMigrationTests.cs:26-33`).
   `FindNewestLegacyFile`'s two-path branch has zero coverage.
2. No `Failed`-then-retry test (A8/A9). No shelve-failure test (locked file → `return
   0` path untested; nothing asserts the next launch retries).
3. No `.bak`-overwrite test (A14): pre-existing `.bak` + migration → old backup lost.
4. No custom `SystemXmlPath` test (A11): all tests use `"system.xml"`.
5. No concurrency test (A7): two writers, `SQLITE_BUSY` assertion, restart-overlap.
6. No downgrade/re-upgrade test (A5): migrate → write 5.7.x-style defaults →
   re-merge → assert DB settings survive.
7. Parser-divergence test missing (A10): same loose `system.xml` through both
   migrators asserting identical sets — the Avalonia suite's loose second block
   (`LegacyMigrationTests.cs:490-497`, no `<Emulators>`) would fail against the WPF
   migrator today.
8. `UnifiedSettingsDatabaseTests` missing: future-schema-version handling (A4);
   quarantine when the file is locked; `UpgradeFavoritesToCompositeKey` conflicting
   rows (which duplicate `INSERT OR IGNORE` keeps is order-undefined, `:264-276`);
   content-equality verification (counts-only `VerifyMigration` means a column-swap
   bug passes green); `busy_timeout` behavior.
9. Negative-count logging untested (A16): seed `("g.zip","NES")` +
   `("g.zip","nes")`, re-merge, assert single row + non-negative report.

---

## B. Other active bugs from the commit review (verified at HEAD)

### B1. HIGH — Avalonia global-search CTS race logs a spurious bug report
- Introduced: hardening batch (`1b9dbfbf` area); file:
  `SimpleLauncher.Avalonia/ViewModels/GlobalSearchSectionViewModel.cs:120-134` + `:208`.
- `SearchAsync()` captures `cts` locally, replaces the field, and disposes `previous`
  (`previous.Dispose()` at `:134`). A rapid second search disposes the first search's
  `cts`; the first search's `finally` then reads `cts.IsCancellationRequested`
  (`:208`) on a disposed object → `ObjectDisposedException` → outer
  `catch (Exception …) { _logErrors.Error(ex, "Error in the global search command"); }`
  (`:211-214`) files a bug via `BugReportApiSink` for expected concurrency.
  `CancelSearch()` already guards `ObjectDisposedException`; the `finally` does not.
- Fix: guard the finally read —
  `bool cancelled; try { cancelled = cts.IsCancellationRequested; } catch
  (ObjectDisposedException) { cancelled = true; } if (!cancelled) IsLoading = false;`
  — or never `Dispose()` a source an older search may still read.

### B2. MEDIUM — Updater arg omitted on the reinstall-after-download path
- Introduced: `d3d3557a`; file:
  `SimpleLauncher/Services/QuitOrReinstall/ReinstallSimpleLauncher.cs:107`.
- `d3d3557a` unified the updater protocol to `"<pid> <app-exe>"`. It updated
  `QuitSimpleLauncher.cs` and the first `ReinstallSimpleLauncher.cs` branch (`:51-52`
  now `"… SimpleLauncher.exe"`), but missed the second branch
  (download-then-launch): `Arguments = Environment.ProcessId.ToString(…)` (`:107`)
  with no app name. Works today only because
  `ProcessService.TryResolveAppProcessName(pid-only)` falls back to
  `WpfAppProcessName`. Any fallback change breaks reinstall-after-download.
- Fix: `Arguments = $"{Environment.ProcessId…} SimpleLauncher.exe"` at `:107`,
  matching `:51-52` and `QuitSimpleLauncher.cs:138-139`.

### B3. MEDIUM — Silent-update network failure logged as Error (bug-report policy)
- Still present after `720e0f84`-era fixes; file:
  `SimpleLauncher.Avalonia/App.axaml.cs:1041-1044`
  (`catch (Exception ex) { Log.Error(ex, "Silent update check failed on startup"); }`).
- `SilentCheckForUpdatesAsync` throws `HttpRequestException`/`IOException`/
  `TimeoutException` for offline / rate-limit / timeout — standing policy requires
  Information, not Warning/Error. (Same pattern was fixed for the updater service in
  `b745d5aa:UpdateService.cs:99`, but `App.axaml.cs` still uses `Error`.)
- Fix: split — Information for `HttpRequestException`/`IOException`/
  `TimeoutException`, Error otherwise.

### B4. MEDIUM-LOW — `SaveAsync` swallows all errors as Information (masks real bugs)
- Introduced: `6d120e5c`; file:
  `SimpleLauncher.Core/Services/SettingsManager/SettingsManagerService.cs:793-799`
  (`catch (Exception ex) { _logger.Information(ex, "Failed to save settings"); }`).
- Fire-and-forget safety is correct, but catch-all Information hides programming bugs
  (serialization, `BuildXElement` failures) that should be Warning/Error → bug
  report. Only `IOException`/`UnauthorizedAccessException`/locked-DB
  `SqliteException` are expected environment conditions.
- Fix: narrow — Information for
  `IOException|UnauthorizedAccessException|SqliteException`, Error otherwise.

### B5. LOW — Card-size slider does not snap back to the 50px grid
- Introduced: `0eb3d9f1`; file: `SimpleLauncher/MainWindow.MenuItems.cs:339-361`
  (`CardSizeSliderDebounceTimerTick`).
- `HandleButtonSizeAsync` (`MenuActionHandlerService.cs:759-764`) clamps/snaps to
  `50..800 step 50`. All callers re-sync except the slider tick:
  `ButtonSizeClickAsync:297`, `NavZoomIn:711`, `NavZoomOut:727`, Ctrl+wheel in
  `MainWindow.xaml.cs` all call `SyncCardSizeSlider()`. The tick at `:350` awaits
  `HandleButtonSizeAsync(newSize)` with no sync, so e.g. drag to `253` persists
  `250` in settings while the slider shows `253`.
- Fix: call `SyncCardSizeSlider();` after `:350` (safe: `ValueChanged`
  early-returns when `size == _settings.ThumbnailSize` and cancels the pending timer).

### B6. LOW — RA session token not cleared when API key/password changes
- Still present after `092b919b`; files:
  `SimpleLauncher.Avalonia/ViewModels/RetroAchievementsSettingsViewModel.cs:173-176`,
  `SimpleLauncher/ViewModels/…:169-172`
  (`if (!Equals(_settings.RaUsername, Username.Trim(), OrdinalIgnoreCase))
  _settings.RaToken = "";`).
- Only a username change clears `RaToken`. Changing `ApiKey`/`Password` with the
  same username re-injects the old account's session token into emulator configs.
- Fix: also compare `ApiKey` (and password) before save.

### B7. LOW — Corrupted-XML dialogs can overlap (fire-and-forget modal)
- Still present after `386c16e7`; file:
  `SimpleLauncher.Avalonia/Services/SystemManager/SystemManagerService.cs:214-221`
  (`_ = _messageBox?.SystemXmlIsCorruptedMessageBoxAsync(...);` never awaited, then
  a restore prompt is awaited). The restore prompt can appear under/over the first
  modal; exceptions are unobserved.
- Fix: `await _messageBox.SystemXmlIsCorruptedMessageBoxAsync(...)`.

### B8. LOW — Underscore not stripped in hash-logic system-name match
- Introduced: `4fe894a9`; file:
  `SimpleLauncher.Core/Services/RetroAchievements/RetroAchievementsSystemMatcher.cs:406-417`
  (`NormalizeForHashLogicMatch` strips `- / & space . ' ™ ®` but not `_`).
  `sega_pico` vs `sega pico` misses the unsupported list (relies on ID fallback).
- Fix: add `.Replace("_", string.Empty).Replace("+", string.Empty)`.

### B9. LOW — Read-only attribute cleared on every DB open, including reads (see A19)
- Introduced: `6d120e5c`; file:
  `SimpleLauncher.Core/Services/UnifiedSettings/UnifiedSettingsDatabase.cs:176-182`,
  `:284-302` (`CreateOpenConnection()` calls `ClearReadOnlyAttributes(path)` before
  `connection.Open()`). `LoadAppSettings()` and other read paths therefore clear the
  Windows read-only bit on `settings.dat`/`-wal`/`-shm`/`-journal` as a side effect.
- Fix: clear only on write paths, or open with `Mode=ReadOnly` when the attribute
  is present instead of mutating.

### B10. PROCESS — `AGENTS.md` untracked but canonical
- Introduced: `c24a20d8`; `.gitignore:422-424` ignores `BUGSNEEDFIX.md`,
  `BUGSTOBEFIXED.md`, `AGENTS.md`. Verified: no tracked file is ignored (clean).
- Conversely the local `AGENTS.md` holds the updated canonical rules (unified
  `release.yml`, shared `Updater.exe`, `AppSettingsFilesAreIdentical`,
  no-quarantine, Warning+ bug reports) but fresh clones lose them — the version
  deleted in `c24a20d8` was stale (old `release-wpf.yml`/`version.txt`), the local
  copy is correct but never re-tracked. (This file, `BugNeedFix.md`, is
  case-distinct from the ignored `BUGSNEEDFIX.md`; keep the names distinct.)
- Fix: `git add -f AGENTS.md` and commit, or explicitly document local-only intent.

---

## C. Introduced after 2df0b6ea but already fixed — no action

- `00638868` merge wiped DB settings with defaults when only `favorites.dat`
  reappeared → fixed in `99a2f466` (`legacySettingsLoaded` gate + `VerifyMigration`
  `expectSettings` + `EnsureCreated` before first write).
- `00638868` favorites collapsed across systems (`DedupeByFileName` on `FileName`
  only) → fixed in `092b919b` + `99a2f466` (composite key + `UpgradeFavoritesToCompositeKey`).
- `3b5ebf34` PBP-corrupt logged as bug (`DiscConverter.cs:356` Error) → fixed in
  `99a2f466` (Information + partial-file cleanup).
- `386c16e7` invalid archive logged as bug (`CommanderGeniusLaunchStrategy`) → fixed
  in `99a2f466` (Information).
- `0eb3d9f1` card-slider NRE (`_settings` null during loader) → fixed in `05665010`
  (subscribe after `CardSizeSlider.Value = _settings.ThumbnailSize`).
- `d3d3557a` appsettings drift (`"Projec(t)64.exe"` regex chars only in the Avalonia
  copy, breaking `Merge-Payload` + `AppSettingsFilesAreIdentical`) → fixed in
  `99a2f466` (HEAD files byte-identical).
- Verified clean at HEAD, no regression: window titles (`"Simple Launcher"` both
  apps), loading-overlay pairing (reference-counted, all `try/finally`), `32563fba`
  card reload (null early-return safe), `06195a19` `PathHelper` (`C:\AppEvil` bypass
  closed), help bold-text + status-bar pairing, RA no-credentials panel, storefront
  scan logging (`418/429` → Information), updater hardening batches
  (`b1a75f82`/`9396d9c3`/`0252a90e`: staged swap + `.updbak` rollback, PID allowlist,
  ADS/link rejection), bundle layout (`Merge-Payload`, shared `Updater.exe`,
  `release_{v}_{rid}.zip` / `updater_{rid}.zip`), localization embedding
  (`WithCulture=false` + `LogicalName`, `pt-br→pt-BR`), concurrency
  (`AvaloniaGameCacheService`, `SaveAsync` semaphore), `f6246626` version sync (4
  csproj + 2 manifests at 5.8.0, `PublishSingleFile`, updater
  `IncludeNativeLibrariesForSelfExtract`).
- `f03d3c21` ("Remove separator") —geo hollow: separator removal only; no logic
  impact found. `d16385d7` test-only (ordinal comparisons). `c4fe8211`
  chore (ignore list). `62bcc11b` image path fix — verified, no regression.

---

## Suggested gating order for 5.8.0

1. Must-fix: A1 (shadow-copy resurrection), A2 (pre-merge backup), A4 (never
   quarantine newer schemas), A6 (portable-mode decision — code or docs, not
   silence), A5 (downgrade note + ignore defaults-only `settings.xml`).
2. Strongly recommended: A3 (DB-wins on re-merge), A8/A9 (single-transaction or
   stage-and-swap), A7 (`busy_timeout` + remove legacy-fallback writes), A10 (unify
   system.xml parsing), A11 (custom `SystemXmlPath`), B1 (CTS race), B2 (updater
   arg), B3 (silent-update log level).
3. Cheap should-fix: A14 (timestamped `.bak`), A12 (Information skip logs),
   A15/A16 (consistent dedupe + `NOCASE`), A17 (don't shelve unparseable files as
   success), A19/B9 (read-only handling), B5–B8.
4. Tests before sign-off: dual-folder migration, crash-between-tables retry,
   shelve-failure, `.bak`-preservation, custom-path, downgrade/re-upgrade,
   cross-variant loose-XML parity, BUSY/lock (A23).
