using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.Services.Favorites;
using SimpleLauncher.Avalonia.Services.PlayHistory;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.CheckPaths;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.SanitizeInputString;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Core.Services.UnifiedSettings;
using CoreMessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window for adding, editing, and deleting emulator system configurations (Expert Mode).
///     Avalonia port of SimpleLauncher's EditSystemWindow — same validation pipeline and save semantics,
///     but saves/deletes through Core's SystemConfigurationWriterService and reads through
///     the new SystemManagerService. No MahApps; OpenEmu-themed.
/// </summary>
public partial class EditSystemWindow : Window
{
    private static readonly char[] SplitSeparators = [',', '|', ';'];
    private readonly IConfiguration _configuration;
    private readonly FavoritesManager _favoritesManager;
    private readonly IFilePickerService _filePicker;
    private readonly AvaloniaHelpUserService _helpUserService;
    private readonly LocalizationService _localization;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly IParameterResolverService _parameterResolver;
    private readonly PlayHistoryManager _playHistoryManager;

    private readonly PlaySoundEffects _playSoundEffects;
    private readonly string? _preSelectedSystemName;
    private readonly SystemManagerService _systemManager;
    private readonly SettingsManagerService _settings;
    private readonly ISystemConfigurationWriterService _writer;
    private string? _originalSystemName;

    private List<SystemManagerConfig> _systems = [];

    public EditSystemWindow(
        PlaySoundEffects playSoundEffects,
        IConfiguration configuration,
        IMessageBoxLibraryService messageBox,
        ILogger logger,
        ISystemConfigurationWriterService writer,
        SystemManagerService systemManager,
        IFilePickerService filePicker,
        FavoritesManager favoritesManager,
        PlayHistoryManager playHistoryManager,
        IParameterResolverService parameterResolver,
        AvaloniaHelpUserService helpUserService,
        LocalizationService localization,
        SettingsManagerService settings,
        string? preSelectedSystemName = null)
    {
        InitializeComponent();
        DataContext = this;

        _playSoundEffects = playSoundEffects;
        _configuration = configuration;
        _messageBox = messageBox;
        _logger = logger;
        _writer = writer;
        _systemManager = systemManager;
        _filePicker = filePicker;
        _favoritesManager = favoritesManager;
        _playHistoryManager = playHistoryManager;
        _parameterResolver = parameterResolver;
        _helpUserService = helpUserService;
        _localization = localization;
        _settings = settings;
        _preSelectedSystemName = preSelectedSystemName;

        SaveSystemButton.IsEnabled = false;
        DeleteSystemButton.IsEnabled = false;

        // Localize the emergency return button (WPF DynamicResource ReturnButton parity)
        EmergencyButton.Content = _localization.GetString("ReturnButton");
        ToolTip.SetTip(EmergencyButton,
            _localization.GetString("ClickHereIfTheLoadingScreenIsStuckToReturnToTheMainMenu"));

        // Localize the System Help panel title (WPF DeveloperSuggestion label parity)
        HelpTitleTextBlock.Text = _localization.GetString("DeveloperSuggestion");
        ToolTip.SetTip(HelpTitleTextBlock,
            _localization.GetString("TooltipDeveloperSuggestionLabel"));

        ApplyExpanderSettings();
    }

    private async void Window_Opened(object? sender, EventArgs e)
    {
        try
        {
            await LoadSystemsAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading systems into Edit window");
        }
    }

    // ── Loading ───────────────────────────────────────────────────────

    private async Task LoadSystemsAsync()
    {
        SetLoadingState(true, _localization.GetString("Loadingsystems", "Loading systems..."));
        try
        {
            await Task.Yield();

            _systems = _systemManager.LoadSystems();

            if (_systems.Count == 0)
            {
                await _messageBox.SystemXmlNotFoundMessageBoxAsync();
                Close();
                return;
            }

            PopulateSystemNamesDropdown();

            if (!string.IsNullOrEmpty(_preSelectedSystemName)) SystemNameDropdown.SelectedItem = _preSelectedSystemName;
        }
        finally
        {
            // Closed on every path (including exceptions) so the overlay cannot stick.
            SetLoadingState(false);
        }
    }

    private void SetLoadingState(bool isLoading, string? message = null)
    {
        LoadingOverlay.IsVisible = isLoading;
        if (isLoading) LoadingText.Text = message ?? _localization.GetString("Loading", "Loading...");
    }

    /// <summary>
    ///     Applies the saved expander states (WPF EditSystemWindow.ApplyExpanderSettings parity).
    /// </summary>
    private void ApplyExpanderSettings()
    {
        AdditionalFoldersExpander.IsExpanded = _settings.AdditionalSystemFoldersExpanded;
        Emulator1Expander.IsExpanded = _settings.Emulator1Expanded;
        Emulator2Expander.IsExpanded = _settings.Emulator2Expanded;
        Emulator3Expander.IsExpanded = _settings.Emulator3Expanded;
        Emulator4Expander.IsExpanded = _settings.Emulator4Expanded;
        Emulator5Expander.IsExpanded = _settings.Emulator5Expanded;
    }

    private void EmergencyOverlayRelease_Click(object? sender, RoutedEventArgs e)
    {
        _playSoundEffects.PlayNotificationSound();
        LoadingOverlay.IsVisible = false;

        _logger.Debug("[Emergency] User forced overlay dismissal in EditSystemWindow");
    }

    private void PopulateSystemNamesDropdown()
    {
        if (_systems.Count == 0) return;

        var currentSelection = SystemNameDropdown.SelectedItem?.ToString();
        SystemNameDropdown.ItemsSource = _systems
            .Select(static s => s.SystemName)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();

        if (currentSelection != null && SystemNameDropdown.Items.Contains(currentSelection))
            SystemNameDropdown.SelectedItem = currentSelection;
    }

    private async void SystemNameDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            var currentSelectedSystemName = SystemNameDropdown.SelectedItem?.ToString();
            _originalSystemName = currentSelectedSystemName;

