# SimpleLauncher.Tests

xUnit test project for [SimpleLauncher](../SimpleLauncher). These tests validate resource localization, URL health, update logic, emulator configurations, game management, and various utility functions.

## Running Tests

```bash
dotnet test SimpleLauncher.Tests
```

To run a specific test class:

```bash
dotnet test SimpleLauncher.Tests --filter "FullyQualifiedName~DetectMissingResourceStringsTests"
```

To run tests by category:

```bash
dotnet test SimpleLauncher.Tests --filter "FullyQualifiedName~DetectMissing"
dotnet test SimpleLauncher.Tests --filter "FullyQualifiedName~UrlValidation"
dotnet test SimpleLauncher.Tests --filter "FullyQualifiedName~EmulatorConfig"
```

## Test Categories

### Resource Localization Tests

| Test | Purpose |
|------|---------|
| `DetectMissingResourceProviderKeysTests` | Finds keys used via `_resourceProvider.GetString(...)` that are missing from the shared `SimpleLauncher.Core\Localization\strings.en.json`. **Auto-writes** missing keys (with defaults) and fails so the developer is notified; also reports duplicate keys. |
| `DetectMissingResourceStringsTests` | Finds keys used via `TryFindResource("Key")` that are missing from `strings.en.json`. **Auto-writes** missing keys (with fallbacks) and fails so the developer is notified. |
| `DetectMismatchedResourceStringsTests` | Detects when the same `TryFindResource("Key")` is used with **different** fallback strings (`?? "Value"`) across the codebase. |
| `DetectDuplicateResourceKeysTests` | Scans every `strings.*.json` for duplicate keys. **Fails** if duplicates with conflicting values are found. |
| `DetectAlphabeticalOrderingTests` | Verifies that every `strings.*.json` has entries sorted alphabetically by key. **Auto-re-sorts** unsorted files and fails to notify the developer. |
| `ResourceFileLoadingTests` | Loads every localization pack embedded in `SimpleLauncher.g.resources` (`resources/strings.*.json`) and verifies it parses to a non-empty JSON object. |
| `LocalizationResourcePackagingTests` | Verifies every selectable language is embedded as a manifest resource and that no unknown pack is embedded. |

Language key parity and empty-value checks for the shared packs live in the Avalonia suite (`LocalizationTests`).

### URL & API Validation Tests

| Test | Purpose |
|------|---------|
| `UrlValidationTests.ParametersMdAllUrlsAreReachable` | Checks every URL inside `parameters.md` is reachable. |
| `UrlValidationTests.EasyModeApiEndpointIsReachable` | Validates the EasyMode API endpoints return valid JSON arrays. |
| `UrlValidationTests.EasyModeFallbackXmlIsReachableAndContainsValidUrls` | Downloads fallback XML files and recursively validates every URL inside them. |
| `ApiConnectivityTests` | Tests connectivity to the bug report and statistics APIs used by SimpleLauncher. |

### Update Simulation Tests

| Test | Purpose |
|------|---------|
| `ExtractAllFromZip_ExtractsFilesCorrectly` | Creates an in-memory ZIP, extracts it via the real `UpdateChecker.ExtractAllFromZip` logic, verifies contents, and cleans up. |
| `ExtractAllFromZip_RejectsPathTraversal` | Ensures the extractor blocks path-traversal attacks (e.g. `../../../etc/passwd`). |
| `ExtractAllFromZip_RejectsDangerousExtensions` | Ensures dangerous file types (`.exe`, `.bat`, etc.) are blocked. |
| `IsNewVersionAvailable_SemanticComparison` | Tests semantic version comparison logic via reflection. |
| `NormalizeVersion_StripsPrefixesAndSuffixes` | Tests version string normalization. |
| `ParseVersionAndAssetUrlsFromResponse_ValidJson` | Tests GitHub release JSON parsing. |

### Version Consistency Tests

| Test | Purpose |
|------|---------|
| `VersionConsistencyTests` | Ensures version metadata across the repository stays in sync with the canonical version in `SimpleLauncher.csproj`. **Auto-rewrites** mismatched files and fails for developer review. |

### Emulator Configuration Tests

