using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.SanitizeInputString;
using SimpleLauncher.Interfaces;

namespace SimpleLauncher.Services.GameScan;

/// <summary>
///     Scans for installed Microsoft Store games using PowerShell, classifies them via an API,
///     and creates launch shortcuts and cover images for confirmed games.
/// </summary>
internal partial class ScanMicrosoftStoreGames : IGamePlatformScanner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScanMicrosoftStoreGames" /> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="httpClientFactory">The HTTP client factory for classification API requests.</param>
    public ScanMicrosoftStoreGames(ILogger logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    ///     Enumerates installed Microsoft Store apps via PowerShell, classifies them as games through the API,
    ///     and creates shortcuts and images for the confirmed games.
    /// </summary>
    /// <param name="gameScannerService">The scanner service providing shared helpers.</param>
    /// <param name="logErrors">The error logger.</param>
    /// <param name="windowsRomsPath">The directory where game shortcuts are created.</param>
    /// <param name="windowsImagesPath">The directory where game images are stored.</param>
    /// <param name="ignoredGameNames">The set of game names to skip.</param>
    public async Task ScanAsync(GameScannerService gameScannerService, ILogger logErrors, string windowsRomsPath,
        string windowsImagesPath, ISet<string> ignoredGameNames)
    {
        try
        {
            // Enhanced PowerShell script:
            // 1. Gets Start Menu Apps (for the correct Display Name and AppID).
            // 2. Gets AppxPackages (for InstallLocation and Logo).
            // 3. Filters out Frameworks, Resources, and Non-Store/Developer signed apps (System components).
            // 4. Matches them based on PackageFamilyName.
            const string script = """
                                  $ErrorActionPreference = 'SilentlyContinue'
                                  $OutputEncoding = [Console]::OutputEncoding = [System.Text.Encoding]::UTF8;
                                  $apps = Get-StartApps
                                  $packages = Get-AppxPackage
                                  $pkgHash = @{}

                                  # Index packages by FamilyName for speed
                                  foreach ($p in $packages) {
                                      if (-not $p.IsFramework -and -not $p.IsResourcePackage) {
                                          $pkgHash[$p.PackageFamilyName] = $p
                                      }
                                  }

                                  $results = @()

                                  foreach ($app in $apps) {
                                      # AppID is usually "FamilyName!AppId"
                                      if ([string]::IsNullOrEmpty($app.AppID)) { continue }
                                      
                                      $parts = $app.AppID.Split('!')
                                      $famName = $parts[0]
                                      
                                      if ($pkgHash.ContainsKey($famName)) {
                                          $pkg = $pkgHash[$famName]
                                          
                                          # Filter out System apps that might have slipped through (Signature check)
                                          if ($pkg.SignatureKind -eq 'System') { continue }
                                          
                                          $results += @{
                                              Name = $app.Name
                                              AppID = $app.AppID
                                              InstallLocation = $pkg.InstallLocation
                                              PackageFamilyName = $pkg.PackageFamilyName
                                              Logo = $pkg.Logo
                                          }
                                      }
                                  }
                                  $results | ConvertTo-Json -Depth 2 -Compress
                                  """;

            var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var powerShellPath = Path.Combine(systemPath, "WindowsPowerShell", "v1.0", "powershell.exe");

            if (!File.Exists(powerShellPath)) powerShellPath = "powershell.exe";

            var startInfo = new ProcessStartInfo
            {
                FileName = powerShellPath,
                Arguments = $"-NoProfile -Command \"{script}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            string output;
            string errorOutput;
            int exitCode;

            try
            {
                // Add a 30-second timeout to prevent indefinite hangs on locked-down systems
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    _logger.Debug(
                        "[ScanMicrosoftStoreGames] PowerShell process returned null (likely blocked by policy). Skipping Microsoft Store scan");
                    return;
                }

                output = await process.StandardOutput.ReadToEndAsync(cts.Token);
                errorOutput = await process.StandardError.ReadToEndAsync(cts.Token);
                await process.WaitForExitAsync(cts.Token);
                exitCode = process.ExitCode;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug(
                    "[ScanMicrosoftStoreGames] PowerShell scan timed out after 30 seconds. Skipping Microsoft Store scan");
                return;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode is 5 or 2 or 126)
            {
                // 5 = Access Denied (AppLocker/WDAC), 2 = File Not Found, 126 = Module Not Found
                _logger.Debug(
                    $"[ScanMicrosoftStoreGames] PowerShell blocked or unavailable (Win32 error {ex.NativeErrorCode}). Skipping Microsoft Store scan.");
                return;
            }
            catch (Exception ex)
            {
                _logger.Debug(
                    $"[ScanMicrosoftStoreGames] Failed to execute PowerShell: {ex.Message}. Skipping Microsoft Store scan.");
                return;
            }

            if (exitCode != 0 && !string.IsNullOrWhiteSpace(errorOutput))
            {
                // Check for execution policy restrictions - skip silently if restricted
                if (IsExecutionPolicyRestricted(errorOutput))
                {
                    _logger.Debug(
                        "[ScanMicrosoftStoreGames] PowerShell execution policy restrictions detected. Skipping Microsoft Store games scan");
                    return;
                }

                // Log warning but don't crash, PS might emit non-fatal errors to stderr
                _logger.Debug($"[ScanMicrosoftStoreGames] PowerShell warning/error: {errorOutput}");
            }

            if (string.IsNullOrWhiteSpace(output)) return;

            var trimmedOutput = output.Trim();

            // Extract the first complete JSON array or object from the output.
            // PowerShell may output non-JSON content (warnings, debug info) alongside the JSON,
            // which causes JsonReaderException if parsed directly.
            var jsonStr = ExtractFirstJsonArray(trimmedOutput);
            if (jsonStr == null)
            {
                var jsonObj = ExtractFirstJsonObject(trimmedOutput);
                if (jsonObj == null) return;

                jsonStr = $"[{jsonObj}]";
            }

            // Remove invalid control characters that may appear in PowerShell output
            // (e.g., game names containing control characters like 0x07 BEL)
            jsonStr = SanitizeJsonControlCharacters(jsonStr);

            var allInstalledApps = new List<StoreAppInfo>();

            using var doc = JsonDocument.Parse(jsonStr);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return;

            // Track seen AppIds to avoid duplicates from multiple Start Menu entries
            var seenAppIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                try
                {
                    var name = element.GetProperty("Name").GetString();
                    var appId = element.GetProperty("AppID").GetString();
                    var installLocation = element.TryGetProperty("InstallLocation", out var il) ? il.GetString() : null;
                    var packageFamilyName =
                        element.TryGetProperty("PackageFamilyName", out var pfn) ? pfn.GetString() : "";
                    var logoRelativePath = element.TryGetProperty("Logo", out var lg) ? lg.GetString() : null;

                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(appId)) continue;

                    // Skip duplicates
                    if (!seenAppIds.Add(appId)) continue;

                    allInstalledApps.Add(new StoreAppInfo
                    {
                        Name = name,
                        AppId = appId,
                        InstallLocation = installLocation ?? "",
                        PackageFamilyName = packageFamilyName ?? "",
                        LogoRelativePath = logoRelativePath ?? ""
                    });
                }
                catch (Exception ex)
                {
                    logErrors.Error(ex, "Error processing Microsoft Store game entry");
                }
            }

            if (allInstalledApps.Count == 0)
            {
                _logger.Debug("[ScanMicrosoftStoreGames] No Microsoft Store apps found");
                return;
            }

            _logger.Debug(
                $"[ScanMicrosoftStoreGames] Found {allInstalledApps.Count} Microsoft Store apps. Sending to classification API...");
            foreach (var app in allInstalledApps)
            {
                _logger.Debug(
                    $"[ScanMicrosoftStoreGames]   -> Sending: Name=\"{app.Name}\" (Normalized=\"{app.Name.Trim().ToUpperInvariant()}\") AppId=\"{app.AppId}\"");
            }

            var confirmedGames = await ClassifyGamesViaApiAsync(allInstalledApps, logErrors);

            if (confirmedGames is { Count: > 0 })
            {
                _logger.Debug($"[ScanMicrosoftStoreGames] API returned {confirmedGames.Count} confirmed games.");

                Directory.CreateDirectory(windowsRomsPath);

                foreach (var game in confirmedGames)
                {
                    var sanitizedGameName = SanitizeInputSystemName.SanitizeFolderName(game.Name);
                    var shortcutPath = Path.Combine(windowsRomsPath, $"{sanitizedGameName}.bat");

                    var batchContent = $"@echo off\r\nstart \"\" \"shell:AppsFolder\\{game.AppId}\"";
                    await File.WriteAllTextAsync(shortcutPath, batchContent);

                    if (!string.IsNullOrEmpty(game.InstallLocation) && Directory.Exists(game.InstallLocation))
                    {
                        await TryExtractStoreIconAsync(gameScannerService, logErrors, game.Name, game.InstallLocation,
                            game.LogoRelativePath, sanitizedGameName, windowsImagesPath);
                    }
                }
            }
            else
            {
                _logger.Debug(
                    "[ScanMicrosoftStoreGames] API returned no confirmed games. The admin may need to curate the game list via the dashboard");
            }
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, "An error occurred while scanning for Microsoft Store games");
        }
    }

    private async Task<List<StoreAppInfo>> ClassifyGamesViaApiAsync(List<StoreAppInfo> installedApps, ILogger logErrors)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("GameClassificationClient");

            var requestBody = new
            {
                SoftwareNames = installedApps.ConvertAll(static app => new
                {
                    app.Name,
                    app.AppId,
                    app.InstallLocation,
                    app.PackageFamilyName,
                    app.LogoRelativePath
                })
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await client.PostAsync("api/GameIdentification/IsAGame", content, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Debug(
                    $"[ScanMicrosoftStoreGames] Game classification API returned status: {response.StatusCode}");

                if (ExpectedHttpStatus.IsExpectedExternalCondition(response.StatusCode))
                {
                    // Expected condition (rate limit or hosting/WAF block): not a bug, keep it
                    // out of the bug report service.
                    logErrors.Information(
                        $"Game classification API returned {response.StatusCode} (expected external condition). Returning empty game list.");
                }
                else
                {
                    logErrors.Warning(
                        $"Game classification API failed with status {response.StatusCode}. Returning empty game list.");
                }

                return [];
            }

            var responseJson = await response.Content.ReadAsStringAsync(cts.Token);
            var apiResponse = JsonSerializer.Deserialize<GameClassificationResponse>(responseJson, JsonOptions);

            if (apiResponse?.Games == null)
            {
                _logger.Debug("[ScanMicrosoftStoreGames] Game classification API returned null games list");
                return [];
            }

            _logger.Debug($"[ScanMicrosoftStoreGames] API deserialized games count: {apiResponse.Games.Count}");
            foreach (var g in apiResponse.Games)
                _logger.Debug($"[ScanMicrosoftStoreGames]   <- Received game: Name=\"{g.Name}\" AppId=\"{g.AppId}\"");

            var confirmedGames = apiResponse.Games.Select(static g => new StoreAppInfo
            {
                Name = g.Name,
                AppId = g.AppId,
                InstallLocation = g.InstallLocation,
                PackageFamilyName = g.PackageFamilyName,
                LogoRelativePath = g.LogoRelativePath
            }).ToList();

            return confirmedGames;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug(
                "[ScanMicrosoftStoreGames] Game classification API request timed out. Returning empty game list");
            return [];
        }
        catch (HttpRequestException ex)
        {
            _logger.Debug(
                $"[ScanMicrosoftStoreGames] Game classification API network error: {ex.Message}. Returning empty game list.");
            return [];
        }
        catch (Exception ex)
        {
            _logger.Debug(
                $"[ScanMicrosoftStoreGames] Game classification API error: {ex.Message}. Returning empty game list.");
            logErrors.Error(ex, "Failed to classify games via API");
            return [];
        }
    }

    private static async Task TryExtractStoreIconAsync(GameScannerService gameScannerService, ILogger logErrors,
        string gameName, string installPath, string logoRelativePath, string sanitizedGameName,
        string windowsImagesPath)
    {
        // Ensure the destination directory exists
        if (!Directory.Exists(windowsImagesPath)) Directory.CreateDirectory(windowsImagesPath);

        var destPath = Path.Combine(windowsImagesPath, $"{sanitizedGameName}.png");
        // Check if a valid icon already exists (non-zero size to handle corrupt/empty files from previous failed copies)
        if (File.Exists(destPath))
        {
            var fileInfo = new FileInfo(destPath);
            if (fileInfo.Length > 0) return;
            // If file exists but is empty/corrupt, we continue to overwrite it
        }

        // 1. Try API first
        if (await gameScannerService.TryDownloadImageFromApiAsync(gameName, destPath, logErrors)) return;

        try
        {
            // 2. Try the Logo property returned by PowerShell (often points to Assets\StoreLogo.png or similar)
            if (!string.IsNullOrEmpty(logoRelativePath))
            {
                var fullLogoPath = Path.Combine(installPath, logoRelativePath);
                if (File.Exists(fullLogoPath) &&
                    await TryCopyStoreLogoAsync(fullLogoPath, destPath, sanitizedGameName, logErrors))
                    return;
            }

            // 3. Heuristic Search: Look for common logo names
            // Windows Store apps often use "targetsize" naming for scaled icons.
            var possibleFiles = new List<string>
            {
                "StoreLogo.png", "Logo.png", "AppIcon.png",
                "Square150x150Logo.png", "Square310x310Logo.png", "Square44x44Logo.png",
                "Wide310x150Logo.png", "SplashScreen.png"
            };

            // Add search for targetsize (e.g., AppIcon.targetsize-256.png)
            var searchDirectories = new[]
                { installPath, Path.Combine(installPath, "Assets"), Path.Combine(installPath, "Images") };

            foreach (var dir in searchDirectories)
            {
                if (!Directory.Exists(dir)) continue;

                // Check exact matches
                foreach (var fileName in possibleFiles)
                {
                    var p = Path.Combine(dir, fileName);
                    if (File.Exists(p) &&
                        await TryCopyStoreLogoAsync(p, destPath, sanitizedGameName, logErrors))
                        return;
                }

                // Check for high-res targetsize images
                string[] pngs;
                try
                {
                    pngs = Directory.GetFiles(dir, "*.png");
                }
                catch (DirectoryNotFoundException)
                {
                    // Directory may have been removed or is inaccessible
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    // No permission to access this directory
                    continue;
                }

                var bestIcon = pngs
                    .Where(static f => f.Contains("targetsize", StringComparison.OrdinalIgnoreCase) ||
                                       f.Contains("scale", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(static f =>
                    {
                        try
                        {
                            return new FileInfo(f).Length;
                        }
                        catch
                        {
                            return 0L;
                        }
                    }) // Bigger is usually better quality
                    .FirstOrDefault();

                if (bestIcon != null &&
                    await TryCopyStoreLogoAsync(bestIcon, destPath, sanitizedGameName, logErrors))
                    return;

                // Fallback: Just take the largest PNG in the Assets folder
                if (dir.EndsWith("Assets", StringComparison.Ordinal) ||
                    dir.EndsWith("Images", StringComparison.Ordinal))
                {
                    var largestPng = pngs.OrderByDescending(static f =>
                    {
                        try
                        {
                            return new FileInfo(f).Length;
                        }
                        catch
                        {
                            return 0L;
                        }
                    }).FirstOrDefault();
                    if (largestPng != null &&
                        await TryCopyStoreLogoAsync(largestPng, destPath, sanitizedGameName, logErrors))
                        return;
                }
            }

            // 4. Final fallback to extracting icon from an EXE in the install folder
            await gameScannerService.ExtractIconFromGameFolderAsync(logErrors, installPath, sanitizedGameName,
                windowsImagesPath);
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, $"Failed to extract Microsoft Store icon for {sanitizedGameName}");
        }
    }

    /// <summary>
    ///     Copies a store logo into the images folder, falling back to a byte-level copy when the
    ///     copy fails with an I/O error (e.g. EFS encryption attributes). Access denied is expected
    ///     for assets inside the ACL-protected Microsoft Store package folder
    ///     (C:\Program Files\WindowsApps) and is logged at Information level so it is not reported
    ///     as a bug (bug 66892).
    /// </summary>
    /// <param name="sourcePath">The logo path inside the store package folder.</param>
    /// <param name="destPath">The destination image path.</param>
    /// <param name="sanitizedGameName">The sanitized game name used in log messages.</param>
    /// <param name="logErrors">The error logger.</param>
    /// <returns>True when the logo was copied; false when the copy failed and another candidate should be tried.</returns>
    private static async Task<bool> TryCopyStoreLogoAsync(string sourcePath, string destPath, string sanitizedGameName,
        ILogger logErrors)
    {
        try
        {
            await Task.Run(() => File.Copy(sourcePath, destPath, true));
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            // Expected user-environment condition: some assets in the Microsoft Store package
            // folder cannot be read without elevation (bug 66892).
            logErrors.Information(ex, $"Failed to copy Microsoft Store logo for {sanitizedGameName}");
            return false;
        }
        catch (IOException)
        {
            // EFS encryption error or other IO error - fallback to byte-level copy which doesn't preserve encryption attributes
            try
            {
                await Task.Run(() =>
                {
                    var bytes = File.ReadAllBytes(sourcePath);
                    File.WriteAllBytes(destPath, bytes);
                });
                return true;
            }
            catch (UnauthorizedAccessException fallbackEx)
            {
                // Expected user-environment condition: see above.
                logErrors.Information(fallbackEx,
                    $"Failed to copy Microsoft Store logo for {sanitizedGameName} (fallback method)");
                return false;
            }
            catch (Exception fallbackEx)
            {
                logErrors.Error(fallbackEx,
                    $"Failed to copy Microsoft Store logo for {sanitizedGameName} (fallback method)");
                return false;
            }
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, $"Failed to copy Microsoft Store logo for {sanitizedGameName}");
            return false;
        }
    }

    /// <summary>
    ///     Extracts the first complete JSON array from a string that may contain non-JSON content.
    ///     Uses bracket depth counting to find the matching closing bracket.
    /// </summary>
    private static string? ExtractFirstJsonArray(string input)
    {
        var startIndex = input.IndexOf('[');
        if (startIndex < 0) return null;

        var result = ExtractJsonAtPosition(input, startIndex, '[', ']');
        return result;
    }

    /// <summary>
    ///     Extracts the first complete JSON object from a string that may contain non-JSON content.
    ///     Uses brace depth counting to find the matching closing brace.
    /// </summary>
    private static string? ExtractFirstJsonObject(string input)
    {
        var startIndex = input.IndexOf('{');
        if (startIndex < 0) return null;

        return ExtractJsonAtPosition(input, startIndex, '{', '}');
    }

    private static string? ExtractJsonAtPosition(string input, int startIndex, char openChar, char closeChar)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = startIndex; i < input.Length; i++)
        {
            var c = input[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            switch (c)
            {
                case '\\' when inString:
                    escaped = true;
                    continue;
                case '"':
                    inString = !inString;
                    continue;
            }

            if (inString) continue;

            if (c == openChar)
            {
                depth++;
            }
            else if (c == closeChar)
            {
                depth--;
                if (depth == 0) return input.Substring(startIndex, i - startIndex + 1);
            }
        }

        return null;
    }

    /// <summary>
    ///     Removes invalid control characters from JSON strings.
    ///     Control characters (0x00-0x1F) are not valid in JSON strings unless properly escaped.
    ///     This method removes them to prevent JsonReaderException.
    /// </summary>
    private static string SanitizeJsonControlCharacters(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;

        // Remove control characters 0x00-0x1F except for valid whitespace:
        // 0x09 (tab), 0x0A (line feed), 0x0D (carriage return)
        // Also allow 0x7F (DEL) and 0x00 is already excluded
        return MyRegex().Replace(json, "");
    }

    /// <summary>
    ///     Detects if PowerShell error output indicates execution policy restrictions
    /// </summary>
    private static bool IsExecutionPolicyRestricted(string errorOutput)
    {
        if (string.IsNullOrWhiteSpace(errorOutput)) return false;

        var lowerError = errorOutput.ToLowerInvariant();
        return lowerError.Contains("execution of scripts is disabled", StringComparison.Ordinal) ||
               (lowerError.Contains("execution policy", StringComparison.Ordinal) &&
                (lowerError.Contains("prevents execution", StringComparison.Ordinal) ||
                 lowerError.Contains("restricted", StringComparison.Ordinal) ||
                 lowerError.Contains("cannot be loaded", StringComparison.Ordinal))) ||
               (lowerError.Contains("is not digitally signed", StringComparison.Ordinal) &&
                lowerError.Contains("execution policy", StringComparison.Ordinal));
    }

    [GeneratedRegex("[\0-\b\v\f\x0E-\x1F]", RegexOptions.None, 1000)]
    private static partial Regex MyRegex();
}