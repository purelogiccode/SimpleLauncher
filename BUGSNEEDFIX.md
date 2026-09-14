### CORE-06 — `Directory.CreateDirectory` failure swallowed, extraction continues
**File:** `SimpleLauncher.Core/Services/ExtractFiles/ExtractionService.cs:140-155`
```csharp
try { Directory.CreateDirectory(resolvedDestinationFolder); }
catch (Exception ex) { _logger.Error(ex, $"Failed to create directory: {resolvedDestinationFolder}"); }
var extractionTrackingFile = Path.Combine(resolvedDestinationFolder, ".extraction_in_progress");
await File.WriteAllTextAsync(extractionTrackingFile, ...);
```
On access-denied/quota logs and falls through to write + extraction instead of `return false`.
**Severity:** Major.

### CORE-07 — Command injection / quoting break via interpolated `Arguments`
**Files:** `Services/ExtractFiles/ExtractionService.cs:451`, `Services/Converters/DiscConverter.cs:52,142,296`, `Services/GameLauncher/MountFiles/MountZipFiles.cs:195,489,758`, `MountChdFiles.cs:63,184,392`, `MountXisoFiles.cs:72`
```csharp
var args = $"x -o\"{destinationFolder}\" -y \"{archivePath}\"";
var args = $"extractdvd -i \"{chdPath}\" -o \"{tempIsoPath}\"";
Arguments = $"\"{resolvedZipFilePath}\" \"{mountPathArgument}\"",
```
Filenames/folders are user-controlled (ROM names, Linux allows `"`). A `"` breaks quoting and injects flags into `7za/chdman/DolphinTool/SimpleZipDrive/CHDMounter`. Must use `ProcessStartInfo.ArgumentList`.
**Severity:** Major.

### CORE-08 — Shared `CancellationTokenSource` races on concurrent downloads
**File:** `SimpleLauncher.Core/Services/DownloadService/DownloadManager.cs:171-191,202-269`
```csharp
private void ResetCancellationToken() // :171
{ oldCts = _cancellationTokenSource; _cancellationTokenSource = new CancellationTokenSource(); }
internal async Task<string?> DownloadFileAsync(...) // :200
{ ResetCancellationToken(); token = _cancellationTokenSource.Token; }
```
Single `_cancellationTokenSource` per instance. Two overlapping `DownloadFileAsync` calls: second `ResetCancellationToken()` cancels/disposes CTS the first call still uses. No per-call CTS / semaphore.
**Severity:** Major.

### CORE-09 — Progress events raised on thread-pool, no dispatcher marshal
**File:** `SimpleLauncher.Core/Services/DownloadService/DownloadManager.cs:441,503-505`
```csharp
OnProgressChanged(new DownloadProgressEventArgs { ... }); // inside DownloadWithProgressAsync
protected virtual void OnProgressChanged(DownloadProgressEventArgs e)
{ DownloadProgressChanged?.Invoke(this, e); }
```
`ExtractFileAsync` wraps progress in `_dispatcherService.InvokeAsync`, but hot download loop does not. WPF/Avalonia subscribers updating UI get cross-thread exceptions.
**Severity:** Major.

### CORE-10 — `async void` timer callback
**File:** `SimpleLauncher.Core/Services/GamePad/GamePadController.cs:112,293`
```csharp
_timer = new Timer(_ => UpdateAsync(), null, Timeout.Infinite, Timeout.Infinite);
private async void UpdateAsync()
```
`async void` only valid for event handlers. Exceptions before inner `try` crash process; callers cannot await/cancel.
**Severity:** Major.

### CORE-11 — Scroll uses same axis twice (copy-paste)
**File:** `SimpleLauncher.Core/Services/GamePad/GamePadController.cs:780-781`
```csharp
var thumbX = state.RotationZ - 32767;
var thumbY = -(state.RotationZ - 32767);
```
Both X and Y derive from `RotationZ`; Y is just negated X. Horizontal/vertical scroll always mirrored. Should use distinct axes.
**Severity:** Major (logic).

### CORE-12 — `OnWatcherError` NRE — `GetException()` can be null
**File:** `SimpleLauncher.Core/Services/GameFileWatcher/GameFileWatcherService.cs:190-193`
```csharp
private void OnWatcherError(object sender, ErrorEventArgs e)
{ _logger.Debug($"[GameFileWatcherService] Watcher error: {e.GetException().Message}"); }
```
`ErrorEventArgs.GetException()` returns `null` for buffer-overflow notifications → `.Message` throws inside watcher thread. Must be `e.GetException()?.Message`.
**Severity:** Major.

### CORE-13 — Debounce can fire after `StopWatching`/`Dispose`
**File:** `SimpleLauncher.Core/Services/GameFileWatcher/GameFileWatcherService.cs:195-232`
```csharp
lock (_lock) { CancelPendingDebounce(); var cts = new CancellationTokenSource(); _debounceCts = cts; _ = Task.Run(...); }
private void CancelPendingDebounce()
{ var oldCts = Interlocked.Exchange(ref _debounceCts, null); oldCts?.Cancel(); oldCts?.Dispose(); }
```
`CancelPendingDebounce` uses `Interlocked` while writers assign under `lock`; `StopWatching` cancels outside watcher lock (`:156`). Concurrent `OnFileChanged` can publish new CTS after cancel; orphaned `Task.Run` invokes `GameFilesChanged` after dispose.
**Severity:** Major.

### CORE-14 — `BugReportApiSink.Dispose` orphans background consumer
**File:** `SimpleLauncher.Core/Services/DebugAndBugReport/BugReportApiSink.cs:67-74,115-129`
```csharp
public void Dispose() { if (_disposed) return; _disposed = true; _cts.Cancel(); _cts.Dispose(); }
// consumer:
while (await _channel.Reader.WaitToReadAsync(cancellationToken))
```
Cancel makes `WaitToReadAsync` throw into `_processTask`, never awaited/observed. In-flight `PostAsync` uses independent 30s CTS (`:176`), so `Dispose` doesn't cancel uploads. Channel never completed; `Emit` post-dispose still enqueues.
**Severity:** Major.

### CORE-15 — Undisposed `StringContent` per bug report
**File:** `SimpleLauncher.Core/Services/DebugAndBugReport/BugReportApiSink.cs:174`
```csharp
var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
using var response = await httpClient.PostAsync(apiUrl, jsonContent, cts.Token);
```
`jsonContent` never disposed. Must be `using var jsonContent`.
**Severity:** Major (leak under burst).

### CORE-16 — `GetLongPath` corrupts relative paths
**File:** `SimpleLauncher.Core/Services/CheckPaths/PathHelper.cs:348-361`
```csharp
if (path.StartsWith(@"\\", ...)) return @"\\?\UNC\" + path[2..];
return @"\\?\" + path;
```
Applied unconditionally — `GetLongPath("images\\default.png")` → `"\\\\?\\images\\default.png"` (invalid). Callers (`DeleteFilesService:34,99`, `WpfImageLoader:49`, `CheckPath:23,50`) get `File.Exists==false` and silently skip delete/load. Must only prefix fully-qualified absolute paths.
**Severity:** Major.

### CORE-17 — `ResolveRelativeToAppDirectory` allows `..` escape
**File:** `SimpleLauncher.Core/Services/CheckPaths/PathHelper.cs:206-224`
```csharp
if (path.StartsWith(BaseFolderPlaceholder, ...)) { basePath = AppDomain.CurrentDomain.BaseDirectory; remainingPath = path[...].TrimStart(...); }
else if (Path.IsPathRooted(path)) { basePath = ""; }
else { basePath = AppDomain.CurrentDomain.BaseDirectory; }
var combinedPath = Path.Combine(basePath, remainingPath);
return Path.GetFullPath(combinedPath);
```
`"%BASEFOLDER%/../../Windows/System32"` resolves outside app dir with no containment check, despite name promising app-relative.
**Severity:** Major.

### CORE-18 — Log-file path escapes data folder via config
**File:** `SimpleLauncher.Core/Services/CheckPaths/PathHelper.cs:244-250`
```csharp
return Path.Combine(AppDataPaths.SimpleLauncherDataFolder, logFileName);
```
If `LogPath` in config is absolute or `..\..\evil`, `Path.Combine` returns outside path and `BugReportApiSink:150,153` `AppendAllTextAsync` writes there. Must take `GetFileName` or validate containment.
**Severity:** Major.