| Test | Purpose |
|------|---------|
| `EmulatorConfigInjectionTests` | Tests emulator configuration injection logic. |
| `EmulatorConfigInjectionTests2` | Additional emulator configuration injection tests. |
| `EmulatorConfigInjectionExtendedTests` | Extended tests for emulator configuration injection. |
| `EmulatorConfigTests` | Tests emulator configuration models. |
| `EmulatorSettingsTests` | Tests emulator settings management including defaults, round-trips, copy, and reset. |
| `EmulatorTests` | Tests emulator-related functionality. |
| `MednafenConfigInjectionTests` | Tests Mednafen-specific configuration injection. |
| `StellaConfigInjectionTests` | Tests Stella-specific configuration injection. |
| `DaphneConfigurationServiceTests` | Tests Daphne configuration service. |
| `InjectionErrorHandlerTests` | Tests error handling during configuration injection. |

### Game Management Tests

| Test | Purpose |
|------|---------|
| `FindGameFileTests` | Tests game file discovery logic. |
| `FindCoverImageJaroWinklerTests` | Tests fuzzy image matching using Jaro-Winkler distance. |
| `GameScannerServiceTests` | Tests game scanning service. |
| `GameFilterServiceTests` | Tests game filtering logic. |
| `GameFilterServiceExtendedTests` | Extended game filtering tests. |
| `GameCacheServiceTests` | Tests game caching service. |
| `GameCacheServiceExtendedTests` | Extended game cache tests. |
| `GameButtonViewModelTests` | Tests game button view model. |
| `GameButtonTagTests` | Tests game button tag functionality. |
| `GameButtonTagExtendedTests` | Extended game button tag tests. |
| `GameListViewItemTests` | Tests game list view item model including defaults, property changes, and edge cases. |
| `MountZipFilesTests` | Tests ZIP file mounting logic. |
| `ChdMountStrategyMatchTests` | Tests CHD mount strategy matching. |
| `ChdToCueStrategyMatchTests` | Tests CHD-to-CUE conversion strategy matching. |
| `ValidateBatchFileTests` | Tests batch file validation including missing paths, comments, and quoted path detection. |
| `RightClickContextTests` | Tests right-click context menu functionality. |
| `SearchOrchestratorServiceTests` | Tests search orchestration service. |
| `SearchValidationResultTests` | Tests search validation results. |
| `PaginationServiceTests` | Tests pagination service. |
| `PaginationServiceExtendedTests` | Extended pagination tests. |

### Favorites Tests

| Test | Purpose |
|------|---------|
| `FavoritesManagerTests` | Tests favorites management including serialization, edge cases, and large datasets. |
| `FavoriteTests` | Tests favorite model including required properties, defaults, and property changes. |
| `BooleanToFavoriteStatusConverterTests` | Tests boolean to favorite status converter. |

### Play History & Statistics Tests

| Test | Purpose |
|------|---------|
| `PlayHistoryManagerTests` | Tests play history management including serialization, unicode, and large lists. |
| `PlayHistoryItemTests` | Tests play history item model including display name, formatting, and property changes. |
| `RomHistoryLoaderTests` | Tests ROM history loading. |
| `GlobalStatsDataTests` | Tests global statistics data model. |
| `GlobalStatsViewModelTests` | Tests global statistics view model. |
| `StatsNormalizeEmulatorNameTests` | Tests emulator name normalization for statistics. |
| `SystemStatsDataTests` | Tests system statistics data model. |
| `SystemPlayTimeTests` | Tests system play time tracking including formatting and property updates. |

### System Manager Tests

| Test | Purpose |
|------|---------|
| `SystemManagerTests` | Tests system manager functionality. |
| `SystemManagerExtendedTests` | Extended system manager tests. |
| `SystemManagerConfigTests` | Tests system manager configuration. |
| `SystemManagerXmlPersistenceTests` | Tests XML persistence for system manager. |
| `EasyModeSystemConfigTests` | Tests EasyMode system configuration including defaults, validation, and edge cases. |
| `DataFileLocationTests` | Tests data file location resolution. |
| `DirectoryValidationServiceTests` | Tests directory validation service. |
| `ParameterResolverResultTests` | Tests parameter resolver results. |

### UI & Converter Tests

