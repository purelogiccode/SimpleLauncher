using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.SanitizeInputString;
using SimpleLauncher.Services.SystemManager;
using CoreMessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher;

internal partial class EditSystemWindow
{
    private async void SaveSystemButtonClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            // Trim input values
            TrimInputValues(out var systemNameText, out var systemFolderText, out var varSystemImageFolderText,
                out var formatToSearchText, out var formatToLaunchText, out var emulator1NameText,
                out var emulator2NameText, out var emulator3NameText, out var emulator4NameText,
                out var emulator5NameText, out var emulator1LocationText, out var emulator2LocationText,
                out var emulator3LocationText, out var emulator4LocationText, out var emulator5LocationText,
                out var emulator1ParametersText, out var emulator2ParametersText, out var emulator3ParametersText,
                out var emulator4ParametersText, out var emulator5ParametersText);

            // Validate SystemName for invalid characters before sanitizing
            if (SanitizeInputSystemName.ContainsInvalidCharacters(systemNameText, out var invalidChars))
            {
                var invalidCharsStr = string.Join(", ", invalidChars.Select(static c => $"'{c}'"));
                await _messageBox.InvalidSystemNameCharactersMessageBoxAsync(invalidCharsStr);
                MarkInvalid(SystemNameTextBox);
                return;
            }

            // Sanitize SystemNameTextBox.Text immediately
            systemNameText = SanitizeInputSystemName.SanitizeFolderName(systemNameText);
            SystemNameTextBox.Text = systemNameText;

            // --- Collect all system folders ---
            var allSystemFolders = new List<string> { systemFolderText };
            allSystemFolders.AddRange(AdditionalFoldersListBox.Items.Cast<string>().Select(static f => f.Trim()));
            allSystemFolders = allSystemFolders.Where(static f => !string.IsNullOrWhiteSpace(f)).ToList();

            // --- Apply %BASEFOLDER% prefix to relative paths before validation/saving ---
            allSystemFolders = allSystemFolders.ConvertAll(MaybeAddBaseFolderPrefix);
            varSystemImageFolderText = MaybeAddBaseFolderPrefix(varSystemImageFolderText);
            emulator1LocationText = MaybeAddBaseFolderPrefix(emulator1LocationText);
            emulator2LocationText = MaybeAddBaseFolderPrefix(emulator2LocationText);
            emulator3LocationText = MaybeAddBaseFolderPrefix(emulator3LocationText);
            emulator4LocationText = MaybeAddBaseFolderPrefix(emulator4LocationText);
            emulator5LocationText = MaybeAddBaseFolderPrefix(emulator5LocationText);

            // --- Update UI with processed values ---
            SystemFolderTextBox.Text = allSystemFolders.FirstOrDefault() ?? "";
            AdditionalFoldersListBox.Items.Clear();
            foreach (var folder in allSystemFolders.Skip(1)) AdditionalFoldersListBox.Items.Add(folder);

            SystemImageFolderTextBox.Text = varSystemImageFolderText;
            Emulator1PathTextBox.Text = emulator1LocationText;
            Emulator2PathTextBox.Text = emulator2LocationText;
            Emulator3PathTextBox.Text = emulator3LocationText;
            Emulator4PathTextBox.Text = emulator4LocationText;
            Emulator5PathTextBox.Text = emulator5LocationText;

            // Validate paths (now using potentially prefixed paths)
            // The ValidatePaths method itself doesn't need to understand %BASEFOLDER%
            // because CheckPath.IsValidPath can handle it.
            ValidatePaths(systemNameText, allSystemFolders.FirstOrDefault() ?? "", varSystemImageFolderText,
                emulator1LocationText,
                emulator2LocationText, emulator3LocationText, emulator4LocationText, emulator5LocationText,
                out var isSystemFolderValid, out var isSystemImageFolderValid, out var isEmulator1LocationValid,
                out var isEmulator2LocationValid, out var isEmulator3LocationValid, out var isEmulator4LocationValid,
                out var isEmulator5LocationValid);

            // Handle validation alerts (coloring)
            HandleValidationAlerts(isSystemFolderValid, isSystemImageFolderValid, isEmulator1LocationValid,
                isEmulator2LocationValid, isEmulator3LocationValid, isEmulator4LocationValid, isEmulator5LocationValid);

            // Early return if any path is invalid — don't proceed with saving
            if (!isSystemFolderValid || !isSystemImageFolderValid) return;

            // Validate SystemName (now with sanitized value)
            if (await ValidateSystemNameAsync(systemNameText)) return;

