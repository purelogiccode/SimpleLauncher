# 05 — Configuration

> appsettings.json, settings.dat (SQLite), legacy settings.xml/system.xml, data-file locations, credentials.
> Related: [04 — Architecture](04-architecture.md) · [12 — Data Formats](12-data-formats.md)

## Overview of config layers

| Concern | Storage | Class |
|---|---|---|
| App-level keys (URLs, paths, lists) | `appsettings.json` (copied to output) | `Microsoft.Extensions.Configuration` |
| User preferences (UI, theme, gamepad, RA…) | `settings.dat` (SQLite `AppSettings` table, both apps); legacy `settings.xml` (LINQ-to-XML) | `SettingsManagerService` + `UnifiedSettingsDatabase` |
| System/game definitions | `settings.dat` (SQLite `Systems` table, both apps); legacy `system.xml` | `SystemManagerService` / `SystemConfigurationWriterService` |
| Favorites / play history / RA data / MAME data | Favorites + play history in `settings.dat` (`Favorites`/`PlayHistory` tables); `history.dat`, `mame.dat`, `RetroAchievements.dat` (MessagePack) | see [12 — Data Formats](12-data-formats.md) |
| Credentials (RA) | DPAPI-encrypted values inside `settings.dat` (`AppSettings`; legacy `settings.xml`) | `WindowsCredentialProtector` |
| Emulator-specific settings | each emulator's own config file + `samples\{Emulator}\*` templates | `InjectEmulatorConfig` services |

## appsettings.json keys (as read in code)

| Key | Read by | Purpose |
|---|---|---|
| `Urls:GameImageUrl` | `App.xaml.cs:151-157` | Game cover API base (GameImageClient, 20 s) |
| `Urls:EasyModeApi` | `App.xaml.cs:159-169` | Easy Mode component manifest API |
| `Urls:GameClassificationApi` | `App.xaml.cs:171-177` | Store-game classification API (30 s) |
| `Urls:ParameterResolverApi` | `App.xaml.cs:179-190` | AI parameter resolver API (60 s) |
| `Urls:RetroAchievementsApi`, `Urls:RetroAchievementsRequest`, `Urls:RetroAchievementsSite` | `RetroAchievementsService.cs:43-45` | RA API base URLs (defaults to retroachievements.org) |
| `StatusBarTimeoutSeconds` | `StartupInitializationService.cs:74-88` | Status-bar auto-clear (default 3 s) |
| `RequiredFiles` | `CheckForRequiredFilesService` | Startup file check list (note: `GetValue<string[]>` does not bind arrays — the hardcoded default list is used in practice; documented in `CheckForRequiredFilesServiceTests`) |
| `AdditionalFolders` | `CreateDefaultSystemFoldersService` | Extra folders created per system |
| `SystemXmlPath` | `DataFileLocation` | Override for `system.xml` location |
| `LogPath` | Serilog bootstrap | Log file directory override |
| `EmulatorsToSkipErrorChecking` | `GameLauncherService.cs:1524` | Exit-code check skip list |
| `EasyModeCacheDur*` | Easy Mode | Cache duration keys (research note — verify exact key names when editing) |

## Data-file location resolution (`DataFileLocation`)

`SimpleLauncher.Core\Services\DataFileLocation.cs` decides where a data file lives:

1. **Portable mode**: `{AppBaseDir}\{fileName}` — used if the portable file exists **and** is newer than the LocalAppData copy.
2. **LocalAppData**: `%LocalAppData%\SimpleLauncher\{fileName}` — fallback when the portable file is missing or older.
3. `TryFallbackToLocalAppData` (`:122-141`) handles write failures by relocating.

Affected files: `settings.dat`, `settings.xml`, `favorites.dat`, `playhistory.dat`, `RetroAchievements.dat`, `system.xml` (via `SystemXmlPath`).

## Unified database (`settings.dat`)