            if (currentSelectedSystemName == null)
            {
                ClearFieldsForNoSelection();
                DisableAllEditableFields();
                SaveSystemButton.IsEnabled = false;
                DeleteSystemButton.IsEnabled = false;
                StatusTextBlock.Text = _localization.GetString(
                    "EditSystemWindow_Select_a_system_to_edit_or_click_Add_New",
                    "Select a system to edit, or click Add New to create one.");
                ClearSystemHelp();
            }
            else
            {
                await LoadSystemDetails(currentSelectedSystemName);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method SystemNameDropdown_SelectionChanged");
        }
    }

    private async Task LoadSystemDetails(string systemNameToLoad)
    {
        try
        {
            ClearAllEmulatorFields();
            EnableFields();
            SaveSystemButton.IsEnabled = true;
            DeleteSystemButton.IsEnabled = true;

            var selectedSystem = _systems.FirstOrDefault(x =>
                x.SystemName.Equals(systemNameToLoad, StringComparison.OrdinalIgnoreCase));

            if (selectedSystem != null)
            {
                SystemNameTextBox.Text = selectedSystem.SystemName;
                StatusTextBlock.Text = $"{_localization.GetString("Editing", "Editing: ")}{selectedSystem.SystemName}";

                SystemFolderTextBox.Text = selectedSystem.PrimarySystemFolder ?? "";

                AdditionalFoldersListBox.Items.Clear();
                foreach (var folder in selectedSystem.SystemFolders.Skip(1)) AdditionalFoldersListBox.Items.Add(folder);

                SystemImageFolderTextBox.Text = selectedSystem.SystemImageFolder;

                FormatToSearchTextBox.Text = string.Join(", ", selectedSystem.FileFormatsToSearch);

                ExtractFileBeforeLaunchComboBox.SelectedItem = FindComboItem(ExtractFileBeforeLaunchComboBox,
                    selectedSystem.ExtractFileBeforeLaunch);
                GroupByFolderComboBox.SelectedItem = FindComboItem(GroupByFolderComboBox, selectedSystem.GroupByFolder);
                DisableRecursiveSearchComboBox.SelectedItem = FindComboItem(DisableRecursiveSearchComboBox,
                    selectedSystem.DisableRecursiveSearch);

                FormatToLaunchTextBox.Text = string.Join(", ", selectedSystem.FileFormatsToLaunch);

                var emulators = selectedSystem.Emulators;
                if (emulators != null)
                {
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

                var resolvedSystemFolder = PathHelper.ResolveRelativeToAppDirectory(SystemFolderTextBox.Text);
                TryCreateDefaultFolder(resolvedSystemFolder, Path.Combine(".", "roms", GetSafeSystemFileName()));

                var resolvedSystemImageFolder = PathHelper.ResolveRelativeToAppDirectory(SystemImageFolderTextBox.Text);
                TryCreateDefaultFolder(resolvedSystemImageFolder, Path.Combine(".", "images", GetSafeSystemFileName()));

                UpdateSystemImagePreview();

                SetFieldValidationState(SystemFolderTextBox,
                    CheckPath.IsValidPath(SystemFolderTextBox.Text) ||
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

                // Update the System Help panel (WPF UpdateHelpUserTextBlock parity)
                UpdateSystemHelp();
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
            _logger.Error(ex, "Error in the method LoadSystemDetails");
        }
    }

    private static ComboBoxItem? FindComboItem(ComboBox combo, bool value)
    {
        var target = value ? "true" : "false";
        return combo.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item?.Content?.ToString(), target, StringComparison.Ordinal));
    }

    private static void PopulateEmulatorFields(Emulator? emulator, TextBox nameTextBox, TextBox pathTextBox,
        TextBox paramsTextBox, ComboBox notificationComboBox)
    {
        if (emulator == null) return;

        nameTextBox.Text = emulator.EmulatorName;
        pathTextBox.Text = emulator.EmulatorLocation;
        paramsTextBox.Text = emulator.EmulatorParameters;

        if (!string.IsNullOrEmpty(nameTextBox.Text))
        {
            var target = emulator.ReceiveANotificationOnEmulatorError ? "true" : "false";
            notificationComboBox.SelectedItem = notificationComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item?.Content?.ToString(), target, StringComparison.Ordinal));
        }
        else
        {
            notificationComboBox.SelectedItem = null;
        }
    }

    private void TryCreateDefaultFolder(string? resolvedCurrentPath, string defaultPatternPathWithSystemName)
    {
        var systemName = SystemNameTextBox.Text;
        if (string.IsNullOrEmpty(systemName)) return;

        var resolvedDefaultPatternPath = PathHelper.ResolveRelativeToAppDirectory(defaultPatternPathWithSystemName);
        if (string.IsNullOrEmpty(resolvedCurrentPath) ||
            !resolvedCurrentPath.Equals(resolvedDefaultPatternPath, StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(resolvedCurrentPath))
        {
            return;
        }

        // Defense in depth (AV-13): only ever auto-create folders inside the app
        // directory, even if a crafted system name slipped into the default pattern.
        // PathHelper normalizes the trailing separator that BaseDirectory carries on
        // Windows (the previous inline check rejected even in-app paths because of it).
        if (!PathHelper.IsPathContainedInBaseFolder(resolvedCurrentPath, AppDomain.CurrentDomain.BaseDirectory))
        {
            _logger.Information(
                "Not auto-creating a folder outside the app directory: {Path}", resolvedCurrentPath);
            return;
        }

        try
        {
            Directory.CreateDirectory(resolvedCurrentPath);
        }
        catch (Exception ex)
        {
            // Environment issue (permissions, read-only install folder), not a code bug.
            _logger.Information(ex, "Unable to create default folder: {Path}", resolvedCurrentPath);
        }
    }

    /// <summary>
    ///     Returns the system name sanitized for use as a file/folder name segment.
    ///     Prevents directory traversal (AV-13): raw <c>SystemNameTextBox.Text</c> must
    ///     never be passed to <c>Path.Combine</c>, since values like <c>../../evil</c>
    ///     or rooted paths would escape <c>images/systems</c> (and the default
    ///     <c>roms/</c> / <c>images/</c> patterns).
    /// </summary>
    private string GetSafeSystemFileName()
    {
        return SanitizeInputSystemName.SanitizeFolderName(SystemNameTextBox.Text?.Trim() ?? "");
    }

    /// <summary>
    ///     Resolves <c>images/systems/&lt;systemName&gt;&lt;ext&gt;</c> and returns
    ///     <c>null</c> when the resolved path would escape the images directory.
    /// </summary>
    private static string? TryResolveSystemImagePath(string imagesSystemsDir, string safeSystemName, string extension)
    {
        var candidate = Path.GetFullPath(Path.Combine(imagesSystemsDir, $"{safeSystemName}{extension}"));
        var baseDirFull = Path.GetFullPath(imagesSystemsDir);
        if (!candidate.StartsWith(baseDirFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return null;

        return candidate;
    }

    // ── Add / Delete / Close ──────────────────────────────────────────

    private async void AddSystemButton_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            _originalSystemName = null;

            EnableFields();
            ClearFields();
            SystemNameDropdown.SelectedItem = null;
            ClearFieldsForNoSelection();
            EnableFields();

            UpdateSystemImagePreview();
            ClearSystemHelp();

            SaveSystemButton.IsEnabled = true;
            DeleteSystemButton.IsEnabled = false;
            StatusTextBlock.Text =
                _localization.GetString("AddinganewsystemfillinthefieldsandclickSave",
                    "Adding a new system — fill in the fields and click Save.");

            await _messageBox.YouCanAddANewSystemMessageBoxAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method AddSystemButton_ClickAsync");
        }
    }

    private async void DeleteSystemButton_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            ClearSystemHelp();

            if (SystemNameDropdown.SelectedItem == null)
            {
                await _messageBox.SelectASystemToDeleteMessageBoxAsync();
                return;
            }

            var selectedSystemName = SystemNameDropdown.SelectedItem.ToString()!;

            var result = await _messageBox.AreYouSureDoYouWantToDeleteThisSystemMessageBoxAsync();
            if (result != CoreMessageBoxResult.Yes) return;

            await _writer.DeleteSystemAsync(selectedSystemName);
            _systemManager.InvalidateCache();
            _playSoundEffects.PlayNotificationSound();

            await LoadSystemsAsync();
            if (SystemNameDropdown.Items.Count == 0 || SystemNameDropdown.SelectedItem == null)
                PopulateSystemNamesDropdown();

            StatusTextBlock.Text =
                $"{_localization.GetString("Systemdeleted", "System deleted: ")}{selectedSystemName}";
            await _messageBox.SystemHasBeenDeletedMessageBoxAsync(selectedSystemName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method DeleteSystemButton_ClickAsync");
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        // Save expander states (WPF EditSystem_Closing parity)
        _settings.AdditionalSystemFoldersExpanded = AdditionalFoldersExpander.IsExpanded;
        _settings.Emulator1Expanded = Emulator1Expander.IsExpanded;
        _settings.Emulator2Expanded = Emulator2Expander.IsExpanded;
        _settings.Emulator3Expanded = Emulator3Expander.IsExpanded;
        _settings.Emulator4Expanded = Emulator4Expander.IsExpanded;
        _settings.Emulator5Expanded = Emulator5Expander.IsExpanded;
        _ = _settings.SaveAsync();

        // Create a backup of the system configuration before closing:
        // settings.dat in the database path, system.xml in the legacy path.
        try
        {
            if (UnifiedSettingsDatabase.IsValidDatabase())
            {
                var dbPath = UnifiedSettingsDatabase.GetDatabasePath();
                var dbFolder = Path.GetDirectoryName(dbPath);
                if (dbFolder is not null && File.Exists(dbPath))
                {
                    var backupFileName = $"settings_backup{DateTime.Now:yyyyMMdd_HHmmss}.dat";
                    File.Copy(dbPath, Path.Combine(dbFolder, backupFileName), true);
                }

                return;
            }

            var appFolderPath = AppDomain.CurrentDomain.BaseDirectory;
            var sourceFilePath = Path.Combine(appFolderPath, "system.xml");
            if (!File.Exists(sourceFilePath)) return;

            var backupXmlFileName = $"system_backup{DateTime.Now:yyyyMMdd_HHmmss}.xml";
            File.Copy(sourceFilePath, Path.Combine(appFolderPath, backupXmlFileName), true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method EditSystem_Closing");
        }
    }

    // ── Field state helpers ───────────────────────────────────────────

    private void EnableFields()
    {
        SetAllEditableFields(true);
    }

    private void DisableAllEditableFields()
    {
        SetAllEditableFields(false);
    }

    private void SetAllEditableFields(bool enabled)
    {
        SystemNameTextBox.IsEnabled = enabled;
        SystemFolderTextBox.IsEnabled = enabled;
        AdditionalFoldersListBox.IsEnabled = enabled;
        SystemImageFolderTextBox.IsEnabled = enabled;
        FormatToSearchTextBox.IsEnabled = enabled;
        FormatToLaunchTextBox.IsEnabled = enabled;
        ExtractFileBeforeLaunchComboBox.IsEnabled = enabled;
        GroupByFolderComboBox.IsEnabled = enabled;
        DisableRecursiveSearchComboBox.IsEnabled = enabled;

        foreach (var tb in new[]
                 {
                     Emulator1NameTextBox, Emulator1PathTextBox, Emulator1ParametersTextBox,
                     Emulator2NameTextBox, Emulator2PathTextBox, Emulator2ParametersTextBox,
                     Emulator3NameTextBox, Emulator3PathTextBox, Emulator3ParametersTextBox,
                     Emulator4NameTextBox, Emulator4PathTextBox, Emulator4ParametersTextBox,
                     Emulator5NameTextBox, Emulator5PathTextBox, Emulator5ParametersTextBox
                 })
        {
            tb.IsEnabled = enabled;
        }

        foreach (var combo in new[]
                 {
                     ReceiveANotificationOnEmulatorError1, ReceiveANotificationOnEmulatorError2,
                     ReceiveANotificationOnEmulatorError3, ReceiveANotificationOnEmulatorError4,
                     ReceiveANotificationOnEmulatorError5
                 })
        {
            combo.IsEnabled = enabled;
        }
    }

    private void ClearFields()
    {
        SystemNameTextBox.Text = "";
        SystemFolderTextBox.Text = "";
        AdditionalFoldersListBox.Items.Clear();
        SystemImageFolderTextBox.Text = "";
        FormatToSearchTextBox.Text = "";
        FormatToLaunchTextBox.Text = "";
        ExtractFileBeforeLaunchComboBox.SelectedItem = null;
        GroupByFolderComboBox.SelectedItem = null;
        DisableRecursiveSearchComboBox.SelectedItem = null;

        ClearAllEmulatorFields();
    }

    private void ClearFieldsForNoSelection()
    {
        ClearFields();
    }

    private void ClearAllEmulatorFields()
    {
        ClearEmulator(Emulator1NameTextBox, Emulator1PathTextBox, Emulator1ParametersTextBox,
            ReceiveANotificationOnEmulatorError1);
        ClearEmulator(Emulator2NameTextBox, Emulator2PathTextBox, Emulator2ParametersTextBox,
            ReceiveANotificationOnEmulatorError2);
        ClearEmulator(Emulator3NameTextBox, Emulator3PathTextBox, Emulator3ParametersTextBox,
            ReceiveANotificationOnEmulatorError3);
        ClearEmulator(Emulator4NameTextBox, Emulator4PathTextBox, Emulator4ParametersTextBox,
            ReceiveANotificationOnEmulatorError4);
        ClearEmulator(Emulator5NameTextBox, Emulator5PathTextBox, Emulator5ParametersTextBox,
            ReceiveANotificationOnEmulatorError5);
    }

    private static void ClearEmulator(TextBox name, TextBox path, TextBox parameters, ComboBox notification)
    {
        name.Text = "";
        name.ClearValue(ForegroundProperty);
        path.Text = "";
        path.ClearValue(ForegroundProperty);
        parameters.Text = "";
        parameters.ClearValue(ForegroundProperty);
        notification.SelectedItem = null;
    }

    private static void SetFieldValidationState(TextBox control, bool isValid)
    {
        if (isValid)
            control.ClearValue(ForegroundProperty);
        else
            control.Foreground = Brushes.Red;
    }

    // ── Folder / file pickers ─────────────────────────────────────────

    private async void ChooseSystemFolder(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folder = await _filePicker.OpenFolderAsync("Please select the System Folder");
            if (!string.IsNullOrEmpty(folder))
            {
                SystemFolderTextBox.Text = folder;
                SetFieldValidationState(SystemFolderTextBox, true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method ChooseSystemFolder");
        }
    }

    private async void ChooseSystemImageFolder(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folder = await _filePicker.OpenFolderAsync("Please select the System Image Folder");
            if (!string.IsNullOrEmpty(folder))
            {
                SystemImageFolderTextBox.Text = folder.Trim();
                SetFieldValidationState(SystemImageFolderTextBox, true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method ChooseSystemImageFolder");
        }
    }

    private void ChooseEmulator1Path(object sender, RoutedEventArgs e)
    {
        ChooseEmulatorPath(Emulator1PathTextBox);
    }

    private void ChooseEmulator2Path(object sender, RoutedEventArgs e)
    {
        ChooseEmulatorPath(Emulator2PathTextBox);
    }

    private void ChooseEmulator3Path(object sender, RoutedEventArgs e)
    {
        ChooseEmulatorPath(Emulator3PathTextBox);
    }

    private void ChooseEmulator4Path(object sender, RoutedEventArgs e)
    {
        ChooseEmulatorPath(Emulator4PathTextBox);
    }

    private void ChooseEmulator5Path(object sender, RoutedEventArgs e)
    {
        ChooseEmulatorPath(Emulator5PathTextBox);
    }

    private async void ChooseEmulatorPath(TextBox pathTextBox)
    {
        try
        {
            var path = await _filePicker.OpenFileAsync(
                "Select Emulator",
                "Executable Files (*.exe;*.bat)|*.exe;*.bat");
            if (!string.IsNullOrEmpty(path))
            {
                pathTextBox.Text = path;
                SetFieldValidationState(pathTextBox, true);
            }

            // Update the System Help panel (WPF ChooseEmulatorPath parity)
            UpdateSystemHelp();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method ChooseEmulatorPath");
        }
    }

    // ── System Help panel (WPF HelpUserTextBlock parity) ──────────────

    /// <summary>
    ///     Refreshes the right-side System Help panel from the name currently in
    ///     SystemNameTextBox (WPF UpdateHelpUserTextBlock parity).
    /// </summary>
    private void UpdateSystemHelp()
    {
        var systemName = SystemNameTextBox.Text?.Trim() ?? string.Empty;
        _helpUserService.UpdateHelpTextBlock(SystemHelpTextBlock, systemName);
    }

    /// <summary>
    ///     Clears the right-side System Help panel (WPF HelpUserTextBlock.Document.Blocks.Clear() parity).
    /// </summary>
    private void ClearSystemHelp()
    {
        SystemHelpTextBlock.Inlines?.Clear();
    }

    private void SystemNameTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        // Update System Help when the user edits/renames the system name
        // (WPF SystemNameTextBox_LostFocus parity).
        UpdateSystemHelp();
    }

    private async void AddAdditionalFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folder = await _filePicker.OpenFolderAsync("Please select an additional System Folder");
            if (!string.IsNullOrEmpty(folder)) AdditionalFoldersListBox.Items.Add(folder);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method AddAdditionalFolder_Click");
        }
    }

    private void RemoveAdditionalFolder_Click(object sender, RoutedEventArgs e)
    {
        if (AdditionalFoldersListBox.SelectedItem is string selected) AdditionalFoldersListBox.Items.Remove(selected);
    }

    // ── System image picker + preview (ported from EditSystemWindow.xaml.cs) ──

    private async void ChooseSystemImageButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            var systemName = SystemNameTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(systemName))
            {
                await _messageBox.SystemNameRequiredBeforeChoosingImageMessageBoxAsync();
                return;
            }

            var sourceFilePath = await _filePicker.OpenFileAsync(
                "Select System Image",
                "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg");
            if (string.IsNullOrEmpty(sourceFilePath)) return;

            var extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            if (extension is not (".png" or ".jpg" or ".jpeg"))
            {
                await _messageBox.InvalidImageFormatMessageBoxAsync();
                return;
            }

            var imagesSystemsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "systems");
            try
            {
                if (!Directory.Exists(imagesSystemsDir)) Directory.CreateDirectory(imagesSystemsDir);

                var safeSystemName = GetSafeSystemFileName();
                var destFilePath = TryResolveSystemImagePath(imagesSystemsDir, safeSystemName, extension);
                if (destFilePath is null)
                {
                    _logger.Error("Refusing to copy system image outside the images directory for system name: {SystemName}",
                        systemName);
                    await _messageBox.FailedToCopySystemImageMessageBoxAsync(systemName);
                    return;
                }

                SystemImagePreview.Source = null; // Release any file lock before overwriting

                const int maxRetries = 3;
                const int retryDelayMs = 500;
                for (var attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        File.Copy(sourceFilePath, destFilePath, true);
                        break;
                    }
                    catch (IOException) when (attempt < maxRetries)
                    {
                        await Task.Delay(retryDelayMs * attempt);
                    }
                    catch (IOException ex)
                    {
                        // Expected user-environment condition (the destination is locked by another
                        // program, e.g. another running SimpleLauncher instance, an image viewer or
                        // the OS file manager preview): not a bug, keep it out of the bug report.
                        _logger.Information(ex,
                            "Could not copy the system image to '{DestFilePath}': the file is in use by another program. Ask the user to close the other program and retry",
                            destFilePath);
                        await _messageBox.FailedToCopySystemImageMessageBoxAsync(ex.Message);
                        return;
                    }
                }

                UpdateSystemImagePreview();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error copying system image");
                await _messageBox.FailedToCopySystemImageMessageBoxAsync(ex.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error copying system image");
        }
    }

    private void UpdateSystemImagePreview()
    {
        var systemName = SystemNameTextBox.Text?.Trim() ?? "";
        var imagesSystemsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "systems");
        string? imagePath = null;

        if (!string.IsNullOrEmpty(systemName))
        {
            var safeSystemName = GetSafeSystemFileName();
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
            {
                var path = TryResolveSystemImagePath(imagesSystemsDir, safeSystemName, ext);
                if (path is not null && File.Exists(path))
                {
                    imagePath = path;
                    break;
                }
            }
        }

        imagePath ??= Path.Combine(imagesSystemsDir, "default.png");

        SystemImagePreview.Source = null; // Release any file lock before swapping
        if (!File.Exists(imagePath)) return;

        try
        {
            using var stream = File.OpenRead(imagePath);
            SystemImagePreview.Source = Bitmap.DecodeToWidth(stream, 300);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading system image preview: {Path}", imagePath);
            SystemImagePreview.Source = null;
        }
    }

    // ── Help link (ported from EditSystemWindow.HelpLink_ClickAsync) ─────────

    private async void HelpLink_ClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            var searchUrl = _configuration.GetValue<string>("WikiParametersUrl")
                            ?? "https://github.com/purelogiccode/SimpleLauncher/wiki/parameters/";
            // AV-15: config-controlled URL — validate the scheme and open via the
            // cross-platform launcher instead of assuming Windows shell-execute.
            if (!await ExternalLinkHelper.TryOpenUrlAsync(searchUrl, GetTopLevel(this)))
            {
                _logger.Error("Unable to open help URL: invalid or unreachable URL");
                await _messageBox.ErrorOpeningUrlMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method HelpLink_ClickAsync");
        }
    }

    // ── Suggest Parameters (ported from EditSystemWindow.SuggestParametersAsync) ──

    private async void SuggestEmulator1Parameters_ClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            await SuggestParametersAsync(Emulator1NameTextBox.Text, Emulator1PathTextBox.Text,
                Emulator1ParametersTextBox.Text);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method SuggestEmulator1Parameters_ClickAsync");
        }
    }

    private async void SuggestEmulator2Parameters_ClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            await SuggestParametersAsync(Emulator2NameTextBox.Text, Emulator2PathTextBox.Text,
                Emulator2ParametersTextBox.Text);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method SuggestEmulator2Parameters_ClickAsync");
        }
    }

    private async void SuggestEmulator3Parameters_ClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            await SuggestParametersAsync(Emulator3NameTextBox.Text, Emulator3PathTextBox.Text,
                Emulator3ParametersTextBox.Text);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method SuggestEmulator3Parameters_ClickAsync");
        }
    }

    private async void SuggestEmulator4Parameters_ClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            await SuggestParametersAsync(Emulator4NameTextBox.Text, Emulator4PathTextBox.Text,
                Emulator4ParametersTextBox.Text);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method SuggestEmulator4Parameters_ClickAsync");
        }
    }

    private async void SuggestEmulator5Parameters_ClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            await SuggestParametersAsync(Emulator5NameTextBox.Text, Emulator5PathTextBox.Text,
                Emulator5ParametersTextBox.Text);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method SuggestEmulator5Parameters_ClickAsync");
        }
    }

    private async Task SuggestParametersAsync(string? emulatorName, string? emulatorPath, string? currentParameters)
    {
        var successTitle = _localization.GetString("ParameterResolverSuccess", "Parameter Suggestion");
        var errorTitle = _localization.GetString("ParameterResolverError", "Error");
        var errorMessage =
            _localization.GetString("ErrorProcessingRequest", "There was an error processing your request.");
        var confirmMessage =
            _localization.GetString("ParameterResolverConfirmApply", "Do you want to apply this parameter?");

        if (string.IsNullOrWhiteSpace(emulatorName))
        {
            await _messageBox.WarningMessageBoxAsync(
                _localization.GetString("ParameterResolverEnterEmulatorName", "Please enter an emulator name first."));
            return;
        }

        if (string.IsNullOrWhiteSpace(SystemNameTextBox.Text))
        {
            await _messageBox.WarningMessageBoxAsync(
                _localization.GetString("ParameterResolverEnterSystemName", "Please enter a system name first."));
            return;
        }

        SetLoadingState(true,
            _localization.GetString("ParameterResolverLoading", "Resolving parameters, please wait..."));

        try
        {
            var request = new ParameterResolverRequest
            {
                SystemName = SystemNameTextBox.Text?.Trim() ?? "",
                SystemFolder = SystemFolderTextBox.Text?.Trim() ?? "",
                FileFormatsToSearch = SplitAndTrim(FormatToSearchTextBox.Text) ?? [],
                ExtractFileBeforeLaunch = ExtractFileBeforeLaunchComboBox.SelectedItem is ComboBoxItem extractItem
                                          && bool.TryParse(extractItem.Content?.ToString(), out var extractVal) &&
                                          extractVal,
                FileFormatsToLaunch = SplitAndTrim(FormatToLaunchTextBox.Text) ?? [],
                GroupByFolder = GroupByFolderComboBox.SelectedItem is ComboBoxItem groupItem
                                && string.Equals(groupItem.Content?.ToString(), "true",
                                    StringComparison.OrdinalIgnoreCase),
                DisableRecursiveSearch = DisableRecursiveSearchComboBox.SelectedItem is ComboBoxItem disableItem
                                         && string.Equals(disableItem.Content?.ToString(), "true",
                                             StringComparison.OrdinalIgnoreCase),
                EmulatorName = emulatorName.Trim(),
                EmulatorPath = emulatorPath?.Trim() ?? "",
                CurrentParameters = currentParameters?.Trim() ?? ""
            };

            var result = await _parameterResolver.ResolveParametersAsync(request);

            if (result != null)
            {
                var suggestedParam = result.SuggestedParameter;
                var explanation = result.Explanation;

                if (!string.IsNullOrWhiteSpace(suggestedParam) &&
                    suggestedParam.StartsWith("Explanation:", StringComparison.OrdinalIgnoreCase))
                {
                    var explanationFromParam = suggestedParam["Explanation:".Length..].Trim();
                    if (string.IsNullOrEmpty(explanation) ||
                        !explanation.Equals(explanationFromParam, StringComparison.OrdinalIgnoreCase))
                    {
                        explanation = explanationFromParam;
                    }

                    suggestedParam = "";
                }

                var dialogMessage = $"{confirmMessage}\n\n{suggestedParam}";
                if (!string.IsNullOrEmpty(explanation)) dialogMessage += $"\n\nExplanation: {explanation}";

                var applyResult = await _messageBox.CustomQuestionMessageBoxAsync(successTitle, dialogMessage);

                if (applyResult)
                {
                    var textBox = FindParametersTextBox(emulatorName);
                    if (textBox is not null) textBox.Text = suggestedParam;
                }
            }
            else
            {
                await _messageBox.CustomErrorMessageBoxAsync(errorMessage, errorTitle);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error calling ParameterResolver API");
            await _messageBox.CustomErrorMessageBoxAsync(errorMessage, errorTitle);
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private TextBox? FindParametersTextBox(string emulatorName)
    {
        if (string.Equals(emulatorName, Emulator1NameTextBox.Text, StringComparison.Ordinal))
            return Emulator1ParametersTextBox;
        if (string.Equals(emulatorName, Emulator2NameTextBox.Text, StringComparison.Ordinal))
            return Emulator2ParametersTextBox;
        if (string.Equals(emulatorName, Emulator3NameTextBox.Text, StringComparison.Ordinal))
            return Emulator3ParametersTextBox;
        if (string.Equals(emulatorName, Emulator4NameTextBox.Text, StringComparison.Ordinal))
            return Emulator4ParametersTextBox;
        if (string.Equals(emulatorName, Emulator5NameTextBox.Text, StringComparison.Ordinal))
            return Emulator5ParametersTextBox;

        return null;
    }

    private static List<string>? SplitAndTrim(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return text.Split(SplitSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    // ── Save pipeline (ported from EditSystemWindow.SaveSystem.cs) ────

    private async void SaveSystemButton_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            TrimInputValues(out var systemNameText, out var systemFolderText, out var systemImageFolderText,
                out var formatToSearchText, out var formatToLaunchText,
                out var emulator1NameText, out var emulator2NameText, out var emulator3NameText,
                out var emulator4NameText, out var emulator5NameText,
                out var emulator1LocationText, out var emulator2LocationText, out var emulator3LocationText,
                out var emulator4LocationText, out var emulator5LocationText,
                out var emulator1ParametersText, out var emulator2ParametersText, out var emulator3ParametersText,
                out var emulator4ParametersText, out var emulator5ParametersText);

            // Validate SystemName for invalid characters before sanitizing
            if (SanitizeInputSystemName.ContainsInvalidCharacters(systemNameText, out var invalidChars))
            {
                var invalidCharsStr = string.Join(", ", invalidChars.Select(static c => $"'{c}'"));
                await _messageBox.InvalidSystemNameCharactersMessageBoxAsync(invalidCharsStr);
                SystemNameTextBox.Foreground = Brushes.Red;
                return;
            }

            systemNameText = SanitizeInputSystemName.SanitizeFolderName(systemNameText);
            SystemNameTextBox.Text = systemNameText;

            // Collect all system folders
            var allSystemFolders = new List<string> { systemFolderText };
            allSystemFolders.AddRange(AdditionalFoldersListBox.Items.Cast<string>().Select(static f => f.Trim()));
            allSystemFolders = allSystemFolders.Where(static f => !string.IsNullOrWhiteSpace(f)).ToList();

            // Apply %BASEFOLDER% prefix to relative paths
            allSystemFolders = allSystemFolders.ConvertAll(MaybeAddBaseFolderPrefix);
            systemImageFolderText = MaybeAddBaseFolderPrefix(systemImageFolderText);
            emulator1LocationText = MaybeAddBaseFolderPrefix(emulator1LocationText);
            emulator2LocationText = MaybeAddBaseFolderPrefix(emulator2LocationText);
            emulator3LocationText = MaybeAddBaseFolderPrefix(emulator3LocationText);
            emulator4LocationText = MaybeAddBaseFolderPrefix(emulator4LocationText);
            emulator5LocationText = MaybeAddBaseFolderPrefix(emulator5LocationText);

            // Update UI with processed values
            SystemFolderTextBox.Text = allSystemFolders.FirstOrDefault() ?? "";
            AdditionalFoldersListBox.Items.Clear();
            foreach (var folder in allSystemFolders.Skip(1)) AdditionalFoldersListBox.Items.Add(folder);

            SystemImageFolderTextBox.Text = systemImageFolderText;
            Emulator1PathTextBox.Text = emulator1LocationText;
            Emulator2PathTextBox.Text = emulator2LocationText;
            Emulator3PathTextBox.Text = emulator3LocationText;
            Emulator4PathTextBox.Text = emulator4LocationText;
            Emulator5PathTextBox.Text = emulator5LocationText;

            // Validate paths
            var firstFolder = allSystemFolders.FirstOrDefault() ?? "";
            ValidatePaths(firstFolder, systemImageFolderText,
                emulator1LocationText, emulator2LocationText, emulator3LocationText,
                emulator4LocationText, emulator5LocationText,
                out var isSystemFolderValid, out var isSystemImageFolderValid,
                out var isEmulator1LocationValid, out var isEmulator2LocationValid,
                out var isEmulator3LocationValid, out var isEmulator4LocationValid,
                out var isEmulator5LocationValid);

            HandleValidationAlerts(isSystemFolderValid, isSystemImageFolderValid,
                isEmulator1LocationValid, isEmulator2LocationValid, isEmulator3LocationValid,
                isEmulator4LocationValid, isEmulator5LocationValid);

            if (!isSystemFolderValid || !isSystemImageFolderValid) return;

            if (await ValidateSystemNameAsync(systemNameText)) return;

            var systemFolderResult = await ValidateSystemFolderAsync(systemNameText, firstFolder);
            if (systemFolderResult.IsFailed) return;

            firstFolder = systemFolderResult.FolderText;
            if (allSystemFolders.Count > 0)
                allSystemFolders[0] = firstFolder;
            else
                allSystemFolders.Add(firstFolder);

            var imageFolderResult = await ValidateSystemImageFolderAsync(systemNameText, systemImageFolderText);
            if (imageFolderResult.IsFailed) return;

            systemImageFolderText = imageFolderResult.FolderText;

            var extractFileBeforeLaunch = ExtractFileBeforeLaunchComboBox.SelectedItem is ComboBoxItem extractItem
                                          && bool.TryParse(extractItem.Content?.ToString(), out var extractVal) &&
                                          extractVal;

            var groupByFolder = GroupByFolderComboBox.SelectedItem is ComboBoxItem groupItem
                                && string.Equals(groupItem.Content?.ToString(), "true",
                                    StringComparison.OrdinalIgnoreCase);
            var disableRecursiveSearch = DisableRecursiveSearchComboBox.SelectedItem is ComboBoxItem disableItem
                                         && string.Equals(disableItem.Content?.ToString(), "true",
                                             StringComparison.OrdinalIgnoreCase);

            var formatSearchResult = await ValidateFormatToSearchAsync(formatToSearchText, extractFileBeforeLaunch);
            if (formatSearchResult.IsFailed)
            {
                FormatToSearchTextBox.Foreground = Brushes.Red;
                return;
            }

            FormatToSearchTextBox.ClearValue(ForegroundProperty);

            var formatsToSearch = formatSearchResult.Formats;

            var formatLaunchResult = await ValidateFormatToLaunchAsync(formatToLaunchText, extractFileBeforeLaunch);
            if (formatLaunchResult.IsFailed) return;

            var formatsToLaunch = formatLaunchResult.Formats;

            if (await ValidateEmulator1NameAsync(emulator1NameText)) return;

            if (await ValidateEmulatorLocationAsync(emulator1NameText, emulator1LocationText, formatsToSearch, 1))
            {
                Emulator1PathTextBox.Foreground = Brushes.Red;
                return;
            }

            if (await ValidateEmulatorLocationAsync(emulator2NameText, emulator2LocationText, formatsToSearch, 2))
            {
                Emulator2PathTextBox.Foreground = Brushes.Red;
                return;
            }

            if (await ValidateEmulatorLocationAsync(emulator3NameText, emulator3LocationText, formatsToSearch, 3))
            {
                Emulator3PathTextBox.Foreground = Brushes.Red;
                return;
            }

            if (await ValidateEmulatorLocationAsync(emulator4NameText, emulator4LocationText, formatsToSearch, 4))
            {
                Emulator4PathTextBox.Foreground = Brushes.Red;
                return;
            }

            if (await ValidateEmulatorLocationAsync(emulator5NameText, emulator5LocationText, formatsToSearch, 5))
            {
                Emulator5PathTextBox.Foreground = Brushes.Red;
                return;
            }

            if (await CheckPathsAsync(isSystemFolderValid, isSystemImageFolderValid,
                    isEmulator1LocationValid, isEmulator2LocationValid, isEmulator3LocationValid,
                    isEmulator4LocationValid, isEmulator5LocationValid))
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
                    if (result == CoreMessageBoxResult.No) return;
                }
            }

            string[] parameterTexts =
            [
                emulator1ParametersText, emulator2ParametersText, emulator3ParametersText,
                emulator4ParametersText, emulator5ParametersText
            ];
            string[] allEmulatorLocationTexts =
            [
                emulator1LocationText, emulator2LocationText, emulator3LocationText,
                emulator4LocationText, emulator5LocationText
            ];

            var receiveNotification1 =
                ReceiveANotificationOnEmulatorError1.SelectedItem is not ComboBoxItem { Content: not null } item1
                || string.Equals(item1.Content.ToString(), "true", StringComparison.Ordinal);
            var receiveNotification2 =
                ReceiveANotificationOnEmulatorError2.SelectedItem is not ComboBoxItem { Content: not null } item2
                || string.Equals(item2.Content.ToString(), "true", StringComparison.Ordinal);
            var receiveNotification3 =
                ReceiveANotificationOnEmulatorError3.SelectedItem is not ComboBoxItem { Content: not null } item3
                || string.Equals(item3.Content.ToString(), "true", StringComparison.Ordinal);
            var receiveNotification4 =
                ReceiveANotificationOnEmulatorError4.SelectedItem is not ComboBoxItem { Content: not null } item4
                || string.Equals(item4.Content.ToString(), "true", StringComparison.Ordinal);
            var receiveNotification5 =
                ReceiveANotificationOnEmulatorError5.SelectedItem is not ComboBoxItem { Content: not null } item5
                || string.Equals(item5.Content.ToString(), "true", StringComparison.Ordinal);

            var emulators = new List<Emulator>();
            var emulatorNames = new HashSet<string>(StringComparer.Ordinal);

            // Emulator 1 (name required)
            if (!string.IsNullOrEmpty(emulator1NameText))
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

            // Emulators 2–5 (name required only when location or parameters provided)
            string[] nameTexts = [emulator2NameText, emulator3NameText, emulator4NameText, emulator5NameText];
            bool[] receiveNotifications =
                [receiveNotification2, receiveNotification3, receiveNotification4, receiveNotification5];

            for (var i = 0; i < nameTexts.Length; i++)
            {
                var currentEmulatorName = nameTexts[i];
                var currentEmulatorLocation = allEmulatorLocationTexts[i + 1];
                var currentEmulatorParameters = parameterTexts[i + 1];
                var currentReceiveNotification = receiveNotifications[i];

                if (!string.IsNullOrEmpty(currentEmulatorLocation) || !string.IsNullOrEmpty(currentEmulatorParameters))
                {
                    if (string.IsNullOrEmpty(currentEmulatorName))
                    {
                        await _messageBox.EmulatorNameRequiredMessageBoxAsync(i + 2);
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

            var isUpdate = !string.IsNullOrEmpty(_originalSystemName)
                           && SystemNameDropdown.SelectedItem != null
                           && _originalSystemName.Equals(SystemNameDropdown.SelectedItem.ToString(),
                               StringComparison.OrdinalIgnoreCase);
            var originalSystemNameToUse = isUpdate ? _originalSystemName : systemNameText;

            var systemToSave = new SystemManagerConfig
            {
                SystemName = systemNameText,
                SystemFolders = allSystemFolders,
                SystemImageFolder = systemImageFolderText,
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
                StatusTextBlock.Text = _localization.GetString("Savingsystem", "Saving system...");

                await _writer.SaveSystemAsync(systemToSave, originalSystemNameToUse);
                _systemManager.InvalidateCache();

                await LoadSystemsAsync();
                SystemNameDropdown.SelectedItem = systemNameText;
                await LoadSystemDetails(systemNameText);

                StatusTextBlock.Text = $"{_localization.GetString("Systemsaved", "System saved: ")}{systemNameText}";
                await _messageBox.SystemSavedSuccessfullyMessageBoxAsync();

                // Keep favorites and play history in sync when the system was renamed:
                // both store the system name as a plain string, so without this migration
                // favorites would point at a system that no longer exists.
                if (isUpdate && !string.Equals(_originalSystemName, systemNameText, StringComparison.OrdinalIgnoreCase))
                {
                    var oldSystemName = _originalSystemName!;
                    await _favoritesManager.RenameSystemAsync(oldSystemName, systemNameText);
                    await _playHistoryManager.RenameSystemAsync(oldSystemName, systemNameText);
                    _logger.Information(
                        "System renamed from {OldSystemName} to {NewSystemName}. Favorites and play history migrated",
                        oldSystemName, systemNameText);
                }

                // Create folders based on the resolved paths
                var resolvedSystemFolder =
                    PathHelper.ResolveRelativeToAppDirectory(allSystemFolders.FirstOrDefault() ?? "");
                var resolvedSystemImageFolder = PathHelper.ResolveRelativeToAppDirectory(systemImageFolderText);
                if (resolvedSystemFolder != null && resolvedSystemImageFolder != null)
                {
                    await CreateDefaultSystemFoldersService.CreateFoldersAsync(
                        systemNameText, resolvedSystemFolder, resolvedSystemImageFolder,
                        _configuration, _logger, _messageBox);
                }

                _originalSystemName = systemNameText;
            }
            catch (InvalidOperationException ex)
            {
                await _messageBox.SaveSystemFailedMessageBoxAsync(ex.InnerException?.Message ?? "Unknown error");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error during system save process");
                await _messageBox.SaveSystemFailedMessageBoxAsync("An unexpected error occurred.");
            }
            finally
            {
                SaveSystemButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving system configuration");
        }
    }

    // ── Validation helpers (ported from EditSystemWindow.ValidateFields.cs) ──

    private void TrimInputValues(out string systemNameText, out string systemFolderText,
        out string systemImageFolderText,
        out string formatToSearchText, out string formatToLaunchText, out string emulator1NameText,
        out string emulator2NameText, out string emulator3NameText, out string emulator4NameText,
        out string emulator5NameText, out string emulator1LocationText, out string emulator2LocationText,
        out string emulator3LocationText, out string emulator4LocationText, out string emulator5LocationText,
        out string emulator1ParametersText, out string emulator2ParametersText, out string emulator3ParametersText,
        out string emulator4ParametersText, out string emulator5ParametersText)
    {
        systemNameText = SystemNameTextBox.Text?.Trim() ?? "";
        systemFolderText = SystemFolderTextBox.Text?.Trim() ?? "";
        systemImageFolderText = SystemImageFolderTextBox.Text?.Trim() ?? "";
        formatToSearchText = FormatToSearchTextBox.Text?.Trim() ?? "";
        formatToLaunchText = FormatToLaunchTextBox.Text?.Trim() ?? "";
        emulator1NameText = Emulator1NameTextBox.Text?.Trim() ?? "";
        emulator2NameText = Emulator2NameTextBox.Text?.Trim() ?? "";
        emulator3NameText = Emulator3NameTextBox.Text?.Trim() ?? "";
        emulator4NameText = Emulator4NameTextBox.Text?.Trim() ?? "";
        emulator5NameText = Emulator5NameTextBox.Text?.Trim() ?? "";
        emulator1LocationText = Emulator1PathTextBox.Text?.Trim() ?? "";
        emulator2LocationText = Emulator2PathTextBox.Text?.Trim() ?? "";
        emulator3LocationText = Emulator3PathTextBox.Text?.Trim() ?? "";
        emulator4LocationText = Emulator4PathTextBox.Text?.Trim() ?? "";
        emulator5LocationText = Emulator5PathTextBox.Text?.Trim() ?? "";
        emulator1ParametersText = Emulator1ParametersTextBox.Text?.Trim() ?? "";
        emulator2ParametersText = Emulator2ParametersTextBox.Text?.Trim() ?? "";
        emulator3ParametersText = Emulator3ParametersTextBox.Text?.Trim() ?? "";
        emulator4ParametersText = Emulator4ParametersTextBox.Text?.Trim() ?? "";
        emulator5ParametersText = Emulator5ParametersTextBox.Text?.Trim() ?? "";
    }

    private static void ValidatePaths(string systemFolderText, string systemImageFolderText,
        string emulator1LocationText, string emulator2LocationText, string emulator3LocationText,
        string emulator4LocationText, string emulator5LocationText,
        out bool isSystemFolderValid, out bool isSystemImageFolderValid,
        out bool isEmulator1LocationValid, out bool isEmulator2LocationValid,
        out bool isEmulator3LocationValid, out bool isEmulator4LocationValid,
        out bool isEmulator5LocationValid)
    {
        isSystemFolderValid = string.IsNullOrWhiteSpace(systemFolderText) || CheckPath.IsValidPath(systemFolderText);
        isSystemImageFolderValid = string.IsNullOrWhiteSpace(systemImageFolderText) ||
                                   CheckPath.IsValidPath(systemImageFolderText);
        isEmulator1LocationValid = string.IsNullOrWhiteSpace(emulator1LocationText) ||
                                   CheckPath.IsValidEmulatorExecutablePath(emulator1LocationText);
        isEmulator2LocationValid = string.IsNullOrWhiteSpace(emulator2LocationText) ||
                                   CheckPath.IsValidEmulatorExecutablePath(emulator2LocationText);
        isEmulator3LocationValid = string.IsNullOrWhiteSpace(emulator3LocationText) ||
                                   CheckPath.IsValidEmulatorExecutablePath(emulator3LocationText);
        isEmulator4LocationValid = string.IsNullOrWhiteSpace(emulator4LocationText) ||
                                   CheckPath.IsValidEmulatorExecutablePath(emulator4LocationText);
        isEmulator5LocationValid = string.IsNullOrWhiteSpace(emulator5LocationText) ||
                                   CheckPath.IsValidEmulatorExecutablePath(emulator5LocationText);
    }

    private void HandleValidationAlerts(bool isSystemFolderValid, bool isSystemImageFolderValid,
        bool isEmulator1LocationValid, bool isEmulator2LocationValid, bool isEmulator3LocationValid,
        bool isEmulator4LocationValid, bool isEmulator5LocationValid)
    {
        SetFieldValidationState(SystemFolderTextBox, isSystemFolderValid);
        SetFieldValidationState(SystemImageFolderTextBox, isSystemImageFolderValid);
        SetFieldValidationState(Emulator1PathTextBox, isEmulator1LocationValid);
        SetFieldValidationState(Emulator2PathTextBox, isEmulator2LocationValid);
        SetFieldValidationState(Emulator3PathTextBox, isEmulator3LocationValid);
        SetFieldValidationState(Emulator4PathTextBox, isEmulator4LocationValid);
        SetFieldValidationState(Emulator5PathTextBox, isEmulator5LocationValid);
    }

    private async Task<bool> CheckPathsAsync(bool isSystemFolderValid, bool isSystemImageFolderValid,
        bool isEmulator1LocationValid, bool isEmulator2LocationValid, bool isEmulator3LocationValid,
        bool isEmulator4LocationValid, bool isEmulator5LocationValid)
    {
        if (isSystemFolderValid && isSystemImageFolderValid && isEmulator1LocationValid && isEmulator2LocationValid &&
            isEmulator3LocationValid && isEmulator4LocationValid && isEmulator5LocationValid)
        {
            return false;
        }

        await _messageBox.PathOrParameterInvalidMessageBoxAsync();
        return true;
    }

    private async Task<bool> ValidateEmulator1NameAsync(string emulator1NameText)
    {
        if (!string.IsNullOrEmpty(emulator1NameText)) return false;

        await _messageBox.Emulator1RequiredMessageBoxAsync();
        return true;
    }

    private async Task<bool> ValidateEmulatorLocationAsync(string emulatorNameText, string emulatorLocationText,
        IEnumerable<string> formatsToSearch, int emulatorNumber)
    {
        if (string.IsNullOrEmpty(emulatorNameText)) return false;

        // If formatsToSearch contains bat, exe, lnk, or url, the emulator path is not required.
        var requiresEmulatorPath = !formatsToSearch.Any(static f =>
            f.Equals("bat", StringComparison.OrdinalIgnoreCase) ||
            f.Equals("exe", StringComparison.OrdinalIgnoreCase) ||
            f.Equals("lnk", StringComparison.OrdinalIgnoreCase) ||
            f.Equals("url", StringComparison.OrdinalIgnoreCase));

        if (requiresEmulatorPath && string.IsNullOrWhiteSpace(emulatorLocationText))
        {
            await _messageBox.EmulatorLocationRequiredMessageBoxAsync(emulatorNumber);
            return true;
        }

        return false;
    }

    private async Task<(bool IsFailed, List<string> Formats)> ValidateFormatToLaunchAsync(string formatToLaunchText,
        bool extractFileBeforeLaunch)
    {
        var formatsToLaunch = formatToLaunchText.Split(SplitSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(static format => format.Trim())
            .Where(static format => !string.IsNullOrEmpty(format))
            .ToList();

        if (extractFileBeforeLaunch && formatsToLaunch.Count == 0)
        {
            await _messageBox.ExtensionToLaunchIsRequiredMessageBoxAsync();
            return (true, formatsToLaunch);
        }

        return (false, formatsToLaunch);
    }

    private async Task<(bool IsFailed, List<string> Formats)> ValidateFormatToSearchAsync(string formatToSearchText,
        bool extractFileBeforeLaunch)
    {
        var formatsToSearch = formatToSearchText.Split(SplitSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(static format => format.Trim())
            .Where(static format => !string.IsNullOrEmpty(format))
            .ToList();

        if (formatsToSearch.Count == 0)
        {
            await _messageBox.ExtensionToSearchIsRequiredMessageBoxAsync();
            return (true, formatsToSearch);
        }

        if (extractFileBeforeLaunch && !formatsToSearch.All(static f => f is "zip" or "7z" or "rar"))
        {
            await _messageBox.FileMustBeCompressedMessageBoxAsync();
            return (true, formatsToSearch);
        }

        return (false, formatsToSearch);
    }

    private async Task<(bool IsFailed, string FolderText)> ValidateSystemImageFolderAsync(string systemNameText,
        string systemImageFolderText)
    {
        var defaultPattern = Path.Combine(".", "images", systemNameText);
        var prefixedDefaultPattern = Path.Combine("%BASEFOLDER%", "images", systemNameText);

        if (string.IsNullOrEmpty(systemImageFolderText)
            || systemImageFolderText.Equals(defaultPattern, StringComparison.OrdinalIgnoreCase)
            || systemImageFolderText.Equals(prefixedDefaultPattern, StringComparison.OrdinalIgnoreCase))
        {
            systemImageFolderText = prefixedDefaultPattern;
            SystemImageFolderTextBox.Text = systemImageFolderText;
        }

        if (string.IsNullOrEmpty(systemImageFolderText))
        {
            await _messageBox.SystemImageFolderCanNotBeEmptyMessageBoxAsync();
            return (true, systemImageFolderText);
        }

        var resolvedImageFolder = PathHelper.ResolveRelativeToAppDirectory(systemImageFolderText);
        if (!string.IsNullOrEmpty(resolvedImageFolder) && !Directory.Exists(resolvedImageFolder))
        {
            if (SanitizeInputSystemName.ContainsInvalidPathCharacters(resolvedImageFolder, out var invalidChars))
            {
                var invalidCharsStr = string.Join(", ", invalidChars.Select(static c => $"'{c}'"));
                await _messageBox.InvalidFolderCharactersMessageBoxAsync(invalidCharsStr);
                return (true, systemImageFolderText);
            }

            try
            {
                Directory.CreateDirectory(resolvedImageFolder);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating the system image folder: {Path}", resolvedImageFolder);
                await _messageBox.FolderCreationFailedMessageBoxAsync();
                return (true, systemImageFolderText);
            }
        }

        return (false, systemImageFolderText);
    }

    private async Task<(bool IsFailed, string FolderText)> ValidateSystemFolderAsync(string systemNameText,
        string systemFolderText)
    {
        var defaultPattern = Path.Combine(".", "roms", systemNameText);
        var prefixedDefaultPattern = Path.Combine("%BASEFOLDER%", "roms", systemNameText);

        if (string.IsNullOrEmpty(systemFolderText)
            || systemFolderText.Equals(defaultPattern, StringComparison.OrdinalIgnoreCase)
            || systemFolderText.Equals(prefixedDefaultPattern, StringComparison.OrdinalIgnoreCase))
        {
            systemFolderText = prefixedDefaultPattern;
            SystemFolderTextBox.Text = systemFolderText;
        }

        if (string.IsNullOrEmpty(systemFolderText))
        {
            await _messageBox.SystemFolderCanNotBeEmptyMessageBoxAsync();
            return (true, systemFolderText);
        }

        var resolvedSystemFolder = PathHelper.ResolveRelativeToAppDirectory(systemFolderText);
        if (!string.IsNullOrEmpty(resolvedSystemFolder) && !Directory.Exists(resolvedSystemFolder))
        {
            if (SanitizeInputSystemName.ContainsInvalidPathCharacters(resolvedSystemFolder, out var invalidChars))
            {
                var invalidCharsStr = string.Join(", ", invalidChars.Select(static c => $"'{c}'"));
                await _messageBox.InvalidFolderCharactersMessageBoxAsync(invalidCharsStr);
                return (true, systemFolderText);
            }

            try
            {
                Directory.CreateDirectory(resolvedSystemFolder);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating the system folder: {Path}", resolvedSystemFolder);
                await _messageBox.FolderCreationFailedMessageBoxAsync();
                return (true, systemFolderText);
            }
        }

        return (false, systemFolderText);
    }

    private async Task<bool> ValidateSystemNameAsync(string systemNameText)
    {
        systemNameText = SanitizeInputSystemName.SanitizeFolderName(systemNameText);

        if (!string.IsNullOrEmpty(systemNameText)) return false;

        await _messageBox.SystemNameCanNotBeEmptyMessageBoxAsync();
        return true;
    }

    private static string MaybeAddBaseFolderPrefix(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        var normalizedPath = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalizedPath) ||
            normalizedPath.StartsWith("%BASEFOLDER%", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedPath;
        }

        var trimmedPath = normalizedPath.TrimStart('.', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.Combine("%BASEFOLDER%", trimmedPath);
    }
}