            // Refuse to save over another existing system (case-insensitive): the unified
            // database upsert would silently replace it, and the legacy XML path would append
            // a duplicate. A case-only rename of the same system remains allowed.
            if (SystemManagerService.SystemExists(systemNameText, _configuration) &&
                !string.Equals(systemNameText, _originalSystemName, StringComparison.OrdinalIgnoreCase))
            {
                var messageTemplate = (string?)Application.Current.TryFindResource("SystemNameAlreadyExists")
                                      ?? "A system named '{0}' already exists. Please choose a different name.";
                var title = (string?)Application.Current.TryFindResource("SystemNameAlreadyInUse")
                            ?? "System Name Already In Use";
                await _messageBox.CustomErrorMessageBoxAsync(
                    string.Format(CultureInfo.CurrentCulture, messageTemplate, systemNameText), title);
                MarkInvalid(SystemNameTextBox);
                return;
            }

            // Validate SystemFolder (uses the potentially prefixed value)
            var firstFolder = allSystemFolders.FirstOrDefault() ?? "";
            var systemFolderResult = await ValidateSystemFolderAsync(systemNameText, firstFolder);
            if (systemFolderResult.IsFailed) return;

            firstFolder = systemFolderResult.FolderText;

            if (allSystemFolders.Count > 0)
                allSystemFolders[0] = firstFolder;
            else
                allSystemFolders.Add(firstFolder);

            // Validate SystemImageFolder (uses the potentially prefixed value)
            var imageFolderResult = await ValidateSystemImageFolderAsync(systemNameText, varSystemImageFolderText);
            if (imageFolderResult.IsFailed) return;

            varSystemImageFolderText = imageFolderResult.FolderText;

            var extractFileBeforeLaunch = ExtractFileBeforeLaunchComboBox.SelectedItem != null &&
                                          bool.Parse((ExtractFileBeforeLaunchComboBox.SelectedItem as ComboBoxItem)
                                              ?.Content.ToString() ?? "false");