### CORE-19 — `HttpResponseMessage` never disposed in resolver
**File:** `SimpleLauncher.Core/Services/ParameterResolver/ParameterResolverService.cs:48-83`
```csharp
response = await client.SendAsync(httpRequest);
responseBody = await response.Content.ReadAsStringAsync();
// both success (67) and error (78) paths return without disposing response
```
Each call leaks socket/stream until GC. Must be `using var response`.
**Severity:** Major.

### CORE-20 — Resolver has no timeout
**File:** `SimpleLauncher.Core/Services/ParameterResolver/ParameterResolverService.cs:52`
```csharp
response = await client.SendAsync(httpRequest);
```
No `CancellationToken`/`CancelAfter`. Black-holed API hangs launch flow forever.
**Severity:** Major.

### CORE-21 — Case-sensitive deserialization drops API fields
**File:** `SimpleLauncher.Core/Services/ParameterResolver/ParameterResolverService.cs:13-17`
```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{ PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
return JsonSerializer.Deserialize<ParameterResolverResult>(responseBody, JsonOptions);
```
No `PropertyNameCaseInsensitive = true` (contrast `EasyModeManager:19-22`). PascalCase responses deserialize to null with no error → silently wrong emulator parameters.
**Severity:** Major.

### CORE-22 — Static `SemaphoreSlim` held across 30s network I/O
**File:** `SimpleLauncher.Core/Services/EasyMode/EasyModeManager.cs:207-241`
```csharp
await CacheLock.WaitAsync();
try { /* ... */ var manager = await FetchFromApiAsync(); /* 30s GetAsync */ }
finally { CacheLock.Release(); }
```
`CacheLock` is `static`, never disposed. Every instance blocks on one global semaphore for full HTTP fetch, serializing startup.
**Severity:** Major.

### CORE-23 — Response stream never disposed
**File:** `SimpleLauncher.Core/Services/EasyMode/EasyModeManager.cs:258-260`
```csharp
var stream = await response.Content.ReadAsStreamAsync(cts.Token);
var systems = await JsonSerializer.DeserializeAsync<List<EasyModeSystemConfig>>(stream, JsonOptions, cts.Token);
```
Neither `stream` nor `response` disposed. Handler/socket leak per fetch.
**Severity:** Major.

### CORE-24 — `RetroAchievementsManager` NRE on null MessagePack data
**File:** `SimpleLauncher.Core/Services/RetroAchievements/RetroAchievementsManager.cs:40,73-85`
```csharp
manager.AllGames = MessagePackSerializer.Deserialize<List<RaGameInfo>>(bytes);
foreach (var game in AllGames) { foreach (var hash in game.Hashes) ... }
```
If `.dat` contains `nil` or entry has `Hashes==null` (version skew), `AllGames`/`game.Hashes` become null → NRE in `PopulateHashLookup` and `AllGames.Count` (`:58`). No null guard.
**Severity:** Major.

### CORE-25 — Hash store torn reads (non-atomic write, unlocked read)
**File:** `SimpleLauncher.Core/Services/RetroAchievements/RetroAchievementsHashStore.cs:71-90,95-118`
```csharp
public RaSystemHashes? LoadSystemHashes(...) // no lock
{ var json = File.ReadAllText(filePath); var data = JsonSerializer.Deserialize<RaSystemHashes>(json, ...); }
public void SaveSystemHashes(...) { lock (_fileLock) { File.WriteAllText(filePath, json); } }
```
Writer holds `_fileLock` during sync `WriteAllText`, readers don't lock, write isn't atomic (temp+move). Concurrent scan gets half-written JSON → `JsonException` → needless rescan.
**Severity:** Major.

### CORE-26 — `Ordinal` vs `OrdinalIgnoreCase` mismatch creates duplicate systems
**File:** `SimpleLauncher.Core/Services/SystemConfiguration/SystemConfigurationWriterService.cs:74-76,191-193,256-258`
```csharp
// Save/Delete:
.FirstOrDefault(el => string.Equals(el.Element("SystemName")?.Value, systemIdentifier, StringComparison.Ordinal));
// Exists:
.Any(el => string.Equals(..., systemName, StringComparison.OrdinalIgnoreCase))
```
`SystemExists("nes")` matches `"NES"`, but `SaveSystemAsync("nes")` won't find `"NES"` and adds second node. Duplicates accumulate.
**Severity:** Major (logic).

### CORE-27 — `XElement.Load` without DTD hardening (XXE)
**File:** `SimpleLauncher.Core/Services/SettingsManager/SettingsManagerService.cs:329`
```csharp
settings = XElement.Load(_fileLocation.FilePath);
```
No `XmlReaderSettings { DtdProcessing = Prohibit, XmlResolver = null }`, unlike `SystemConfigurationWriterService:177-181,248-252` and `EasyModeManager:180-184`. Malicious `settings.xml` with `<!ENTITY xxe SYSTEM ...>` expanded on load.
**Severity:** Major (security).

### CORE-28 — Extension normalization mismatch → empty game lists
**File:** `SimpleLauncher.Core/Services/GetListOfFiles/GetListOfFilesService.cs:46,87,50`
```csharp
var extensionsSet = new HashSet<string>(fileExtensions, StringComparer.OrdinalIgnoreCase);
var ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
if (extensions.Contains(ext)) results.Add(file);
var doRecurse = !(disableRecursiveSearch && !groupByFolder);
```
If config supplies `".zip"` (with dot), never equals stripped `"zip"` → zero results silently. Also `doRecurse` recurses when `disableRecursiveSearch==true && groupByFolder==true`, contradicting flag name.
**Severity:** Major.

### CORE-29 — `RomHistoryLoader` throws instead of falling back (TOCTOU)
**File:** `SimpleLauncher.Core/Services/RomHistory/RomHistoryLoader.cs:22-32,86-95`
```csharp
if (File.Exists(datFilePath)) return FindEntryFromDat(datFilePath, romName); // :24
private static XElement? FindEntryFromDat(...) // no try
{ var binaryData = File.ReadAllBytes(datFilePath); var history = MessagePackSerializer.Deserialize<HistoryData>(binaryData); }
using var reader = XmlReader.Create(historyFilePath, settings); // :94 no Exists check
```
Deleted-between-check or corrupt `.dat` throws to UI instead of falling back to XML; missing `.xml` throws `FileNotFoundException`. No `try/catch` at this layer.
**Severity:** Major.

### CORE-30 — Cover lookup path traversal + `!` null-deref in launch strategy
**Files:** `Services/FindCoverImage/FindCoverImageService.cs:67` + `Services/GameLauncher/Strategies/DefaultLaunchStrategy.cs:29-47`
```csharp
// FindCoverImageService.cs:67
var imagePath = Path.Combine(resolvedImageFolder, $"{fileNameWithoutExtension}{ext}");
// DefaultLaunchStrategy.cs:29
return launcher.RunBatchFileAsync(context.ResolvedFilePath, context.EmulatorManager!, context.WindowContext!);
// :43
context.SystemManagerService!, context.EmulatorManager!, context.WindowContext!
```
`fileNameWithoutExtension` from ROM filenames; `../../evil` escapes image folder. Separately `DefaultLaunchStrategy` force-unwraps three nullable `LaunchContext` members; any launch with null manager/context throws NRE.
**Severity:** Major.

### CORE-31 — Minor / worth noting (Core)
- `DiscConverter.cs:113-114,203-204,358-359` — duplicate `_logger.Error(ex,...)` twice per catch → double bug-report submissions.
- `WindowsCredentialProtector.cs:69,82-85` — `Unprotect` catches only `CryptographicException`; `Convert.FromBase64String` throws `FormatException` on corrupt settings → uncaught crash loading `RaApiKey/RaPassword/RaToken`.
- `CleanSimpleLauncherFolderService.cs:249-257,268-276` — `throw new ArgumentOutOfRangeException` for non-X64/Arm64 (e.g. X86) aborts `CleanupTrash()` at startup; should no-op.
- `RetroAchievementsHasherTool.cs:139-162,209-212` — `loadingState?.SetLoadingState(true)` at `:139` but early returns never reset → stuck spinner; only hashing `finally` resets.
- `MountXisoFiles.cs:114` / `MountChdFiles.cs:119` — `catch { if (!mountProcess.HasExited)... }`: when `Start()` throws, `HasExited` throws `InvalidOperationException`, masking original error.
- `ExternalToolLauncherService.cs:234-247` — `Process.Start(psi)` return ignored with `UseShellExecute=true`; leaked `Process` handle. Should `using`.
- `FindCoverImageService.cs:224` — `matchDistance = Max(len1,len2)/2 - 1` is `-1` for 1-char names → identical `"a"/"a"` scores `0.0`; off-by-one for short ROM names.
**Severity:** Minor–Major.

