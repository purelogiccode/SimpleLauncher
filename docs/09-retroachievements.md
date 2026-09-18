# 09 — RetroAchievements

> The RetroAchievements integration: API client, system matching, hashing, credential injection, UI.
> Related: [07 — Core Services](07-core-services.md) · [12 — Data Formats](12-data-formats.md)

## Overview

```mermaid
flowchart LR
    U[RA Windows] --> S[RetroAchievementsService<br/>REST client]
    S --> RA[(retroachievements.org API)]
    H[HasherTool] --> F[RetroAchievementsFileHasher<br/>RetroAchievementsSharp CLI tool]
    H --> M[SystemMatcher]
    M --> S
    C[EmulatorConfigurator] --> E[RetroArch / PCSX2 / DuckStation /<br/>PPSSPP / Dolphin / Flycast / BizHawk configs]
    Mgr[RetroAchievementsManager<br/>local .dat store] --> S
    Scn[RetroAchievementsHashScanner<br/>Core] --> F
    Scn --> Str[RetroAchievementsHashStore<br/>JSON per system]
    Btn[RA filter button] --> Scn
    Btn --> Mgr
```

HTTP client: named `"RetroAchievementsClient"` via `IHttpClientFactory` (`RetroAchievementsService.cs:37`; registered `App.xaml.cs:145-149` — 30 s timeout, `User-Agent: SimpleLauncher/1.0`). Base URLs from config `Urls:RetroAchievementsApi/Request/Site` (defaults `https://retroachievements.org/API/…`). Auth: API key as `y=`, username as `u=` query params; every call throws `RaUnauthorizedException` on HTTP 401.

## API client (`RetroAchievementsService`, app project)

| Method | Endpoint | Returns |
|---|---|---|
| `GetSessionTokenAsync(user, pass)` | POST `dorequest.php` (`r=login`) | session token |
| `GetGameInfoAndUserProgressAsync(gameId, user, key)` | `API_GetGameInfoAndUserProgress.php` | `RaUserGameProgress` + `RaAchievement[]` (incl. hardcore points) |
| `GetGameExtendedAsync(gameId, user, key)` | `API_GetGameExtended.php` | `RaGameExtendedDetails` |
| `GetUserGameRankAndScoreAsync(...)` | `API_GetUserGameRankAndScore.php` | `List<RaUserGameRank>` |
| `GetGameRankAndScoreAsync(..., latestMasters)` | `API_GetGameRankAndScore.php` (`t=1|0`) | `List<RaGameRankAndScore>` |
| `GetUserProfileAsync(user, key)` | `API_GetUserProfile.php` | `RaProfile` |
| `GetUserRecentlyPlayedGamesAsync(..., count, offset)` | `API_GetUserRecentlyPlayedGames.php` | recently played |
| `GetAchievementsEarnedBetweenAsync(..., from, to)` | `API_GetAchievementsEarnedBetween.php` (epoch `f/t`) | `List<RaEarnedAchievement>` |
| `GetUserCompletionProgressAsync(..., count=100, offset=0)` | `API_GetUserCompletionProgress.php` | paginated completion list (site URL prefixed onto `ImageIcon`) |

## System matching (`RetroAchievementsSystemMatcher`, Core)

- `SystemMappings`: static dictionary official RA system name → `RaSystemInfo { Id, Aliases[] }` — ~80 systems, plus `"unsupported"` (ID 102) with a huge alias list (PS3/PS4/Xbox/Switch/Windows…) (`:27-186`).
- `GetBestMatchSystemName` (`:193-217`): lowercase/trim, exact scan over all aliases; unmatched logged once per name.
- `GetExactAliasMatch` (`:256-271`), `IsSystemInMappings` (`:279-305`, contains/substring), `GetSupportedSystemNames` (`:233-236`), `GetSystemId` (`:243-249`, −1 if unknown).

## Hashing (`RetroAchievementsHasherTool` + `RetroAchievementsFileHasher`, Core)