| Test | Purpose |
|------|---------|
| `InverseBooleanConverterTests` | Tests boolean inversion converter including round-trip and edge cases. |
| `ImageUrlConverterTests` | Tests image URL converter. |
| `DownloadButtonStateTests` | Tests download button state management. |
| `DownloadProgressEventArgsTests` | Tests download progress event args. |
| `ImagePackDownloadItemTests` | Tests image pack download item model. |
| `DebugViewModelTests` | Tests debug view model. |
| `DosBoxFileItemTests` | Tests DOSBox file item model. |
| `DosBoxFileItemExtendedTests` | Extended DOSBox file item tests. |

### File System & Utility Tests

| Test | Purpose |
|------|---------|
| `FormatFileSizeTests` | Tests file size formatting including boundary values and unit suffixes. |
| `FormatFileSizeServiceTests` | Tests file size formatting service. |
| `PathHelperTests` | Tests path helper utilities. |
| `CheckPathTests` | Tests path validation. |
| `CheckIfDirectoryIsWritableTests` | Tests directory write permission checks. |
| `CheckForFileLockTests` | Tests file lock detection. |
| `FileLockServiceTests` | Tests file lock service. |
| `DeleteFilesTests` | Tests file deletion logic. |
| `CleanTempFolderTests` | Tests temporary folder cleanup. |
| `CleanTempFolderExtendedTests` | Extended temporary folder cleanup tests. |
| `CleanSimpleLauncherFolderServiceTests` | Tests SimpleLauncher folder cleanup service. |
| `SteamVdfParserTests` | Tests Steam VDF file parser. |
| `LaunchContextTests` | Tests launch context management. |

### Security & Input Tests

| Test | Purpose |
|------|---------|
| `SanitizeInputSystemNameTests` | Tests input sanitization for system names including invalid characters, reserved names, and edge cases. |
| `InputSanitizerServiceTests` | Tests input sanitizer service. |
| `EncryptDuckStationTokenTests` | Tests DuckStation token encryption. |
| `BugReportFormatterTests` | Tests bug report formatting. |

### RetroAchievements Tests

| Test | Purpose |
|------|---------|
| `RetroAchievementsSystemMatcherTests` | Tests RetroAchievements system matching including aliases, official names, and system IDs. |
| `RetroAchievementsFileHasherTests` | Tests RetroAchievements file hashing. |
| `RetroAchievementsManagerTests` | Tests RetroAchievements manager. |

### System Utility Tests

| Test | Purpose |
|------|---------|
| `GetMicrosoftWindowsVersionTests` | Tests Windows version detection. |
| `MameManagerTests` | Tests MAME manager functionality. |
| `MameManagerExtendedTests` | Extended MAME manager tests. |

## Known Expected Failures

Some tests are designed to **fail** to alert the admin of real data issues that must be fixed manually:

- **Duplicate keys** in the shared `strings.*.json` packs (`DetectDuplicateResourceKeysTests`)
- **Keys used in WPF source but missing from `strings.en.json`** (`DetectMissingResourceProviderKeysTests` / `DetectMissingResourceStringsTests`; auto-added when a fallback exists)
- **Unsorted resource keys** in localization files (`DetectAlphabeticalOrderingTests`)
- **Version mismatches** across project files (`VersionConsistencyTests`)
- **Broken or unreachable URLs** in `parameters.md` or EasyMode XML (`UrlValidationTests`)

## Project Structure