---
---

# Part 2 — SimpleLauncher (WPF frontend)

## High

### WPF-01 — `MainWindow.Dispose` never unsubscribes — window leaked by singletons
**File:** `SimpleLauncher/MainWindow.CloseWindowEvents.cs:17-73` vs `122-150`
```csharp
public void Dispose()
{
    _isDisposed = true;
    _cancellationSource?.Cancel();
    GameListItems?.Clear(); // no -= for _gameFilesChangedHandler/_gamePlayedHandler
}
private void UnsubscribeEventHandlers() // only called from Closing 2nd pass:99-102
{
    _lifecycle.UnsubscribeGameFilesChanged(_gameFilesChangedHandler);
    _gameLauncherService.GamePlayed -= _gamePlayedHandler;
}
```
If `Dispose()` runs without deferred `Closing` path (DI teardown, `App.OnExit`), singletons keep `MainWindow` alive.
**Severity:** High (leak).

### WPF-02 — Sync-over-async `LoadSystemManagers` + sync `Dispatcher.Invoke` message box = deadlock
**Files:** `SimpleLauncher/Services/SystemManager/SystemManagerService.cs:152-154` + `SimpleLauncher/Services/WpfServices/WpfMessageDialogService.cs:78-79`
```csharp
return Task.Run(() => LoadSystemManagersInternalAsync(...)).GetAwaiter().GetResult();
var wpfResult = Application.Current.Dispatcher.Invoke(() =>
    System.Windows.MessageBox.Show(...));
```
Called from UI thread (`MainWindow.xaml.cs:199` in ctor). Corrupt/locked `system.xml` makes pool thread block on `Dispatcher.Invoke` while UI thread blocks on `GetResult()`.
**Severity:** High (deadlock on startup).

### WPF-03 — `async void OnGameFilesChangedAsync` touches UI from watcher thread, no coalescing
**File:** `SimpleLauncher/Services/GameFileLoadingOrchestrator/GameFileLoadingOrchestratorService.cs:201-217`
```csharp
public async void OnGameFilesChangedAsync(string systemName)
{
    var currentSystem = _host.SystemComboBox.SelectedItem?.ToString(); // off-UI thread
    await InvalidateGameFileCachesAsync();
    await LoadGameFilesAsync(cancellationToken: CancellationToken.None); // uncancelable, races nav
}
```
Caller discards it (`MainWindow.xaml.cs:215`, `GameBrowserService.cs:204-206` is `void`). Raised from `Task.Run` debounce → WPF threading violation; overlapping reloads fight pagination/search. Exceptions only `Debug`-logged.
**Severity:** High.

### WPF-04 — Unvalidated `Process.Start(UseShellExecute=true)` on hyperlink/URL — arbitrary scheme launch
**Files:**
- `SimpleLauncher/EasyModeWindow.xaml.cs:1379-1382` `Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri){UseShellExecute=true});`
- `SimpleLauncher/RomHistoryWindow.xaml.cs:64-68`
- `SimpleLauncher/RetroAchievementsWindow.xaml.cs:291-296` `Process.Start(new ProcessStartInfo(url){UseShellExecute=true});`
- `SimpleLauncher/ViewModels/DownloadImagePackViewModel.cs:544`
- `SimpleLauncher/EditSystemWindow.xaml.cs:515-519` `FileName = searchUrl` from `WikiParametersUrl` config
```csharp
Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri){UseShellExecute=true});
```
No `http(s)` allowlist. `file://`, `ms-msdt:`, `search-ms:` URIs execute shell handlers.
**Severity:** High (security).

### WPF-05 — `ContextMenuFunctions` builds shell URL from editable setting without validation
**File:** `SimpleLauncher/Services/ContextMenu/ContextMenuFunctions.cs:214-222,267-275`
```csharp
var searchUrl = $"{settings.VideoUrl}{Uri.EscapeDataString($"{searchTerm} {systemName}")}";
Process.Start(new ProcessStartInfo{ FileName = searchUrl, UseShellExecute = true });
```
If `VideoUrl`/`InfoUrl` tampered, `FileName` can be `C:\evil.exe` + args. Suffix escaped, prefix not validated.
**Severity:** High.