Since 5.8.0 both apps persist **all** user data in one SQLite database (`settings.dat`) in the
portable folder or `%LocalAppData%\SimpleLauncher\`; see
[12 — Data Formats](12-data-formats.md#unified-database-settingsdat) for the schema, migration
and merge behavior. The legacy files below are still read (first-launch migration) and can be
exported, but the database is the live store.

## settings.xml (`SettingsManagerService`) — legacy

`SimpleLauncher.Core\Services\SettingsManager\SettingsManagerService.cs`

- **File:** `DefaultSettingsFilePath = "settings.xml"` (`:221`); load `:287-322`, save `:565-685`.
- **Load:** `XElement.Load`; missing or corrupt → defaults + save. `LoadFromXml` (`:395-560`) validates values against whitelists (`:23-33`) and reads both `<Application>` children and legacy root-level elements.
- **Save:** read-lock snapshot (`CopyFrom` `:324-393`) → background thread → `BuildXElement` (`:687-764`) → temp file → atomic `File.Move` with 3 retries + exponential backoff; portable → LocalAppData fallback (`:627-642`); failure → `FailedToSaveSettingsMessageBoxAsync` (`:683`).

Persisted categories (`:38-155`): thumbnail sizes (games + system screen), `GamesPerPage`, `ShowGames`, `ViewMode`, `EnableGamePadNavigation`, `VideoUrl`/`InfoUrl` templates, `BaseTheme`/`AccentColor`/`StyleVariant`/`Language`, `DeadZoneX`/`DeadZoneY`, `ButtonAspectRatio`, `FilenameDisplayMode`, `DisplayMachineName`, filename/machine-name font sizes, `EnableFuzzyMatching` + `FuzzyMatchingThreshold` + `EnableAnnotationStripping`, notification sound setting, RA credentials (`RaUsername`/`RaApiKey`/`RaPassword`/`RaToken`, **DPAPI-encrypted** via `EncryptString/DecryptString` `:225-264`, written encrypted in `BuildXElement` `:717-719`), overlay-button booleans, emulator-section expansion states, `SystemPlayTimes`.

Plus 21 emulator settings classes (`SettingsManager\EmulatorSettings\`: Ares…Yumir incl. Xenia, Yumir, Mesen, Rpcs3…) used by the inject-config ViewModels.

## system.xml (`SystemConfigurationWriterService`)

`SimpleLauncher.Core\Services\SystemConfiguration\SystemConfigurationWriterService.cs`

Schema (as written by `CreateSystemXElement`, `:250-303`):

```xml
<SystemConfigs>
  <SystemConfig>
    <SystemName>Nintendo SNES</SystemName>
    <SystemFolders><SystemFolder>.\roms\Nintendo SNES</SystemFolder>…</SystemFolders>
    <SystemImageFolder>.\images\Nintendo SNES</SystemImageFolder>
    <FileFormatsToSearch><FormatToSearch>zip</FormatToSearch>…</FileFormatsToSearch>
    <GroupByFolder>false</GroupByFolder>
    <DisableRecursiveSearch>false</DisableRecursiveSearch>
    <ExtractFileBeforeLaunch>true</ExtractFileBeforeLaunch>
    <FileFormatsToLaunch><FormatToLaunch>smc</FormatToLaunch>…</FileFormatsToLaunch>
    <Emulators>
      <Emulator>
        <EmulatorName>RetroArch Snes9x</EmulatorName>
        <EmulatorLocation>%BASEFOLDER%\emulators\RetroArch\retroarch.exe</EmulatorLocation>
        <EmulatorParameters>-L "%EMULATORFOLDER%\cores\snes9x_libretro.dll" -f</EmulatorParameters>
        <ReceiveANotificationOnEmulatorError>true</ReceiveANotificationOnEmulatorError>
        <ImagePackDownloadLink1..5>…</ImagePackDownloadLink1..5>
        <ImagePackDownloadExtractPath>…</ImagePackDownloadExtractPath>
      </Emulator>
    </Emulators>
  </SystemConfig>
</SystemConfigs>
```

Write behavior: alphabetically sorted (ordinal-ignore-case), XML-UTF8-indented, temp file + `File.Move`, 3 retries with 500 ms backoff, `SystemExists` case-insensitive. `EmulatorXmlHelpers` (`SettingsManager\EmulatorXmlHelpers.cs`) reads typed values with a fallback chain: section element → flattened root element (`{SectionName}{PropertyName}`) → default.

Interfaces: `ISystemManager` (`SystemName`, `SystemFolders`, `PrimarySystemFolder`, `SystemImageFolder`, `FileFormatsToSearch`, `FileFormatsToLaunch`, `Emulators`, `GroupByFolder`, `DisableRecursiveSearch`, `ExtractFileBeforeLaunch`) and `IEmulator` (`EmulatorName`, `EmulatorLocation`, `EmulatorParameters`, `ReceiveANotificationOnEmulatorError`, `ImagePackDownloadLink1..5`, `ImagePackDownloadExtractPath`).

## Path placeholders

Resolved at launch time by `PathHelper`/`ResolveParameterString` (`GameLauncherService.cs:1063-1070`):

| Placeholder | Meaning |
|---|---|
| `%BASEFOLDER%` | Directory of `SimpleLauncher.exe` |
| `%SYSTEMFOLDER%` | First `<SystemFolder>` of the current system |
| `%EMULATORFOLDER%` | Directory of the emulator executable |
| `%ROM%` | Full path of the ROM (path + extension) |
| `%NAME%` | ROM name without path/extension |
| `%ROMSYSTEMFOLDER%` | The system folder that contains the selected ROM |

If none of the ROM placeholders is present, the ROM path is auto-appended (MAME/Raine get the bare machine name instead).

## Credentials

- `ICredentialProtector` → `WindowsCredentialProtector` (DPAPI, `DataProtectionScope.CurrentUser`, fixed entropy `"SimpleLauncher.Salt"`; Base64 ciphertext; empty in → empty out; tampered data → null). Used for RA credentials and DuckStation token encryption.
- `NoOpCredentialProtector` exists in tests.

## Related docs

- [04 — Architecture](04-architecture.md)
- [06 — Systems & Launch](06-systems-and-launch.md)
- [12 — Data Formats](12-data-formats.md)
- [18 — Emulator Parameters](18-emulator-parameters.md)
