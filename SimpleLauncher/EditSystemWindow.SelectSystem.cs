using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.CheckPaths;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher;

internal partial class EditSystemWindow
{
    private void PopulateSystemNamesDropdown()
    {
        if (_systems == null) return;

        var currentSelection = SystemNameDropdown.SelectedItem?.ToString();
        SystemNameDropdown.ItemsSource = _systems
            .Select(static s => s.SystemName)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();

        // Try to restore selection if it still exists
        if (currentSelection != null && SystemNameDropdown.Items.Contains(currentSelection))
        {
            SystemNameDropdown.SelectedItem = currentSelection;
        }
        else if (SystemNameDropdown.Items.Count > 0)
        {
            //SystemNameDropdown.SelectedIndex = 0; // Optionally select the first item
        }
        // No items, ensure UI reflects this (handled by SelectionChanged if selection becomes null)
    }

    private void SystemNameDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var currentSelectedSystemName = SystemNameDropdown.SelectedItem?.ToString();
        _originalSystemName = currentSelectedSystemName; // Keep track of the name of the system being edited/viewed

        if (currentSelectedSystemName == null)
        {
            // No system is selected from the dropdown.
            ClearFieldsForNoSelection();
            DisableAllEditableFields();
            SaveSystemButton.IsEnabled = false;
            DeleteSystemButton.IsEnabled = false;
            HelpUserTextBlock.Document.Blocks.Clear();
        }
        else
        {
            // A system is selected. Load its details.
            LoadSystemDetailsAsync(currentSelectedSystemName);
        }
    }

    private async void LoadSystemDetailsAsync(string systemNameToLoad)
    {
        try
        {
            // This method assumes systemNameToLoad is a valid, existing system name.
            // It will enable fields, populate them from _xmlDoc, and handle related UI updates.

            ClearAllEmulatorFieldsInternal(); // Clear all emulator fields first, including notifications
            // This also resets notification ComboBoxes.

            EnableFields();
            SaveSystemButton.IsEnabled = true;
            DeleteSystemButton.IsEnabled = true;

            var selectedSystem = _systems.FirstOrDefault(x =>
                x.SystemName.Equals(systemNameToLoad, StringComparison.OrdinalIgnoreCase));

            if (selectedSystem != null)
            {
                SystemNameTextBox.Text = selectedSystem.SystemName;
                UpdateSystemImagePreview();

                // Load system folders
                if (selectedSystem.PrimarySystemFolder != null)
                    SystemFolderTextBox.Text = selectedSystem.PrimarySystemFolder;

                AdditionalFoldersListBox.Items.Clear();
                foreach (var folder in selectedSystem.SystemFolders.Skip(1)) AdditionalFoldersListBox.Items.Add(folder);

                SystemImageFolderTextBox.Text = selectedSystem.SystemImageFolder;

                FormatToSearchTextBox.Text = string.Join(", ", selectedSystem.FileFormatsToSearch);

                var extractFileBeforeLaunchValue = selectedSystem.ExtractFileBeforeLaunch ? "true" : "false";
                ExtractFileBeforeLaunchComboBox.SelectedItem = ExtractFileBeforeLaunchComboBox.Items
                    .Cast<ComboBoxItem>()
                    .FirstOrDefault(item => string.Equals(item.Content.ToString(), extractFileBeforeLaunchValue,
                        StringComparison.Ordinal));

                var groupByFolderValue = selectedSystem.GroupByFolder ? "true" : "false";
                GroupByFolderComboBox.SelectedItem = GroupByFolderComboBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(item =>
                        string.Equals(item.Content.ToString(), groupByFolderValue, StringComparison.Ordinal));
                var disableRecursiveSearchValue = selectedSystem.DisableRecursiveSearch ? "true" : "false";
                DisableRecursiveSearchComboBox.SelectedItem = DisableRecursiveSearchComboBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(item => string.Equals(item.Content.ToString(), disableRecursiveSearchValue,
                        StringComparison.Ordinal));

                FormatToLaunchTextBox.Text = string.Join(", ", selectedSystem.FileFormatsToLaunch);

                var emulators = selectedSystem.Emulators;
                if (emulators != null)
                {
                    // Populate fields with saved strings (including %BASEFOLDER% for location)
                    PopulateEmulatorFields(emulators.ElementAtOrDefault(0), Emulator1NameTextBox, Emulator1PathTextBox,
                        Emulator1ParametersTextBox, ReceiveANotificationOnEmulatorError1);
                    PopulateEmulatorFields(emulators.ElementAtOrDefault(1), Emulator2NameTextBox, Emulator2PathTextBox,
                        Emulator2ParametersTextBox, ReceiveANotificationOnEmulatorError2);
                    PopulateEmulatorFields(emulators.ElementAtOrDefault(2), Emulator3NameTextBox, Emulator3PathTextBox,
                        Emulator3ParametersTextBox, ReceiveANotificationOnEmulatorError3);
                    PopulateEmulatorFields(emulators.ElementAtOrDefault(3), Emulator4NameTextBox, Emulator4PathTextBox,
                        Emulator4ParametersTextBox, ReceiveANotificationOnEmulatorError4);
                    PopulateEmulatorFields(emulators.ElementAtOrDefault(4), Emulator5NameTextBox, Emulator5PathTextBox,
                        Emulator5ParametersTextBox, ReceiveANotificationOnEmulatorError5);
                }
                // else: ClearAllEmulatorFieldsInternal() already handled this at the beginning.

                // Try creating default folders based on the *current UI text*, which might contain %BASEFOLDER%
                var resolvedSystemFolder = PathHelper.ResolveRelativeToAppDirectory(SystemFolderTextBox.Text);
                TryCreateDefaultFolder(resolvedSystemFolder, Path.Combine(".", "roms", SystemNameTextBox.Text),
                    "SystemFolder"); // Pass resolved path

                var resolvedSystemImageFolder = PathHelper.ResolveRelativeToAppDirectory(SystemImageFolderTextBox.Text);
                TryCreateDefaultFolder(resolvedSystemImageFolder, Path.Combine(".", "images", SystemNameTextBox.Text),
                    "SystemImageFolder"); // Pass resolved path


                // Mark validity. CheckPath.IsValidPath will now handle %BASEFOLDER% internally.
                SetFieldValidationState(SystemFolderTextBox!,
                    CheckPath.IsValidPath(SystemFolderTextBox!.Text!) ||
                    string.IsNullOrWhiteSpace(SystemFolderTextBox.Text));
                SetFieldValidationState(SystemImageFolderTextBox,
                    CheckPath.IsValidPath(SystemImageFolderTextBox.Text) ||
                    string.IsNullOrWhiteSpace(SystemImageFolderTextBox.Text));
                SetFieldValidationState(Emulator1PathTextBox,
                    string.IsNullOrWhiteSpace(Emulator1PathTextBox.Text) ||
                    CheckPath.IsValidPath(Emulator1PathTextBox.Text));
                SetFieldValidationState(Emulator2PathTextBox,
                    string.IsNullOrWhiteSpace(Emulator2PathTextBox.Text) ||
                    CheckPath.IsValidPath(Emulator2PathTextBox.Text));
                SetFieldValidationState(Emulator3PathTextBox,
                    string.IsNullOrWhiteSpace(Emulator3PathTextBox.Text) ||
                    CheckPath.IsValidPath(Emulator3PathTextBox.Text));
                SetFieldValidationState(Emulator4PathTextBox,
                    string.IsNullOrWhiteSpace(Emulator4PathTextBox.Text) ||
                    CheckPath.IsValidPath(Emulator4PathTextBox.Text));
                SetFieldValidationState(Emulator5PathTextBox,
                    string.IsNullOrWhiteSpace(Emulator5PathTextBox.Text) ||
                    CheckPath.IsValidPath(Emulator5PathTextBox.Text));

                // Validate parameter fields. This uses ParameterValidator which is updated to handle %BASEFOLDER% etc.
                ValidateParameterFields();

                HelpUserTextBlock.Document.Blocks.Clear();
                _helpUserService.UpdateHelpUserTextBlock(HelpUserTextBlock, SystemNameTextBox.Text.Trim());
            }
            else
            {
                await _messageBox.SystemNotFoundInTheXmlMessageBoxAsync();
                ClearFieldsForNoSelection();
                DisableAllEditableFields();
                SaveSystemButton.IsEnabled = false;
                DeleteSystemButton.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method LoadSystemDetailsAsync");
        }
    }

    private static void PopulateEmulatorFields(Emulator? emulator, TextBox nameTextBox, TextBox pathTextBox,
        TextBox paramsTextBox, Selector notificationComboBox)
    {
        if (emulator != null)
        {
            nameTextBox.Text = emulator.EmulatorName;
            // Load the saved string directly into the UI, including %BASEFOLDER% if present
            pathTextBox.Text = emulator.EmulatorLocation;
            paramsTextBox.Text = emulator.EmulatorParameters;

            if (!string.IsNullOrEmpty(nameTextBox.Text))
            {
                var receiveNotificationValue = emulator.ReceiveANotificationOnEmulatorError ? "true" : "false";
                notificationComboBox.SelectedItem = notificationComboBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(item =>
                        string.Equals(item.Content.ToString(), receiveNotificationValue, StringComparison.Ordinal));
            }
            else
            {
                notificationComboBox.SelectedItem = null; // Name is empty, clear notification
            }
        }
    }

    // Accept a resolved path and compare against a default pattern
    private void TryCreateDefaultFolder(string? resolvedCurrentPath, string defaultPatternPathWithSystemName,
        string folderTypeForLog)
    {
        // Ensure systemName is not empty before forming the pattern path
        var systemName = SystemNameTextBox.Text; // Get the current system name from UI
        if (string.IsNullOrEmpty(systemName)) return;

        // Resolve the default pattern path for comparison
        var resolvedDefaultPatternPath = PathHelper.ResolveRelativeToAppDirectory(defaultPatternPathWithSystemName);

        // Only create if the current path matches the default pattern AND the directory doesn't exist
        // Also check if the resolved path is valid before attempting creation
        if (string.IsNullOrEmpty(resolvedCurrentPath) ||
            !resolvedCurrentPath.Equals(resolvedDefaultPatternPath, StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(resolvedCurrentPath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(resolvedCurrentPath); // Create the resolved path
        }
        catch (Exception ex)
        {
            // Notify developer
            _logger.Error(ex, $"Unable to create default {folderTypeForLog}: {resolvedCurrentPath}");
        }
    }
}