### WPF-06 — System-image destination path traversal via raw `SystemNameTextBox`
**File:** `SimpleLauncher/EditSystemWindow.xaml.cs:616,669`
```csharp
var systemName = SystemNameTextBox.Text.Trim(); // unsanitized
var destFilePath = Path.Combine(imagesSystemsDir, $"{systemName}{extension}");
File.Copy(sourceFilePath, destFilePath, true);
```
`..\`, `/`, `:`, reserved names (`CON`), overlong names cause overwrite outside `images/systems` or unhandled `IOException/ArgumentException`. No `GetInvalidFileNameChars` check.
**Severity:** High.

### WPF-07 — Dedup by `GetFileName` silently drops distinct ROMs
**File:** `SimpleLauncher/Services/GameFileLoadingOrchestrator/GameFileLoadingOrchestratorService.cs:356,385`
```csharp
foreach (var file in filesInFolder) uniqueFiles.TryAdd(Path.GetFileName(file), file);
```
Key is filename only. Two folders containing `game.zip` (different dumps) → second lost. Duplicated in both cache-miss branches.
**Severity:** High (data loss/logic).

### WPF-08 — `EasyModeWindow.Dispose` disposes injected DI singletons
**File:** `SimpleLauncher/EasyModeWindow.xaml.cs:277-287`
```csharp
_downloadManager.DownloadProgressChanged -= DownloadManager_ProgressChanged;
_downloadManager?.Dispose();
_manager?.Dispose();
_easyModeManager?.Dispose();
```
`_downloadManager`/`_easyModeManager` come from DI ctor (`:60-74`). Second open of EasyMode throws `ObjectDisposedException`.
**Severity:** High.

### WPF-09 — `RetroAchievementsWindow` fire-and-forget loads leave overlay stuck
**File:** `SimpleLauncher/RetroAchievementsWindow.xaml.cs:104-117,139-189`
```csharp
_ = LoadUserProfileAsync(); // TabControl_SelectionChanged + Loaded:131
private async Task LoadUserProfileAsync()
{
    SetLoadingState(true);
    await _viewModel.LoadUserProfileAsync(); // no try/catch
    SetLoadingState(false); // unreachable on throw
}
```
No `try`, no CTS. Rapid tab switches overlap; failure leaves `LoadingOverlay` visible forever + unobserved exception.
**Severity:** High.

## Medium

### WPF-10 — Unawaited `OpenUrlInBrowserAsync` Task from click handler
**File:** `SimpleLauncher/RetroAchievementsWindow.xaml.cs:291-309`
```csharp
private async Task OpenUrlInBrowserAsync(string url){ ... await _messageBox...; }
private void ViewProfileOnRaButton_Click(...){ ... OpenUrlInBrowserAsync(url); } // not awaited
```
Returned `Task` dropped; `Process.Start` failure becomes unobserved.
**Severity:** Medium.

### WPF-11 — `CancelAndRecreateToken` leaks every old CTS handle
**File:** `SimpleLauncher/MainWindow.xaml.cs:564-582`
```csharp
var oldCts = Interlocked.Exchange(ref _cancellationSource, new CancellationTokenSource());
try { oldCts.Cancel(); } catch(ObjectDisposedException){}
// never oldCts.Dispose()
```
Every search/pagination/sort leaks CTS kernel timer. High-frequency nav → handle buildup.
**Severity:** Medium-High.

### WPF-12 — `EasyModeWindow.OnPropertyChanged` fires on pool thread
**File:** `SimpleLauncher/EasyModeWindow.xaml.cs:368-371`
```csharp
private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
{
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); // no Dispatcher check
}
```
vs `MainWindow.xaml.cs:343-349` which correctly marshals. Values set from download continuations (`:575-833`) on background threads → cross-thread binding exception.
**Severity:** Medium-High.

### WPF-13 — `MainWindow_Closing` deferred `async void` can strand window
**File:** `SimpleLauncher/MainWindow.CloseWindowEvents.cs:75-108`
```csharp
private async void MainWindow_Closing(object? sender, CancelEventArgs e)
{
    if (!_isCloseSaveDeferred){ e.Cancel = true; _isCloseSaveDeferred = true;
        try{ await SaveApplicationSettings(); } catch(...){}
        Close(); return; }
```
If `SaveApplicationSettings()` never returns or `Close()` throws, window never closes, no timeout/retry.
**Severity:** Medium.

### WPF-14 — `FilterByLetter("#")` can throw `IndexOutOfRangeException`
**File:** `SimpleLauncher/Services/GameFilter/GameFilterService.cs:83-88`
```csharp
return files.Where(static file => !string.IsNullOrEmpty(file) &&
    file.Length > 0 &&
    char.IsDigit(Path.GetFileName(file)[0])).ToList();
```
Guards `file.Length`, not `Path.GetFileName(file).Length`. `file = "C:\\roms\\"` → filename `""` → `[0]` throws.
**Severity:** Medium.

### WPF-15 — `Search` `finally` prematurely hides concurrent load overlay
**File:** `SimpleLauncher/MainWindow.Search.cs:62-112`
```csharp
private async Task ExecuteSearchAsync()
{
    if (_isLoadingGames) return;
    SetLoadingState(true, searchingMsg);
    try{ CancelAndRecreateToken(); ... await LoadGameFilesAsync(...); }
    finally{ SetLoadingState(false); }
}
```
Concurrent pagination started after `CancelAndRecreateToken` gets overlay cleared by stale search.
**Severity:** Medium (logic).

### WPF-16 — `RefreshGameListAfterPlay` sync `Dispatcher.Invoke` + possible NRE
**File:** `SimpleLauncher/MainWindow.xaml.cs:774-813`
```csharp
Dispatcher.Invoke(() => {
    var gameItem = GameListItems.FirstOrDefault(item =>
        item.FilePath.Equals(fileName, ...)); // FilePath nullable for placeholder
    GameDataGrid.Items.Refresh(); // redundant, throws if mid-update
});
```
`GamePlayed` fires on launcher bg thread; sync `Invoke` blocks it. Null `FilePath` → NRE. `Items.Refresh()` during render → `InvalidOperationException`.
**Severity:** Medium.

### WPF-17 — Sync `Dispatcher.Invoke` `SetLoadingState` everywhere — deadlock on reentrancy/dispose
**Files:** `SimpleLauncher/SupportWindow.xaml.cs:76`, `EasyModeWindow.xaml.cs:296`, `RetroAchievementsWindow.xaml.cs:81`, `UpdateLogWindow.xaml.cs:31`, `GameFileLoadingOrchestratorService.cs:186`
```csharp
Dispatcher.Invoke(() => { LoadingOverlay.Visibility = ...; });
_host.Dispatcher.Invoke(() => _host.SetLoadingState(false));
```
Called from handlers that may already be on UI thread and from `finally` during close → hang or `TaskCanceledException`/`InvalidOperationException` when host disposed. Should be `InvokeAsync`/`BeginInvoke` with disposed guard.
**Severity:** Medium.

### WPF-18 — `DebugWindow` accumulates handlers on singleton ViewModel + locks around `Show()`
**File:** `SimpleLauncher/DebugWindow.xaml.cs:63-84`
```csharp
viewModel.PropertyChanged += logTextPropertyChangedHandler; // viewModel is DI singleton:51
lock(InstanceLock){ ... Instance.Show(); } // pumps messages while holding lock
```
Hide-on-close (`OnClosing:127-137`) never unsubscribes; each recreate adds handler → N× `BeginInvoke(ScrollToEnd)` storm + old windows rooted. `Show()` inside lock risks reentrancy deadlock.
**Severity:** Medium.

### WPF-19 — `ToastNotificationWindow` drops bg-thread toasts + leaks animation handler
**File:** `SimpleLauncher/Services/NotificationToast/ToastNotificationWindow.xaml.cs:53,75-81,42`
```csharp
if (_isDisposed || !Application.Current.Dispatcher.CheckAccess()) return; // silent drop, no marshal
var fadeOut = new DoubleAnimation(...); fadeOut.Completed += (_, _) => Hide(); // never -=
public void Dispose(){ _dismissTimer.Tick -= ...; Close(); }
```
Bg-thread toasts lost. Each tick creates new `Completed` closure. `Close()` without state guard can throw if already closing.
**Severity:** Medium.

### WPF-20 — `FlashOverlayWindow` anonymous `CloseRequested` never removed + CTS dispose race
**File:** `SimpleLauncher/FlashOverlayWindow.xaml.cs:24,28-42,50-73`
```csharp
_viewModel.CloseRequested += (_, _) => Close(); // anonymous, never -=
Closing += (_, _) => { _cts?.Cancel(); Dispose(); };
public void Dispose(){ _cts?.Dispose(); _cts = null; }
await Task.Delay(600, _cts.Token); catch(OperationCanceledException){return;}
```
ViewModel outlives window → leak. `Dispose` racing in-flight `Delay` throws `ObjectDisposedException` unhandled. Repeated `ShowFlashAsync` overwrites `_cts` without disposing old.
**Severity:** Medium.

### WPF-21 — `GlobalStats` double-dispose / `ClosingCommand` race
**Files:** `SimpleLauncher/GlobalStatsWindow.xaml.cs:49-87` + `ViewModels/GlobalStatsViewModel.cs:75-79,418-451`
```csharp
public void Dispose(){ _cancellationTokenSource?.Dispose(); ... } // no guard, no nulling
// Closing:87 Dispose(); + ClosingCommand.Execute(e) may also Dispose():428
private async Task ClosingAsync(CancelEventArgs? e){ lock(_processingLock){ if(!IsProcessing){Dispose(); return;}} e!.Cancel=true; ...}
```
`Closing` calls both `ClosingCommand.Execute(e)` (fire-and-forget) and `Dispose()` synchronously → second `Dispose` throws. `e!` null-forgiving → NRE if invoked with null. `IsProcessing` set outside `_processingLock` → TOCTOU.
**Severity:** Medium.

### WPF-22 — `ImageViewer` fire-and-forget + sync web `BitmapImage` on UI thread
**Files:** `SimpleLauncher/ImageViewerWindow.xaml.cs:31` + `ViewModels/ImageViewerViewModel.cs:52,87`
```csharp
_ = _viewModel.LoadImageFromPathAsync(imagePath); // string? passed, imagePath! inside
var imageData = await File.ReadAllBytesAsync(imagePath!); // ArgumentNullException if null
var bitmap = new BitmapImage(imageUri); bitmap.Freeze(); // sync web download on UI thread
```
Null path relies on catch+dialog. Remote URI blocks UI with no timeout/`CacheOption`.
**Severity:** Medium.

### WPF-23 — `App.ShowStartupFailureAndShutdown` `async void` may `Shutdown` before dialog shows
**File:** `SimpleLauncher/App.xaml.cs:729-749` called at `:603,610`
```csharp
private async void ShowStartupFailureAndShutdown(IMessageBoxLibraryService messageBox)
{
    await messageBox.FailedToStartSimpleLauncherMessageBoxAsync();
    _singleInstanceMutex?.Dispose(); Shutdown();
}
```
Fire-and-forget from `OnStartup`; `Shutdown()` can race dialog. Adjacent: `OnExit:804-807 CancelScanAndWaitAsync(...).GetAwaiter().GetResult()` blocks exit up to 10s even if scanner needs dispatcher.
**Severity:** Medium.

### WPF-24 — `-whatsnew` `BeginInvoke(ShowDialog)` + narrow catch crashes shutdown
**File:** `SimpleLauncher/App.xaml.cs:690-704`
```csharp
Dispatcher.BeginInvoke(new Action(static () => {
    try{ ServiceProvider.GetRequiredService<UpdateHistoryWindow>().ShowDialog(); }
    catch(SystemException ex){ ... } // misses InvalidOperationException/ObjectDisposedException
}));
```
If shutdown disposes provider before invoke runs, non-`SystemException` escapes dispatcher → crash.
**Severity:** Medium.

### WPF-25 — `RomHistoryWindow` re-adds hyperlink handler per `Loaded`, swallows failures
**File:** `SimpleLauncher/RomHistoryWindow.xaml.cs:29-44,60-76`
```csharp
Loaded += (_, _) => HistoryMarkdownViewer.AddHandler(Hyperlink.RequestNavigateEvent, _requestNavigateHandler);
Closed += (_, _) => HistoryMarkdownViewer.RemoveHandler(...);
catch(Exception ex){ _logger.Debug($"Failed to open link: {e.Uri}..."); }
e.Handled = true; // even on failure, no user feedback
```
Multiple `Loaded` → duplicate invokes → N× `Process.Start` per click. Failure only `Debug`-logged.
**Severity:** Low-Medium.

---
---

# Part 3 — SimpleLauncher.Avalonia (cross-platform frontend)

## Critical / High

### AV-01 — `ImageViewerViewModel.LoadImageFromPathAsync` — `!` on nullable + 2× buffering + leak
**File:** `SimpleLauncher.Avalonia/ViewModels/ImageViewerViewModel.cs:49-53`
```csharp
public async Task LoadImageFromPathAsync(string? imagePath)
{
    var imageData = await File.ReadAllBytesAsync(imagePath!);
    await using var ms = new MemoryStream(imageData);
    var bitmap = Bitmap.DecodeToWidth(ms, 1200);
    ImageSource = bitmap; // old ImageSource never disposed
```
Null → `ArgumentNullException`. Full file into `byte[]` then decode = 2× memory. Previous `Bitmap` (unmanaged) leaked on every load.
**Severity:** High.

### AV-02 — `ImageViewerWindow.Dispose` disposes `Bitmap` still set as `Image.Source`
**File:** `SimpleLauncher.Avalonia/ImageViewerWindow.axaml.cs:30-33`
```csharp
public void Dispose()
{
    _viewModel.ImageSource?.Dispose();
```
View still references disposed `Bitmap` → `ObjectDisposedException` on next render. Never sets `ImageSource=null` / `Image.Source=null`. `Closing += (_,_)=>Dispose()` never unsubscribed.
**Severity:** High.

### AV-03 — `PathToImageConverter` — sync I/O on UI thread, eviction leak, case bug on Linux
**File:** `SimpleLauncher.Avalonia/Converters/PathToImageConverter.cs:37-60,80-111,141-170`
```csharp
public object? Convert(...) // runs on UI thread
{
    var image = LoadImage(path); // File.Exists + File.OpenRead + Bitmap.DecodeToWidth(stream,300)
```
```csharp
var oldest = LruList.Last!;
LruIndex.Remove(oldest.Value.Path); // Bitmap never Dispose()d
public static void ClearCache() { WeakCache.Clear(); lock(LruLock){LruList.Clear(); LruIndex.Clear();} }
private static readonly ConcurrentDictionary<string,WeakReference<Bitmap>> WeakCache =
    new(StringComparer.OrdinalIgnoreCase);
```
Every new cover blocks UI. 1500-entry LRU pins up to 1500 `Bitmap`s; eviction/`ClearCache()` leaks native memory. `OrdinalIgnoreCase` collides `Game.PNG` vs `game.png` on case-sensitive Linux.
**Severity:** High.

### AV-04 — `RemoteImageLoader` — static `HttpClient`, unbounded `Clear()` without `Dispose`
**File:** `SimpleLauncher.Avalonia/Services/RemoteImageLoader.cs:13-39`
```csharp
private static readonly HttpClient Http = new(); // no Timeout, never disposed
lock(CacheLock){ if(Cache.Count>=MaxCacheEntries) Cache.Clear(); Cache[url]=bitmap; }
// 512 Bitmaps dropped without Dispose()
```
`GetByteArrayAsync(url)` has infinite timeout, no cancellation. Mass eviction leaks all native images.
**Severity:** High.

### AV-05 — `RemoteImage` — fire-and-forget load without cancellation, old `Source` leaked
**File:** `SimpleLauncher.Avalonia/Controls/RemoteImage.cs:35-55`
```csharp
private void OnUrlChanged(AvaloniaPropertyChangedEventArgs e)
{
    Source = RemoteImageLoader.GetCached(url);
    _ = LoadAsync(url); // unawaited, no CTS
}
private async Task LoadAsync(string url)
{
    var bitmap = await RemoteImageLoader.LoadAsync(url);
    await Dispatcher.UIThread.InvokeAsync(()=>{ if(string.Equals(Url,url,...)) Source=bitmap; });
}
```
Fast scrolling spawns N concurrent HTTP loads; losers allocate discarded `Bitmap`s. If dispatcher shut down, `InvokeAsync` throws unobserved. Previous `Source` never disposed.
**Severity:** High.

### AV-06 — `EasyModeWindow` `PropertyChanged` touches UI off UI thread
**File:** `SimpleLauncher.Avalonia/EasyModeWindow.axaml.cs:38-46`
```csharp
_onViewModelPropertyChanged = (_, args) =>
{
    if(string.Equals(args.PropertyName, nameof(EasyModeViewModel.IsLoading), ...))
    {
        LoadingOverlay.IsVisible = _viewModel.IsLoading; // no Dispatcher check
    }
};
```
If `IsLoading` set from background task, Avalonia throws `InvalidOperationException`.
**Severity:** High.

### AV-07 — Unawaited `ShowDialog(this)` — fire-and-forget modals everywhere
**Files:**
- `MainWindow.axaml.cs:570-575` `OpenInjectWindow<T>`: `win.ShowDialog(this);`
- `MainWindow.axaml.cs:2027,2069,2082,2095,2109,2122,2135` (`EditLinks`, `SetFuzzyMatching`, `SoundConfiguration`, `DownloadImagePack`, `GlobalStats`, `About`, `Support`)
- `MainWindow.axaml.cs:1457` `OpenGameDetail`: `window.ShowDialog(this);`
- `GameDetailWindow.axaml.cs:115` `viewer.ShowDialog(this);`
- `PreferencesWindow.axaml.cs:109,116,122`
- `AboutWindow.axaml.cs:21-25`
```csharp
private void OpenInjectWindow<T>(Action<T> initialize) where T : Window
{
    var win = App.ServiceProvider.GetRequiredService<T>();
    initialize(win);
    win.ShowDialog(this); // Task<object?> discarded
}
```
Avalonia `ShowDialog` must be awaited; faults/cancellations unobserved, owner can close before dialog completes.
**Severity:** High.

### AV-08 — `DownloadImagePackWindow` `async void Closing` cannot delay close
**File:** `SimpleLauncher.Avalonia/DownloadImagePackWindow.axaml.cs:61-75`
```csharp
private async void CloseWindowRoutineAsync(object? sender, WindowClosingEventArgs e)
{
    try { await _viewModel.CloseWindowRoutineAsync(); }
    finally { Dispose(); } // races with in-flight save/cancel
}
```
`Closing` has no deferral; window closes before routine finishes. `Dispose()` can dispose `DownloadManager`/scope while routine still uses them.
**Severity:** High.

### AV-09 — `GlobalStatsWindow_Closing` re-entrant `Close()` double-prompts
**File:** `SimpleLauncher.Avalonia/GlobalStatsWindow.axaml.cs:63-80`
```csharp
private async void GlobalStatsWindow_Closing(object? sender, WindowClosingEventArgs e)
{
    if(_viewModel.IsProcessing)
    {
        e.Cancel = true;
        var allowClose = await _viewModel.RequestCloseAsync();
        if(allowClose) Close(); // re-enters Closing; IsProcessing still true -> prompts again
    }
}
```
No `_forceClose` guard. Second `Closing` re-prompts.
**Severity:** High (logic).

### AV-10 — `GlobalStatsViewModel` CTS disposed while in use; NRE; dead null-check
**File:** `SimpleLauncher.Avalonia/ViewModels/GlobalStatsViewModel.cs:71-75,152-155,307,341`
```csharp
public void Dispose(){ _cancellationTokenSource?.Dispose(); } // may be running in StartAsync
finally{ IsProcessing=false; _cancellationTokenSource?.Dispose(); _cancellationTokenSource=null; }
TotalEmulators = _systemManagers.Sum(static c => c.Emulators.Count), // NRE if Emulators==null
private async Task SaveReportAsync(){ if(_globalStats==null) return; // dead: struct new()
```
`Dispose()` + `finally` race → `ObjectDisposedException`. Empty report can be saved when stats never ran.
**Severity:** High.

### AV-11 — `App.Dispose` blocking `GetResult()` + fire-and-forget `StopAsync()`
**File:** `SimpleLauncher.Avalonia/App.axaml.cs:119-140`
```csharp
ServiceProvider?.GetService<IRetroAchievementsHashScanner>()
    ?.CancelScanAndWaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); // blocks Exit thread
if(gamePadController is not null){ _ = gamePadController.StopAsync(); gamePadController.Dispose(); }
```
`GetResult()` on `Exit` (UI) thread deadlocks if scan needs dispatcher. `_ = StopAsync()` returns immediately; `Dispose()` destroys controller while stop in flight.
**Severity:** High.

### AV-12 — Shutdown watchdog `Environment.Exit(0)` truncates clean shutdown
**File:** `SimpleLauncher.Avalonia/MainWindow.axaml.cs:197-237`
```csharp
Closed += (_,_)=>{
    _ = Task.Delay(TimeSpan.FromSeconds(5),token).ContinueWith(_=>{
        Log.Warning("Shutdown watchdog fired after window close; forcing process exit");
        Environment.Exit(0);
    },token);
};
```
Fires 5s after close even on healthy shutdown, killing 10s RA-scan drain in `App.Dispose` and in-flight `SaveAsync`, orphaning CLI hash processes.
**Severity:** High.

### AV-13 — `EditSystemWindow` unsanitized `SystemName` → traversal
**File:** `SimpleLauncher.Avalonia/EditSystemWindow.axaml.cs:707-712,758-765`
```csharp
var imagesSystemsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"images","systems");
var destFilePath = Path.Combine(imagesSystemsDir, $"{systemName}{extension}"); // systemName = TextBox.Text
File.Copy(sourceFilePath, destFilePath, true);
```
`../../evil`, `/`, `:` never sanitized → writes outside `images/systems`. Same at `:257,260`: `Path.Combine(".","roms",SystemNameTextBox.Text)`.
**Severity:** High (security).

### AV-14 — `ShowGameInFolderAsync` unescaped quote injection into `explorer.exe`
**File:** `SimpleLauncher.Avalonia/MainWindow.axaml.cs:1390-1395`
```csharp
Process.Start(new ProcessStartInfo{
    FileName="explorer.exe",
    Arguments=$"/select,\"{game.FilePath}\"",
    UseShellExecute=true });
```
`"` in `FilePath` breaks quoting and injects extra `explorer` args.
**Severity:** High.

### AV-15 — `Process.Start+UseShellExecute` for URLs/folders — Windows assumption, no scheme check
**Files:** `MainWindow.axaml.cs:2805,2824`, `EditSystemWindow.axaml.cs:802-806`, `RetroAchievementsWindow.axaml.cs:251`, `RetroAchievementsForAGameWindow.axaml.cs:254`, `RetroAchievementsSettingsWindow.axaml.cs:72`
```csharp
Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
Process.Start(new ProcessStartInfo{FileName=url,UseShellExecute=true})
Process.Start(new ProcessStartInfo{FileName=appDataPath,UseShellExecute=true})
```
`ShowGameInFolderAsync` correctly uses `TopLevel.Launcher` on non-Windows, but `Donate`/`OpenAppData` do not — throws on Linux. Config-controlled URLs executed without scheme validation.
**Severity:** High (security + cross-platform).

## Medium

### AV-16 — `ConsoleToCardHeightConverter` mishandles `int`/`UnsetValue`
**File:** `SimpleLauncher.Avalonia/Converters/ConsoleToCardHeightConverter.cs:16-28`
```csharp
var cardWidth = values[0] as double? ?? 168.0; // int, float, UnsetValue -> 168.0
var systemName = values[1] as string ?? "";
```
Silently returns wrong height instead of `UnsetValue`/`DoNothing`. Static `_ratioService` read/written without lock.
**Severity:** Medium.

### AV-17 — `BoolToVisibility` / `InverseBool*` converters swallow binding errors
**Files:** `Converters/BoolToVisibilityConverter.cs:11-14`, `InverseBoolConverter.cs:11-14`, `InverseBoolToVisibilityConverter.cs:11-14`, `NullToVisibilityConverter.cs:11-14`
```csharp
public object Convert(object? value,...){ return value is true; }
```
Never returns `AvaloniaProperty.UnsetValue`; broken binding silently collapses UI instead of surfacing diagnostics.
**Severity:** Medium.

### AV-18 — `DebugViewModel` O(n²) log rebuild on every line
**File:** `SimpleLauncher.Avalonia/ViewModels/DebugViewModel.cs:47-66,71-91`
```csharp
LogMessages.Add(formattedMessage);
while(LogMessages.Count>MaxMessageCount) LogMessages.RemoveAt(0); // O(n) shift
LogText = string.Join(Environment.NewLine, LogMessages)+Environment.NewLine; // up to 5000 joins per line
```
Verbose logging freezes UI; 5000-item `ObservableCollection` + full-string rebuild per append.
**Severity:** Medium (perf).

### AV-19 — `AvaloniaGameCacheService.GetCachedOrScan` holds lock across disk I/O
**File:** `SimpleLauncher.Avalonia/Services/AvaloniaGameCacheService.cs:75-86`
```csharp
lock(_lock){
    if(_cache.TryGetValue(...)) return [..cached];
    var files = enumerateFiles(system).ToList(); // disk enumeration under lock
```
Blocks every reader during scan; deadlock if enumerator re-enters cache.
**Severity:** Medium.

### AV-20 — `SetGamepadDeadZone_Click` `ContinueWith` + unawaited restarts
**File:** `SimpleLauncher.Avalonia/MainWindow.axaml.cs:2040-2056`
```csharp
window.ShowDialog(this).ContinueWith(_=>{
    _gamePadController.DeadZoneX=_settings.DeadZoneX; ...
    if(_settings.EnableGamePadNavigation){ _gamePadController.StopAsync(); _gamePadController.StartAsync(); }
    else{ _gamePadController.StopAsync(); }
},TaskScheduler.FromCurrentSynchronizationContext());
```
Runs even if dialog faulted/cancelled; overlapping `Stop/Start` races; uses `_settings`/`_gamePadController` after disposal.
**Severity:** Medium.

### AV-21 — RetroAchievements tabs overlapping fire-and-forget loads, UI update after close
**Files:** `RetroAchievementsWindow.axaml.cs:61-90,266-298`, `RetroAchievementsForAGameWindow.axaml.cs:99-140,335-379`
```csharp
case "MyProfile": _playSoundEffects.PlayNotificationSound(); _ = LoadUserProfileAsync(); break;
private async void OpenUrlInBrowserAsync(string url)
```
Rapid tab switches overlap; slower earlier load overwrites current tab. Sets `Text/ItemsSource/IsVisible` after `await` without `IsLoaded`/disposal check → `ObjectDisposedException` if closed mid-load.
**Severity:** Medium.

### AV-22 — `FlashOverlayWindow.ShowFlashAsync` CTS leak, `Show()` before layout, dispose race
**File:** `SimpleLauncher.Avalonia/FlashOverlayWindow.axaml.cs:32-35,53-92`
```csharp
public async Task ShowFlashAsync(){
    _cts = new CancellationTokenSource(); // previous _cts never disposed
    Show(); // positioned afterwards
    ...
    await animation.RunAsync(FlashRectangle,_cts.Token);
}
```
`Closed` does `_cts?.Cancel(); Dispose();` while `RunAsync` awaits token → race/`ObjectDisposedException`.
**Severity:** Medium.

### AV-23 — Toast dismissal after close — unobserved `InvokeAsync` on dead visual
**File:** `SimpleLauncher.Avalonia/MainWindow.axaml.cs:2889,2897-2905`
```csharp
_ = DismissToastAsync(toast);
private async Task DismissToastAsync(Border toast){
    await Task.Delay(5000);
    await Dispatcher.UIThread.InvokeAsync(()=>{ ToastStack.Children.Remove(toast); ... }); // no try/catch
}
```
If `MainWindow` closes within 5s, continuation touches detached `ToastStack` → unobserved exception.
**Severity:** Medium.

### AV-24 — Concurrent fire-and-forget `SaveAsync` — lost writes / torn `settings.xml`
**Files:** `MainWindow.axaml.cs:434,447,1242,1882` (`_ = _settings.SaveAsync()`), `PreferencesWindow.axaml.cs:85,202,228`
```csharp
Closed += (_,_)=> SavePreferences(); // SavePreferences does _ = _settings.SaveAsync();
```
Multiple overlapping saves race; last-writer-wins loses toggles; exit can truncate write.
**Severity:** Medium.

### AV-25 — `UpdateLogWindow.Log` fire-and-forget `InvokeAsync`, burst ordering
**File:** `SimpleLauncher.Avalonia/UpdateLogWindow.axaml.cs:30-33`
```csharp
public void Log(string message){
    Dispatcher.UIThread.InvokeAsync(()=> _viewModel.AppendLog(message)); // Task discarded
}
```
Called from `AvaloniaCheckForUpdatesService.ShowUpdateWindowAsync` on bg threads in burst; failures (closed) unobserved, order not guaranteed. Should return `Task` and be awaited.
**Severity:** Medium.

---
---

# Part 4 — Updaters (`SimpleLauncher.Updater` + `SimpleLauncher.Avalonia.Updater`)

> Trees are ~95% duplicated. Unless noted, bug exists identically in both. No hash/signature code exists in either tree (grep `hash|sha256|signature|Verify` → 0 hits).

## Critical / High

### UPD-01 — No hash / signature verification — tampered zip installed silently
**Files:** `Services/UpdateService.cs:156-186` (WPF) / `:154-185` (Avalonia), `Services/DownloadService.cs:43-128` / `:41-126`, `Services/GitHubService.cs:66-86,152-171,203-242`
```csharp
updateFileStream = await DownloadWithFallbackAsync(assetUrl, fallbackAssetUrl, cancellationToken);
_zipService.IgnoredFiles = ignoredFiles;
await _zipService.ExtractFromStreamAsync(updateFileStream, cancellationToken);
```
No `SHA256`/checksum/signature/size check anywhere. Compromised GitHub account, compromised `assets.purelogiccode.com`, or MITM with valid CA → arbitrary file write to app dir + auto-restart = silent RCE. Same root cause in `DokanService` MSI path.
**Severity:** Critical.

### UPD-02 — Downgrade attack — updater never compares versions
**Files:** `Services/UpdateService.cs:91-210` (both)
`ExecuteUpdateAsync(processId, ignoredFiles, ct)` installs unconditionally from `GetLatestReleaseAssetUrlAsync()`. No `CurrentVersion`, no `Version` comparison. Contrast main apps: `SimpleLauncher/Services/CheckForUpdatesService.cs:112` and `AvaloniaCheckForUpdatesService.cs:153` both gate on `IsNewVersionAvailable()`. Rolling back `version.txt`, re-tagging release, or double-clicking `Updater.exe` gives silent downgrade.
**Severity:** High.

### UPD-03 — ZipSlip prefix bypass — `StartsWith` without separator
**Files:** `SimpleLauncher.Updater/Services/ZipService.cs:88-95`, `SimpleLauncher.Avalonia.Updater/Services/ZipService.cs:87-94`
```csharp
var trimmedEntry = entryKey.TrimStart('/', '\\');
var destinationPath = Path.GetFullPath(Path.Combine(_appDirectory, trimmedEntry));
var appDirectoryFullPath = Path.GetFullPath(_appDirectory);
if (!destinationPath.StartsWith(appDirectoryFullPath, StringComparison.OrdinalIgnoreCase))
    throw new SecurityException($"Zip entry attempts to escape target directory: {entryKey}");
```
If `_appDirectory = C:\App`, entry `..\AppEvil\pwn.exe` → `C:\AppEvil\pwn.exe` which **does** `StartsWith("C:\\App")` → passes, escapes. Need trailing separator or `Path.GetRelativePath` check. Identical in both.
**Severity:** High.

### UPD-04 — No symlink / reparse / ADS / hardlink defense + in-place symlink follow
**Files:** same `ZipService.cs` extraction blocks; grep `Symlink|Reparse|AlternateData|UnixFileMode|FileOptions` → 0 hits (both).
No link-flag check, no `ReparsePoint` check, no ADS stripping, no `UnixFileMode` clearing on Linux. Multi-pass extraction over live dir: pass N can drop symlink/junction, pass N+1 `FileMode.Create` follows it and writes outside `AppDirectory` even if second entry's own `StartsWith` passed. Also overwrites previous-run symlinks.
**Severity:** High.

### UPD-05 — In-place overwrite, no staging / atomic swap / backup — partial update bricks app
**Files:** `SimpleLauncher.Updater/Services/ZipService.cs:145-202` / Avalonia `:144-201`
```csharp
await using var destinationFileStream = new FileStream(
    destinationPath, FileMode.Create, FileAccess.Write,
    FileShare.ReadWrite | FileShare.Delete, FileBufferSize, true);
await using var entryStream = reader.OpenEntryStream();
await entryStream.CopyToAsync(destinationFileStream, cancellationToken);
```
Writes directly onto live install. Any mid-zip failure (cancel, disk-full, lock, power loss) leaves half-old/half-new binaries with no rollback. No temp-dir + `Move`, no `.bak`, no manifest. Identical both.
**Severity:** High.

### UPD-06 — Unbounded `MemoryStream` download — OOM / zip-bomb / disk-full
**Files:** `SimpleLauncher.Updater/Services/DownloadService.cs:68-70` / Avalonia `:67-69`
```csharp
var totalBytes = response.Content.Headers.ContentLength ?? -1L;
var memoryStream = new MemoryStream();
```
`ContentLength` read but never enforced; no max-size cap, no `DriveInfo.AvailableFreeSpace` check, no streaming to disk. Attacker-controlled size exhausts RAM. `catch (HttpRequestException or IOException or OperationCanceledException)` doesn't cover `OutOfMemoryException`.
**Severity:** High.

### UPD-07 — Untrusted URL executed — `browser_download_url` + `version.txt` without allowlist
**Files:** `SimpleLauncher.Updater/Services/GitHubService.cs:159-167,220-237` / Avalonia `:163-171,224-241`
```csharp
var assetUrl = asset.GetProperty("browser_download_url").GetString();
var versionText = ...; assetUrl = SecondaryServerBaseUrl + expectedAssetName
```
Asset host never validated (any `https://` accepted), no pinning, no TLS enforcement. `version.txt` bare string trusted to construct fallback URL.
**Severity:** High.

### UPD-08 — PID not validated — waits on / races with wrong process
**Files:** `SimpleLauncher.Updater/Services/ProcessService.cs:32-33` (both)
```csharp
using var mainAppProcess = Process.GetProcessById(processId.Value);
```
`processId` from CLI args (`int.TryParse(_args[0])`, `MainWindow.xaml.cs:158-162` / `MainWindow.axaml.cs:151-155`) with only `pid > 0` check. No process-name/start-time/path check. PID reuse or malicious arg → 30s wait on unrelated process then `TimeoutException` abort (DoS), or proceed while real app still holds locks.
**Severity:** High.

### UPD-09 — Null-PID path waits for one instance, then proceeds into locks
**Files:** `SimpleLauncher.Updater/Services/ProcessService.cs:73-99` / Avalonia `:72-97`
```csharp
var processes = Process.GetProcessesByName("SimpleLauncher");
if (processes.Length > 0) { var process = processes[0]; ... while (...) {...}
    if (!hasExited) { LogMessage?.Invoke(... "Proceeding anyway."); } }
```
Only `processes[0]` waited on; instances 2..N ignored but still lock files. Divergent semantics: PID path **throws** `TimeoutException` (`:48`), null-PID path logs and **proceeds anyway** → partial-write / retry storm on multi-instance machines.
**Severity:** High.

### UPD-10 — Shared `HttpClient` auth-header mutation — race + secret leak
**Files:** `SimpleLauncher.Updater/Services/ApplicationStats.cs:52-53` / Avalonia `:51-52`
```csharp
var httpClient = MainWindow.HttpClient;
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
```
Mutates single static `HttpClient` (`MainWindow.xaml.cs:21` / `MainWindow.axaml.cs:20`, `Timeout=5min`) used concurrently by `DownloadService`/`GitHubService`/`BugReportService`. `DefaultRequestHeaders` not safe to mutate during in-flight requests; `SendLaunchStats()` runs as `Task.Run` at startup parallel to download. Bearer key left on client and sent to `api.github.com` / `assets.purelogiccode.com` subsequently.
**Severity:** High.

## Medium

### UPD-11 — `HttpResponseMessage` leaks in `GitHubService`
**Files:** `SimpleLauncher.Updater/Services/GitHubService.cs:107,213` / Avalonia `:111,217`
```csharp
var response = await _httpClient.GetAsync(apiUrl, linkedCts.Token);
var versionResponse = await _httpClient.GetAsync(versionUrl, cancellationToken);
```
No `using`. `DownloadService` disposes correctly; `GitHubService` never does. Socket exhaustion.
**Severity:** Medium.

### UPD-12 — `HttpResponseMessage` leak in `BugReportService`
**Files:** `SimpleLauncher.Updater/Services/BugReportService.cs:64` / Avalonia `:62`
```csharp
var response = await MainWindow.HttpClient.SendAsync(request, cts.Token);
if (!response.IsSuccessStatusCode) { var responseContent = await response.Content.ReadAsStringAsync(cts.Token); ... }
```
No `using`. Every bug report leaks response + stream.
**Severity:** Medium.

### UPD-13 — Avalonia updater never disposes `HttpClient` / sink — WPF does (divergence)
- WPF: `MainWindow.xaml.cs:75-78` `DisposeHttpClient()`, `App.xaml.cs:58-67` `OnExit` disposes client + `BugReportService` + `Log.CloseAndFlush()`.
- Avalonia: `MainWindow.axaml.cs` has **no** `DisposeHttpClient`; `App.axaml.cs:18-55` has no `OnExit`/`Dispose`, never disposes `_cts`, `_processTask`, sink, or `HttpClient`.
**Severity:** Medium.

### UPD-14 — Dokan MSI without verification + cleanup that never runs
**Files:** `SimpleLauncher.Updater/Services/DokanService.cs:84-139` / Avalonia `:86-141`
```csharp
await using var memoryStream = await _downloadService.DownloadToMemoryAsync(downloadUrl);
await using var fileStream = File.Create(msiPath);
var process = Process.Start(new ProcessStartInfo { FileName = msiPath, UseShellExecute = true });
_ = Task.Run(async () => { await Task.Delay(TimeSpan.FromMinutes(10)); File.Delete(msiPath); });
```
No hash/signature on MSI (same as UPD-01). MSI staged in `appDirectory` (often ACL-protected). 10-min `Task.Run` cleanup dies when updater exits after restart — normal case — so `Dokan_*.msi` litters install dir forever.
**Severity:** Medium/High.

### UPD-15 — `DokanService` event-subscription leak
**Files:** `Services/DokanService.cs:95-96` (both)
```csharp
_downloadService.LogMessage += (_, e) => LogMessage?.Invoke(this, e);
_downloadService.ProgressChanged += (_, e) => ProgressChanged?.Invoke(this, e);
```
Added on every `DownloadAndInstallDokanAsync()` call, never removed. Duplicate spam + roots `DokanService` via long-lived `DownloadService`.
**Severity:** Medium.

### UPD-16 — `RestartApplication` reports success when nothing started
**Files:** `SimpleLauncher.Updater/Services/ProcessService.cs:156` / Avalonia `:161`
```csharp
Process.Start(startInfo)?.Dispose();
return true;
```
If `Process.Start` returns `null`, still returns `true`. Caller treats as restarted and closes updater.
**Severity:** Medium.

### UPD-17 — WPF restart hardcodes `.exe` — divergence
- WPF `ProcessService.cs:139`: `Path.Combine(appDirectory, $"{executableName}.exe")`
- Avalonia `ProcessService.cs:140-144` fixed it:
```csharp
var baseName = executableName.EndsWith(".exe", ...) ? executableName[..^4] : executableName;
var executableFileName = OperatingSystem.IsWindows() ? $"{baseName}.exe" : baseName;
```
Avalonia comment (`UpdateService.cs:269-271`) admits old form produced `SimpleLauncher.Avalonia.exe.exe`. WPF still fragile.
**Severity:** Medium.

### UPD-18 — `async void` update kickoff, no re-entrancy guard — double extraction race
- WPF `MainWindow.xaml.cs:69`: `Loaded += async (_, _) => await ExecuteUpdateAsync(_cts.Token);`
- Avalonia `MainWindow.axaml.cs:74`: `Opened += async (_, _) => await ExecuteUpdateAsync(_cts.Token);`
`async void` handlers; no `_started` flag. `Opened`/`Loaded` can fire more than once → two concurrent `ExtractFromStreamAsync` writers + double `RestartApplication`.
**Severity:** Medium.

### UPD-19 — Timeout vs. user-cancel conflated; only one fallback attempt
- Shared client: `MainWindow.xaml.cs:21` / `MainWindow.axaml.cs:20`: `new() { Timeout = TimeSpan.FromMinutes(5) }`.
- `UpdateService.cs:246` / `:244`:
```csharp
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
catch (Exception ex) when (...fallback...) { ... retry once ... }
```
`HttpClient.Timeout` surfaces as `TaskCanceledException` with `IsCancellationRequested==false`, treated as retryable (correct-ish) but linked-CTS GitHub 5s timeout also `OperationCanceledException` swallowed to `null` → silent fallback. No retry/backoff beyond single secondary attempt.
**Severity:** Medium.

### UPD-20 — Permissive `FileShare` lets half-written binaries execute
**Files:** `ZipService.cs:171-177` / Avalonia `:170-176`: `FileShare.ReadWrite | FileShare.Delete` on destination while streaming. Concurrent reader (Explorer, AV, second updater from UPD-18, not-yet-exited app from UPD-09) can load truncated `.dll`/`.exe`. Should be `FileShare.None` during write.
**Severity:** Medium.

### UPD-21 — Disk-full / read-only / ACL failures retried as "locked"
**Files:** `ZipService.cs:182-191` (both): `catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < 5)` + `Task.Delay(500*attempt)`. `ENOSPC`, read-only ACL, path-too-long burn 5 retries (~7.5s) then throw generic `Failed to extract... may be locked` masking real cause. No free-space preflight.
**Severity:** Medium.

## Low

### UPD-22 — Progress/count lies — incremented before write
**Files:** `ZipService.cs:101-112` / `:101-111`
```csharp
extractedCount++;
ProgressChanged?.Invoke(... ExtractedCount = extractedCount ...);
await ExtractFileWithRetryAsync(...);
```
Failed files counted/announced before write. Aborted updates report inflated counts.
**Severity:** Low.

### UPD-23 — `IgnoredFiles` name-only, over- and under-matched
- WPF `MainWindow.xaml.cs:24-31`: `["Updater.exe","Updater.pdb","Updater.dll","Updater.deps.json","Updater.runtimeconfig.json"]`
- Avalonia `MainWindow.axaml.cs:23-31`: adds extensionless `"SimpleLauncher.Avalonia.Updater"` + 5 suffixed names.
Match is `Path.GetFileName(entryKey)` (`ZipService.cs:78-80`), so `subdir/Updater.dll` also skipped (overbroad), while case/unicode variants or running single-file host not covered. WPF has no extensionless entry.
**Severity:** Low.

### UPD-24 — ZipSlip / extraction errors double-reported as bugs
**Files:** `ZipService.cs:116-121` (both): `catch (Exception ex) { Log.Error; await BugReportService.ReportBugAsync(ex,...); throw; }`, then `UpdateService.cs:193-197` reports `"Error extracting ZIP archive"` again. Malicious zip triggering `SecurityException` spams bug API twice per file.
**Severity:** Low.

### UPD-25 — No `SupportedOSPlatform` guard on WPF `DokanService` registry path (divergence)
- WPF `DokanService.cs:158-200` calls `RegistryKey.OpenBaseKey` with no `[SupportedOSPlatform("windows")]`; Avalonia added `[SupportedOSPlatform("windows")]` (`:160,171`) + `if (!OperatingSystem.IsWindows()) return false` (`:47`) + skips prompt on Linux (`MainWindow.axaml.cs:122,167`). WPF Windows-only today so not exploitable, but next cross-compile inherits unguarded path.
**Severity:** Low.

---
---

# Fix priority (suggested)

1. **Security/RCE first:** UPD-01, UPD-03, UPD-04, UPD-07, CORE-02, CORE-05, CORE-07, CORE-17, CORE-18, CORE-27, WPF-04, WPF-05, WPF-06, AV-13, AV-14, AV-15.
2. **Data loss / brick:** CORE-01, CORE-06, UPD-05, UPD-06, UPD-20, UPD-21, WPF-07, AV-24.
3. **Deadlock / hang:** CORE-03, CORE-04, CORE-22, WPF-02, WPF-03, WPF-11, WPF-17, AV-11, AV-12, UPD-08, UPD-09, UPD-18.
4. **Leaks / stability:** CORE-08–CORE-10, CORE-12–CORE-15, CORE-19, CORE-23–CORE-25, WPF-01, WPF-08, WPF-18–WPF-22, AV-01–AV-05, UPD-10–UPD-15.
5. **Logic/UX:** CORE-11, CORE-26, CORE-28–CORE-31, WPF-09–WPF-10, WPF-12–WPF-16, WPF-23–WPF-25, AV-06–AV-10, AV-16–AV-23, AV-25, UPD-02, UPD-16–UPD-17, UPD-19, UPD-22–UPD-25.

*End of report — 31 Core + 25 WPF + 25 Avalonia + 25 Updater = 106 items.*
