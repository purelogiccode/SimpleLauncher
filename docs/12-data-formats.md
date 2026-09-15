# 12 — Data Formats

> Every persistence format used by Simple Launcher and where it lives.
> Related: [05 — Configuration](05-configuration.md) · [07 — Core Services](07-core-services.md)

## Format map

| Format | Files | Written/read by |
|---|---|---|
| **XML (LINQ-to-XML)** | `settings.xml`, `system.xml` (legacy in both apps), emulator `*.ini`-adjacent XML (Ares `settings.bml`, Cemu `settings.xml`) | `SettingsManagerService`, `SystemConfigurationWriterService`, `EmulatorXmlHelpers`, inject services |
| **MessagePack** | `favorites.dat`, `playhistory.dat` (legacy in both apps), `history.dat`, `mame.dat`, `RetroAchievements.dat`, `all_ra_games.dat` | `FavoritesManager`, `PlayHistoryManager`, `RomHistoryLoader`, `MameManagerService`, `RetroAchievementsManager` |
| **SQLite** | `settings.dat` (unified store, WPF + Avalonia), Amazon Games DB (read-only), Stella `stella.sqlite3` (settings upserts) | `UnifiedSettingsDatabase` + `AvaloniaLegacyMigrator`/`WpfLegacyMigrator` (Microsoft.Data.Sqlite), `ScanAmazonGames`, `StellaConfigurationService` |
| **JSON** | `appsettings.json` (config), `LauncherInstalled.dat`/manifests (Epic), GOG `.info`, Humble `config.json`, itch `.itch.toml`-adjacent, Mesen `settings.json`, BizHawk `config.ini` (JSON), API payloads | app config, scanners, `MesenConfigurationService`, `RetroAchievementsEmulatorConfiguratorService`, `JsonSerializer` |
| **YAML** | RPCS3 `config.yml` | `RPCS3ConfigurationService` (YamlDotNet round-trip) |
| **TOML** | Xenia `xenia[-canary].config.toml`, Yumir `Ymir.toml` | `XeniaConfigurationService`, `YumirConfigurationService` (Tomlyn) |
| **Plain text** | `parameters.md`, `WhatsNew.md`, `.extraction_in_progress` marker, `version.txt`, batch files | help/update/extraction/updater |
| **INI** | most emulator configs (`retroarch.cfg`, `PCSX2.ini`, `Dolphin.ini`, `settings.ini`…) | `InjectEmulatorConfig\*` + RA configurator INI helpers |

## MessagePack data files

| File | Content | Class |
|---|---|---|
| `favorites.dat` | favorite games (system + filename) | `FavoritesManager` (`Services\Favorites\`, app) |
| `playhistory.dat` | play sessions: date, play count, total seconds | `PlayHistoryManager` (`Services\PlayHistory\`, app) |
| `history.dat` | ROM history database (`HistoryData`/`EntryData`/`ItemData`/`SoftwareData`) | `RomHistoryLoader` (Core) |
| `mame.dat` | MAME machines + software-list descriptions | `MameManagerService` (Core) |
| `RetroAchievements.dat` | RA game info, achievements, progress | `RetroAchievementsManager` (Core) |
| `all_ra_games.dat` | full RA game database (built by `Tools\RetroAchievements.DataFetcher`) | RA fetcher tool |

**history.xml → history.dat migration** (`RomHistoryLoader`): the loader prefers `history.dat` (faster, smaller); if absent it falls back to `history.xml` (legacy). `Tools\XmlToBinaryConverter` performs the conversion.

`BoolConverter` (Core `Models\BoolConverter.cs`) handles APIs that return booleans as numbers (`0`/`1`).

## settings.xml details

- LINQ-to-XML, root `<Application>`; atomic save via temp file + `File.Move` with 3 retries + exponential backoff; portable → LocalAppData fallback (`SettingsManagerService.SaveAsync`, `:565-685`).
- Emulator settings blocks (`EmulatorSettings\` ×21) are serialized as child elements; RA credentials are **DPAPI-encrypted** (Base64) before writing (`:717-719`).

## system.xml details

- `SystemConfigs` root; entries alphabetically sorted (ordinal-ignore-case); UTF-8, 2-space indent; `XDeclaration` ensured; retry ×3 with 500 ms backoff; temp file + `File.Move`; empty or corrupt root replaced with a fresh `<SystemConfigs/>`. See [05 — Configuration](05-configuration.md#systemxml).

## Unified database (`settings.dat`)

- Both the WPF app and the Avalonia app store **all** user data — favorites, play
  history, application + emulator settings, per-system play times, and system
  configurations — in a single SQLite database named `settings.dat` (SQLite content,
  legacy extension) inside the AppData folder (`%LocalAppData%\SimpleLauncher` on
  Windows, `~/.local/share/SimpleLauncher` on Linux/macOS). Same file works on all
  platforms (Microsoft.Data.Sqlite), so both apps share the same store.
- Schema (versioned via a `Meta` table): `AppSettings` (key/value),
  `EmulatorSettings` (one JSON blob per emulator), `Favorites`, `PlayHistory`,
  `Systems` (one JSON blob per system), `SystemPlayTimes`. Name/file keys are
  `COLLATE NOCASE`, matching the legacy OrdinalIgnoreCase semantics.
- First launch migrates `favorites.dat`, `playhistory.dat`, `settings.xml` and
  `system.xml` (portable location or AppData, newest wins) into the database, verifies
  row counts, then shelves each legacy file as `<name>.bak`. The migration is idempotent;
  if it fails, legacy files are left untouched and the next launch retries.
  See `UnifiedSettingsDatabase` (Core) and `AvaloniaLegacyMigrator` (Avalonia) /
  `WpfLegacyMigrator` (WPF).

## Emulator config files (injected)

| Emulator | File | Parser |
|---|---|---|
| Ares | `settings.bml` | XML |
| Azahar / DuckStation / Flycast / RetroArch / Redream / Dolphin / Blastem / Supermodel / Sega Model 2 / Raine / Mednafen / Daphne | `*.ini`/`*.cfg` | custom INI readers/writers (sections, insert/append) |
| PCSX2 | `PCSX2.ini` | INI with `[Achievements]` etc. |
| RPCS3 | `config.yml` | YamlDotNet (round-trip preserves structure) |
| Xenia / Yumir | `*.toml` | Tomlyn |
| Mesen | `settings.json` | `System.Text.Json.Nodes` |
| Stella | `stella.sqlite3` | SQLite upserts (sample DB copied when missing) |
| BizHawk (RA) | `config.ini` (JSON) | `JsonNode` |

Missing files are restored from `samples\{Emulator}\{file}` (copied from the app's `samples\` folder).

## Related docs

- [05 — Configuration](05-configuration.md)
- [09 — RetroAchievements](09-retroachievements.md)
- [11 — Bundled Tools](11-bundled-tools.md)
