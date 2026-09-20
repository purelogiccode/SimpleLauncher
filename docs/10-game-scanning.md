# 10 — Game Scanning

> Automatic detection of games from modern PC storefronts.
> Related: [07 — Core Services](07-core-services.md) · [11 — Bundled Tools](11-bundled-tools.md)

**Platform note:** storefront scanning reads the Windows registry and storefront install databases, so it only
works on Windows. On Linux/macOS the Avalonia app returns an empty result, hides the **Scan for Microsoft Windows
games** menu item, and skips the first-run scan (it goes straight to the Easy Mode prompt). A Linux-native game
scanner would be a separate engine.

## Orchestration (`GameScannerService`)

`SimpleLauncher\Services\GameScan\GameScannerService.cs`

- `ScanForStoreGamesAsync` (`:77-97`): ensures the **"Microsoft Windows"** system exists (`roms\Microsoft Windows` + images; `FileFormatsToSearch = url, lnk, bat`; existing system paths reused — `:99-170`), then runs **all scanners in parallel** (`Task.WhenAll`, `:87-89`).
- `IgnoredGameNames` (`:28-41`): Steamworks Common Redistributables, Unreal Engine, Fab UE Plugin, Quixel Bridge, DirectX, Google Earth VR, Spacewar, PC Health Check, Rockstar Games Launcher, Battle.net, Ubisoft Connect.
- **Image handling:** `TryDownloadImageFromApiAsync` (`:180-258`, `GameImageClient`, 2 attempts with 5 s retry), `FindAndSaveGameImageAsync` (`:269-293`), `ExtractIconFromGameFolderAsync` (`:303-332`), `FindMainExecutable` heuristics (`:334-379`: name match → contains → largest non-setup/launcher/unins exe), `TryGetExeFiles` (`:381-399`, returns null instead of throwing when the folder vanished between the `Directory.Exists` check and enumeration — Microsoft Store `WindowsApps` race).
- **Interface:** `IGamePlatformScanner.ScanAsync(GameScannerService, ILogger, windowsRomsPath, windowsImagesPath, ignoredGameNames)` — single method, one class per storefront.

## The 11 scanners (`Services\GameScan\`)

| Scanner | Data source | Output |
|---|---|---|
| `ScanAmazonGames` | SQLite `%LOCALAPPDATA%\Amazon Games\Data\Games\Sql\GameInstallInfo.sqlite` (read-only) | `.url` → `amazon-games://play/{id}` |
| `ScanBattleNetGames` | registry uninstall keys, UID regex `Battle.net.*--uid=(.*?)`, `BNetAppDef` InternalId→name map | `.url` → `battlenet://{uid}`; classics get `.bat` launching the exe |
| `ScanEaGames` | registry `HKLM\SOFTWARE\WOW6432Node\Electronic Arts\EA Core\Installed Games` + game-classification API (`GameClassificationClient` → `api/GameIdentification/IsAGame`, same as Microsoft Store) | `.url` → `origin2://game/launch?offerIds={contentId}` (only confirmed games) |
| `ScanEpicGames` | JSON `LauncherInstalled.dat` (preferred) or `Manifests\*.item` fallback; filters `UE_`/Falcon/plugins/editors/engines/DLC | `.url` → `com.epicgames.launcher://apps/{app}?action=launch&silent=true` |
| `ScanGogGames` | registry uninstall keys (Publisher `GOG.com`); DLC via `goggame-{id}.info` JSON `RootGameId` | `.bat` launching primary `PlayTasks` FileTask exe |
| `ScanHumbleGames` | JSON `%APPDATA%\Humble App\config.json` → `game-collection-4` | `.url` → `humble://launch/{machineName}` |
| `ScanItchioGames` | itch `apps` folder + `.itch.toml` `[[actions]]`; pretty name from exe `ProductName` | `.bat` launching first action exe |
| `ScanMicrosoftStoreGames` | PowerShell `Get-StartApps`/`Get-AppxPackage` (30 s timeout) + game-classification API; logo extraction | `.bat` → `start "" "shell:AppsFolder\{AppId}"` |
| `ScanRockstarGames` | registry uninstall regex + `RockstarGameDef` TitleId map | `.url` → `rockstargames://launch/{titleId}` |
| `ScanSteamGames` | registry SteamPath + `libraryfolders.vdf` (`ISteamVdfParser`) + `appmanifest_*.acf` + `sourcemods\gameinfo.txt` | `.url` → `steam://run/{appId}`; mods `steam://run/{baseAppId}//-game "{mod}"`; copies Steam artwork |
| `ScanUplayGames` | registry `HKLM\SOFTWARE\Ubisoft\Launcher\Installs` (32 & 64-bit views) | `.url` → `uplay://launch/{gameId}` |

## Icon extraction

`IconExtractor` (`Services\GameScan\IconExtractor.cs`): P/Invoke icon extraction → PNG files for store-game shortcuts (used when the cover API has no image).

## Related docs

- [01 — Overview](01-overview.md) (feature list)
- [07 — Core Services](07-core-services.md)
- [ManualTests.md](manual-tests.md) §9 — manual verification checklist for the scanners