```
SimpleLauncher.Tests/
├── SimpleLauncher.Tests.csproj          # Project file (xUnit, .NET 10)
├── TestHelpers/                         # Shared test utilities
│
├── # Resource Localization
├── DetectMissingResourceProviderKeysTests.cs
├── DetectMissingResourceStringsTests.cs
├── DetectMismatchedResourceStringsTests.cs
├── DetectDuplicateResourceKeysTests.cs
├── DetectAlphabeticalOrderingTests.cs
├── ResourceFileLoadingTests.cs
├── LocalizationResourcePackagingTests.cs
│
├── # URL & API Validation
├── UrlValidationTests.cs
├── ApiConnectivityTests.cs
│
├── # Update & Version
├── UpdateSimulationTests.cs
├── VersionConsistencyTests.cs
│
├── # Emulator Configuration
├── EmulatorConfigInjectionTests.cs
├── EmulatorConfigInjectionTests2.cs
├── EmulatorConfigInjectionExtendedTests.cs
├── EmulatorConfigTests.cs
├── EmulatorSettingsTests.cs
├── EmulatorTests.cs
├── MednafenConfigInjectionTests.cs
├── StellaConfigInjectionTests.cs
├── DaphneConfigurationServiceTests.cs
├── InjectionErrorHandlerTests.cs
│
├── # Game Management
├── FindGameFileTests.cs
├── FindCoverImageJaroWinklerTests.cs
├── GameScannerServiceTests.cs
├── GameFilterServiceTests.cs
├── GameFilterServiceExtendedTests.cs
├── GameCacheServiceTests.cs
├── GameCacheServiceExtendedTests.cs
├── GameButtonViewModelTests.cs
├── GameButtonTagTests.cs
├── GameButtonTagExtendedTests.cs
├── GameListViewItemTests.cs
├── MountZipFilesTests.cs
├── ChdMountStrategyMatchTests.cs
├── ChdToCueStrategyMatchTests.cs
├── ValidateBatchFileTests.cs
├── RightClickContextTests.cs
├── SearchOrchestratorServiceTests.cs
├── SearchValidationResultTests.cs
├── PaginationServiceTests.cs
├── PaginationServiceExtendedTests.cs
│
├── # Favorites
├── FavoritesManagerTests.cs
├── FavoriteTests.cs
├── BooleanToFavoriteStatusConverterTests.cs
│
├── # Play History & Statistics
├── PlayHistoryManagerTests.cs
├── PlayHistoryItemTests.cs
├── RomHistoryLoaderTests.cs
├── GlobalStatsDataTests.cs
├── GlobalStatsViewModelTests.cs
├── StatsNormalizeEmulatorNameTests.cs
├── SystemStatsDataTests.cs
├── SystemPlayTimeTests.cs
│
├── # System Manager
├── SystemManagerTests.cs
├── SystemManagerExtendedTests.cs
├── SystemManagerConfigTests.cs
├── SystemManagerXmlPersistenceTests.cs
├── EasyModeSystemConfigTests.cs
├── DataFileLocationTests.cs
├── DirectoryValidationServiceTests.cs
├── ParameterResolverResultTests.cs
│
├── # UI & Converters
├── InverseBooleanConverterTests.cs
├── ImageUrlConverterTests.cs
├── DownloadButtonStateTests.cs
├── DownloadProgressEventArgsTests.cs
├── ImagePackDownloadItemTests.cs
├── DebugViewModelTests.cs
├── DosBoxFileItemTests.cs
├── DosBoxFileItemExtendedTests.cs
│
├── # File System & Utilities
├── FormatFileSizeTests.cs
├── FormatFileSizeServiceTests.cs
├── PathHelperTests.cs
├── CheckPathTests.cs
├── CheckIfDirectoryIsWritableTests.cs
├── CheckForFileLockTests.cs
├── FileLockServiceTests.cs
├── DeleteFilesTests.cs
├── CleanTempFolderTests.cs
├── CleanTempFolderExtendedTests.cs
├── CleanSimpleLauncherFolderServiceTests.cs
├── SteamVdfParserTests.cs
├── LaunchContextTests.cs
│
├── # Security & Input
├── SanitizeInputSystemNameTests.cs
├── InputSanitizerServiceTests.cs
├── EncryptDuckStationTokenTests.cs
├── BugReportFormatterTests.cs
│
├── # RetroAchievements
├── RetroAchievementsSystemMatcherTests.cs
├── RetroAchievementsFileHasherTests.cs
├── RetroAchievementsManagerTests.cs
│
└── # System Utilities
    ├── GetMicrosoftWindowsVersionTests.cs
    ├── MameManagerTests.cs
    └── MameManagerExtendedTests.cs
```

## Test Dependencies

- **Framework:** .NET 10 (Windows)
- **Language:** C# 14
- **Test Runner:** xUnit 2.9.3
- **Mocking:** Moq 4.20.72
- **Coverage:** Coverlet 10.0.1
- **IDE Integration:** xUnit Runner for Visual Studio 3.1.5