            var groupByFolder = (GroupByFolderComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                ?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
            var disableRecursiveSearch = (DisableRecursiveSearchComboBox.SelectedItem as ComboBoxItem)?.Content
                ?.ToString()
                ?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

            var formatSearchResult = await ValidateFormatToSearchAsync(formatToSearchText, extractFileBeforeLaunch);
            if (formatSearchResult.IsFailed)
            {
                MarkInvalid(FormatToSearchTextBox);
                return;
            }

            MarkValid(FormatToSearchTextBox); // Valid state

            var formatsToSearch = formatSearchResult.Formats;

            var formatLaunchResult = await ValidateFormatToLaunchAsync(formatToLaunchText, extractFileBeforeLaunch);
            if (formatLaunchResult.IsFailed) return;

            var formatsToLaunch = formatLaunchResult.Formats;

            if (await ValidateEmulator1NameAsync(emulator1NameText)) return;

            if (await ValidateEmulatorLocationAsync(emulator1NameText, emulator1LocationText, formatsToSearch, 1))
            {
                MarkInvalid(Emulator1PathTextBox);
                return;
            }

            if (await ValidateEmulatorLocationAsync(emulator2NameText, emulator2LocationText, formatsToSearch, 2))
            {
                MarkInvalid(Emulator2PathTextBox);
                return;
            }

            if (await ValidateEmulatorLocationAsync(emulator3NameText, emulator3LocationText, formatsToSearch, 3))
            {
                MarkInvalid(Emulator3PathTextBox);
                return;
            }

            if (await ValidateEmulatorLocationAsync(emulator4NameText, emulator4LocationText, formatsToSearch, 4))
            {
                MarkInvalid(Emulator4PathTextBox);
                return;
            }

            if (await ValidateEmulatorLocationAsync(emulator5NameText, emulator5LocationText, formatsToSearch, 5))
            {
                MarkInvalid(Emulator5PathTextBox);
                return;
            }

            // Check if any of the *location* paths are invalid after prefixing/validation
            if (await CheckPathsAsync(isSystemFolderValid, isSystemImageFolderValid, isEmulator1LocationValid,
                    isEmulator2LocationValid, isEmulator3LocationValid, isEmulator4LocationValid,
                    isEmulator5LocationValid))
            {
                return;
            }

            // Warn user if GroupByFolder is true with neither MAME nor DOSBox configured
            if (groupByFolder)
            {
                var emulatorsToCheck = new[]
                {
                    (Name: emulator1NameText, Location: emulator1LocationText),
                    (Name: emulator2NameText, Location: emulator2LocationText),
                    (Name: emulator3NameText, Location: emulator3LocationText),
                    (Name: emulator4NameText, Location: emulator4LocationText),
                    (Name: emulator5NameText, Location: emulator5LocationText)
                };

                var hasMameOrDosBoxEmulator = emulatorsToCheck.Any(static emu =>
                    !string.IsNullOrEmpty(emu.Name) && (
                        emu.Name.Contains("MAME", StringComparison.OrdinalIgnoreCase) ||
                        emu.Name.Contains("DOSBox", StringComparison.OrdinalIgnoreCase) ||
                        (emu.Location != null && (
                            emu.Location.Contains("mame.exe", StringComparison.OrdinalIgnoreCase) ||
                            emu.Location.Contains("mame64.exe", StringComparison.OrdinalIgnoreCase) ||
                            emu.Location.Contains("dosbox", StringComparison.OrdinalIgnoreCase))))
                );

                if (!hasMameOrDosBoxEmulator)
                {
                    var result = await _messageBox.GroupByFolderWarningMessageBoxAsync();
                    if (result == CoreMessageBoxResult.No) return; // User chose not to save, so abort.
                }
            }

            string[] parameterTexts =
            [
                emulator1ParametersText, emulator2ParametersText, emulator3ParametersText, emulator4ParametersText,
                emulator5ParametersText
            ];
            string[] allEmulatorLocationTexts = // Used for validating parameters
            [
                emulator1LocationText, emulator2LocationText, emulator3LocationText, emulator4LocationText,
                emulator5LocationText
            ];

            var receiveNotification1 =
                ReceiveANotificationOnEmulatorError1.SelectedItem is not ComboBoxItem { Content: not null } item1 ||
                string.Equals(item1.Content.ToString(), "true", StringComparison.Ordinal);
            var receiveNotification2 =
                ReceiveANotificationOnEmulatorError2.SelectedItem is not ComboBoxItem { Content: not null } item2 ||
                string.Equals(item2.Content.ToString(), "true", StringComparison.Ordinal);
            var receiveNotification3 =
                ReceiveANotificationOnEmulatorError3.SelectedItem is not ComboBoxItem { Content: not null } item3 ||
                string.Equals(item3.Content.ToString(), "true", StringComparison.Ordinal);
            var receiveNotification4 =
                ReceiveANotificationOnEmulatorError4.SelectedItem is not ComboBoxItem { Content: not null } item4 ||
                string.Equals(item4.Content.ToString(), "true", StringComparison.Ordinal);
            var receiveNotification5 =
                ReceiveANotificationOnEmulatorError5.SelectedItem is not ComboBoxItem { Content: not null } item5 ||
                string.Equals(item5.Content.ToString(), "true", StringComparison.Ordinal);

            var emulators = new List<Emulator>();
            var emulatorNames = new HashSet<string>(StringComparer.Ordinal);

            // Add Emulator 1
            if (!string.IsNullOrEmpty(emulator1NameText)) // Only add if name is provided
            {
                if (!emulatorNames.Add(emulator1NameText))
                {
                    await _messageBox.EmulatorNameMustBeUniqueMessageBoxAsync(emulator1NameText);
                    return;
                }

                emulators.Add(new Emulator
                {
                    EmulatorName = emulator1NameText,
                    EmulatorLocation = emulator1LocationText,
                    EmulatorParameters = emulator1ParametersText,
                    ReceiveANotificationOnEmulatorError = receiveNotification1
                });
            }

            string[] nameTexts = [emulator2NameText, emulator3NameText, emulator4NameText, emulator5NameText];
            // locationTexts are already defined as allEmulatorLocationTexts
            bool[] receiveNotifications =
                [receiveNotification2, receiveNotification3, receiveNotification4, receiveNotification5];

            for (var i = 0; i < nameTexts.Length; i++)
            {
                var currentEmulatorName = nameTexts[i];
                var currentEmulatorLocation =
                    allEmulatorLocationTexts[i + 1]; // Use potentially prefixed location (index i+1 for emulators 2-5)
                var currentEmulatorParameters = parameterTexts[i + 1]; // Use original parameter text
                var currentReceiveNotification = receiveNotifications[i];

                if (!string.IsNullOrEmpty(currentEmulatorLocation) || !string.IsNullOrEmpty(currentEmulatorParameters))
                {
                    if (string.IsNullOrEmpty(currentEmulatorName))
                    {
                        await _messageBox.EmulatorNameRequiredMessageBoxAsync(i + 2); // Pass emulator number (2-5)
                        return;
                    }
                }

                if (string.IsNullOrEmpty(currentEmulatorName)) continue;

                if (!emulatorNames.Add(currentEmulatorName))
                {
                    await _messageBox.EmulatorNameMustBeUniqueMessageBoxAsync(currentEmulatorName);
                    return;
                }

                emulators.Add(new Emulator
                {
                    EmulatorName = currentEmulatorName,
                    EmulatorLocation = currentEmulatorLocation,
                    EmulatorParameters = currentEmulatorParameters,
                    ReceiveANotificationOnEmulatorError = currentReceiveNotification
                });
            }

            var isUpdate = !string.IsNullOrEmpty(_originalSystemName) && SystemNameDropdown.SelectedItem != null &&
                           _originalSystemName.Equals(SystemNameDropdown.SelectedItem.ToString(),
                               StringComparison.OrdinalIgnoreCase);
            var originalSystemNameToUse = isUpdate ? _originalSystemName : systemNameText;

            var systemToSave = new SystemManagerService
            {
                SystemName = systemNameText,
                SystemFolders = allSystemFolders,
                SystemImageFolder = varSystemImageFolderText,
                FileFormatsToSearch = formatsToSearch.ToList(),
                ExtractFileBeforeLaunch = extractFileBeforeLaunch,
                GroupByFolder = groupByFolder,
                DisableRecursiveSearch = disableRecursiveSearch,
                FileFormatsToLaunch = formatsToLaunch.ToList(),
                Emulators = emulators
            };

            try
            {
                SaveSystemButton.IsEnabled = false;
                await SystemManagerService.SaveSystemConfigurationAsync(systemToSave, originalSystemNameToUse);

                await LoadSystemsAsync();
                SystemNameDropdown.SelectedItem = systemNameText;
                LoadSystemDetailsAsync(
                    systemNameText); // This will load the saved values (including %BASEFOLDER%) back into UI

                // Notify user
                await _messageBox.SystemSavedSuccessfullyMessageBoxAsync();

                // Keep favorites and play history in sync when the system was renamed:
                // both store the system name as a plain string, so without this migration
                // favorites would fail to launch with a missing system manager.
                // Use the name captured before the UI refresh: LoadSystemsAsync plus the
                // subsequent SelectedItem assignment overwrite _originalSystemName with the
                // new name, so it can no longer identify the old one here.
                if (isUpdate && !string.Equals(originalSystemNameToUse, systemNameText, StringComparison.OrdinalIgnoreCase))
                {
                    var oldSystemName = originalSystemNameToUse!;
                    await _favoritesManager.RenameSystemAsync(oldSystemName, systemNameText);
                    await _playHistoryManager.RenameSystemAsync(oldSystemName, systemNameText);
                    _logger.Information(
                        $"System renamed from '{oldSystemName}' to '{systemNameText}'. Favorites and play history migrated.");
                }

                // Create folders based on the resolved paths
                var resolvedSystemFolder =
                    PathHelper.ResolveRelativeToAppDirectory(allSystemFolders.FirstOrDefault() ?? "");
                var resolvedSystemImageFolder = PathHelper.ResolveRelativeToAppDirectory(varSystemImageFolderText);
                if (resolvedSystemFolder != null && resolvedSystemImageFolder != null)
                {
                    await CreateDefaultSystemFoldersService.CreateFoldersAsync(systemNameText, resolvedSystemFolder,
                        resolvedSystemImageFolder, _configuration, _logger, _messageBox);
                }

                _originalSystemName = systemNameText; // Update original name after successful save & UI refresh
            }
            catch (InvalidOperationException ex)
            {
                // Notify user
                await _messageBox.SaveSystemFailedMessageBoxAsync(ex.InnerException?.Message ?? "Unknown error");
            }
            catch (Exception ex)
            {
                // Notify developer
                const string contextMessage = "Unexpected error during system save process.";
                _logger.Error(ex, contextMessage);

                // Notify user
                await _messageBox.SaveSystemFailedMessageBoxAsync("An unexpected error occurred.");
            }
            finally
            {
                SaveSystemButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            // Notify developer
            _logger.Error(ex, "Error saving system configuration");
        }
    }

    // Helper method to add %BASEFOLDER% prefix if the path is relative and doesn't have it
    private static string MaybeAddBaseFolderPrefix(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        // Normalize separators to the system default (e.g., / to \ on Windows)
        var normalizedPath = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        // Check if it's already rooted (absolute) using the normalized path
        if (Path.IsPathRooted(normalizedPath)) return normalizedPath;

        // Check if it already starts with the placeholder
        if (normalizedPath.StartsWith("%BASEFOLDER%", StringComparison.OrdinalIgnoreCase)) return normalizedPath;

        // It's a relative path without the placeholder, add it
        // Handle cases like ".\roms" or "../images"
        var trimmedPath = normalizedPath.TrimStart('.', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.Combine("%BASEFOLDER%", trimmedPath);
    }
}