All hash computation is delegated to the **bundled RetroAchievementsSharp CLI tool**
(`tools\RetroAchievementsSharp\RetroAchievementsSharp.exe`, `_arm64.exe` for arm64 — a
1:1 C# port of the rcheevos hashing engine that produces the exact same hashes as RAHasher).
The in-process NuGet library, the old custom MD5 logic, and the previous RAHasher binary are
all gone: the file hasher spawns the CLI (single-file mode for one game, `scan --console <id>
--format json` batch mode for whole systems) and reads the hash/manifest back. The system
matcher resolves the system name to its official RA console ID (passed **numerically** —
NULL-group consoles like 3DS can only be addressed by numeric id), and the tool handles every
console's algorithm internally (whole-file MD5, header stripping, N64 byte-swapping, arcade
filename hashing, Arduboy line-ending normalization, disc hashing, RVZ/WIA via RVZSharp, …).

| Concern | Implementation |
|---|---|
| Console dispatch | `RetroAchievementsSystemMatcher.GetSystemId` → CLI positional mode `RetroAchievementsSharp <consoleId> <file>` |
| Complex discs (PS1/Saturn/Dreamcast/…) | hashed directly by the CLI — no `DiscConverter` conversion, no 60 s process timeout |
| GameCube/Wii `.rvz`/`.wia` | hashed **live** by the CLI via RVZSharp (decode-on-read, no temp ISO) |
| `.zip`/`.7z`/`.rar` | `.zip` handled by the CLI itself (first entry pre-load); `.7z`/`.rar` extracted to temp first (except arcade, which hashes the file name) |
| 3DS | requires decryption keys (`-s`/`Hash3Ds`); without them the CLI produces no hash → hash null |
| Unsupported input | the CLI prints no hash and exits non-zero → hash null, logged at Information |

Flow (`GetGameHashForRetroAchievementsAsync`): exact alias match or **system-picker prompt**
(`SystemSelectionWindow` with fuzzy pre-selected guess; cancel → `RaHashResult(null,null,false,…)`);
systems without a usable console ID (e.g. the `unsupported` pseudo-system, ID > 90) are rejected
up front; **`.zip` archives are passed to the CLI directly** (it pre-loads the first entry —
single entry from memory, multi-entry hashes the whole archive, oversized entries fall back to
a temp file) — no extraction unless really needed; only `.7z/.rar` archives are extracted to
temp (except arcade-based systems — console 27, including Naomi — which hash the file name);
single hash call; temp cleaned.

Hash support is decided in one place: `RetroAchievementsSystemMatcher.IsSystemSupportedForHashing`
(valid console id and known hash logic) is the **single source of truth** shared by the hasher
tool (RA icon) and the background hash scanner (`IsSystemScannable`).

## Hash-based game filter (`RetroAchievementsHashScanner` + `RetroAchievementsHashStore`, Core)

The **RetroAchievements filter button** (trophy icon on the main window) no longer matches
games by filename. It matches by **hash**:

1. On click, the filter checks whether a hash scan result exists for the selected system.
2. If none exists, the user is prompted: *"We need to scan your game path to see what game is
   compatible with RetroAchievements."*
   - **Cancel** → nothing happens (filter stays disabled).
   - **OK** → the entire game path is scanned **in the background**; the user is told they can
     click the button again later to see if the hashing is complete. A **notification toast** is
     shown when the calculation finishes.
3. Once a scan result exists, the filter keeps only game files whose hash resolves to a known
   game in `RetroAchievementsManager.GetGameInfoByHash` — i.e. the hash is compared against the
   compatible-hash set loaded from `RetroAchievements.dat` (see [Local data store](#local-data-store)),
   and only the matched games are displayed.

Parallel hash calculations are **prevented** (single global scan flag) — clicking the button or
running the menu command while a scan is running shows an in-progress toast instead of starting
a second scan.

### Re-scan optimization

Before hashing a system, the scanner enumerates the ROM path and compares the **number of game
files** against the `FileCount` stored in the system's JSON file. If the count is unchanged, the
scan is skipped entirely — hashes are only recalculated when a game was added to or removed from
the path, since there is no need to hash again when nothing changed. The explicit **Rescan
RetroAchievements** command passes `force: true` and always re-hashes, so replacing a ROM with a
different revision (same count and name) can be refreshed.

### Persistence

Each scanned system produces a JSON file in `%LocalAppData%\SimpleLauncher\RetroAchievementsHashes\`:

| File | Content |
|---|---|
| `{SystemName}.json` | `RaSystemHashes` — `SystemName`, `ScannedAtUtc`, `FileCount` (number of games at scan time), `Hashes` (dictionary of full file path → hash) |

Files are written via `RetroAchievementsHashStore` (System.Text.Json, indented) and read back
for filtering. The scan enumerates the exact same file set as the game-list cache
(`IGetListOfFilesService.GetFilesAsync`), hashes the directly-supported files (including
`.zip`) in **one batch CLI invocation per system** (`scan --console <id> --format json`),
hashes `.7z`/`.rar` individually after extraction, and persists one result file per system.
Only one scan runs at a time (avoids spawning many CLI processes at once).

**Zips are handled by the CLI tool itself** — its `scan`/positional zip pre-load mirrors the
old library behavior (`FileUtil.LoadZippedFile` + `GenerateFromBuffer`): single-entry `.zip`
files are loaded into memory and hashed from a buffer (no disk extraction); multi-entry zips
hash the whole archive; entries too large for a `byte[]` fall back to a temporary file that
is deleted afterwards. Only `.7z`/`.rar` archives are extracted to a temporary folder before
hashing (unless the system is arcade, which hashes by file name). The hash is always
persisted under the **original archive path** so the game list can match it.
`RaSystemHashes.HashVersion` records the hash-logic version: when it changes (e.g. extraction
rules), previously scanned systems are re-hashed automatically even if the game count is
unchanged.

### Menu command

**Options → RetroAchievements → Calculate hash for all Game Paths** runs the same background
scan for every configured system (systems not supported for hashing — no usable console ID or
unknown hash logic — are skipped), with a completion toast per system and a single in-progress
toast on repeat clicks.

## Credential injection (`RetroAchievementsEmulatorConfiguratorService`, Core)

| Emulator | File / section | Notes |
|---|---|---|
| RetroArch | `retroarch.cfg` | keys `cheevos_enable/username/password/hardcore_mode_enable`, `" = "` format |
| PCSX2 | `PCSX2.ini` → `[Achievements]` | `inis\` beside exe or `%MyDocuments%\PCSX2\inis` |
| DuckStation | `settings.ini` → `[Cheevos]` | token **encrypted** (`EncryptDuckStationToken`); portable if `portable.txt` |
| PPSSPP | `memstick\PSP\SYSTEM\ppsspp.ini` + **`ppsspp_retroachievements.dat`** | TitleCase keys; token in separate `.dat` |
| Dolphin | `RetroAchievements.ini` → `[Achievements]` | portable `User\Config\` or `%MyDocuments%\Dolphin Emulator\Config` |
| Flycast | `emu.cfg` → `[achievements]` | `yes/no` values; exe dir or `%APPDATA%\flycast\` |
| BizHawk | `config.ini` (JSON) | flat root keys `RAUsername/RAToken/RACheevosActive/RAHardcoreMode/…` |

Missing/0-byte configs are restored from `samples\{emulatorFolderName}\{filename}`. INI helpers: `UpdateSimpleIniFile` (`:358-415`), `UpdateIniFile` with sections (`:418-493`). Wired from `RetroAchievementsSettingsViewModel` (`:125-131`).

## Local data store

`RetroAchievementsManager` persists API data to `RetroAchievements.dat` (MessagePack): `RaGameInfo` rows (id, title, console, hashes), achievements, recently played, completion progress. `RaGameInfo` carries the hashes used for local matching.

## Models (`Ra*`)

`RaApiAchievement`, `RaEarnedAchievement`, `RaGameExtendedDetails`, `RaGameInfo`, `RaGameProgressResponse`, `RaGameRankAndScore`, `RaHashResult` (struct), `RaProfile`, `RaRecentlyPlayedGame`, `RaUnauthorizedException`, `RaUserCompletionGame`, `RaUserCompletionProgressResponse`, `RaUserGameProgress`, `RaUserGameRank` — see [07 — Core Services](07-core-services.md#models) for locations.

## Windows

- `RetroAchievementsWindow` — browse profile, unlocks, completion progress.
- `RetroAchievementsForAGameWindow` — per-game achievements/rankings/progress (badges, hardcore 🏆, rarity).
- `RetroAchievementsSettingsWindow` — credentials + "Configure Emulator" for the 7 supported emulators.
- `SystemSelectionWindow` — system picker when auto-matching is unsure.

Credentials (username/API key/password/token) are stored **DPAPI-encrypted** in the unified `settings.dat` database (legacy: `settings.xml`) — see [05 — Configuration](05-configuration.md#credentials).

## Related docs

- [07 — Core Services](07-core-services.md)
- [12 — Data Formats](12-data-formats.md)
- [05 — Configuration](05-configuration.md)
