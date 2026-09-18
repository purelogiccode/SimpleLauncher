using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Core.Services.UnifiedSettings;
using SimpleLauncher.Interfaces;
using SimpleLauncher.Services.Favorites;
using SimpleLauncher.Services.LoadImages;
using SimpleLauncher.Services.PlayHistory;
using SimpleLauncher.Services.QuitOrReinstall;
using SimpleLauncher.Services.SystemManager;
using CoreMessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;
using Application = System.Windows.Application;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace SimpleLauncher;

/// <summary>
///     Window for adding, editing, and validating emulator system configurations.
/// </summary>
internal partial class EditSystemWindow : ILoadingState
{
    private static readonly char[] SplitSeparators = [',', '|', ';'];
    private readonly IConfiguration _configuration;
    private readonly FavoritesManager _favoritesManager;
    private readonly IHelpUserService _helpUserService;
    private readonly IImageLoader _imageLoader;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly IParameterResolverService _parameterResolverService;
    private readonly PlayHistoryManager _playHistoryManager;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly string? _preSelectedSystemName;
    private readonly QuitSimpleLauncher _quitSimpleLauncher;
    private readonly SettingsManagerService _settings;
    private Button? _emergencyReturnButton;
    private string? _originalSystemName;
    private List<SystemManagerService> _systems = null!;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EditSystemWindow" /> class.
    /// </summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="playSoundEffects">The sound effects service.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="helpUserService">The help user service for context-sensitive help.</param>
    /// <param name="imageLoader">The image loader service.</param>
    /// <param name="messageBox">The message box library service.</param>
    /// <param name="quitSimpleLauncher">The quit application service.</param>
    /// <param name="logger">The logger for error logging.</param>
    /// <param name="parameterResolverService">The parameter resolver service.</param>
    /// <param name="preSelectedSystemName">Optional system name to pre-select in the dropdown.</param>
    /// <param name="favoritesManager">The favorites manager, kept in sync when a system is renamed.</param>
    /// <param name="playHistoryManager">The play history manager, kept in sync when a system is renamed.</param>
    public EditSystemWindow(SettingsManagerService settings, PlaySoundEffects playSoundEffects,
        IConfiguration configuration, IHelpUserService helpUserService, IImageLoader imageLoader,
        IMessageBoxLibraryService messageBox, QuitSimpleLauncher quitSimpleLauncher, ILogger logger,
        IParameterResolverService parameterResolverService, string? preSelectedSystemName = null,
        FavoritesManager? favoritesManager = null, PlayHistoryManager? playHistoryManager = null)
    {
        InitializeComponent();
        App.ApplyThemeToWindow(this);

        _configuration = configuration;
        _settings = settings;
        _playSoundEffects = playSoundEffects;
        _helpUserService = helpUserService;
        _imageLoader = imageLoader;
        _preSelectedSystemName = preSelectedSystemName;
        _messageBox = messageBox;
        _quitSimpleLauncher = quitSimpleLauncher;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _parameterResolverService = parameterResolverService;
        _favoritesManager = favoritesManager!;
        _playHistoryManager = playHistoryManager!;

        ApplyExpanderSettings();

        Closing += EditSystem_Closing;

        Loaded += (_, _) =>
        {
            LoadingOverlay.ApplyTemplate();
            if (LoadingOverlay.Template.FindName("PART_EmergencyReturnButton", LoadingOverlay) is Button emergencyBtn)
            {
                _emergencyReturnButton = emergencyBtn;
                emergencyBtn.Click += EmergencyOverlayRelease_Click;
            }

            _ = LoadSystemsAsync();
        };

        SaveSystemButton.IsEnabled = false;
        DeleteSystemButton.IsEnabled = false;
    }

    /// <summary>
    ///     Sets the loading state of the window, showing or hiding the loading overlay.
    /// </summary>
    /// <param name="isLoading">Whether the window is in a loading state.</param>
    /// <param name="message">Optional message to display on the loading overlay.</param>
    public void SetLoadingState(bool isLoading, string? message = null)
    {
        Dispatcher.Invoke(() =>
        {
            LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            MainContentGrid.IsEnabled = !isLoading;
            if (isLoading)
            {
                LoadingOverlay.Content =
                    message ?? (string)Application.Current.TryFindResource("Loading") ?? "Loading...";
            }
        });
    }

    private void ApplyExpanderSettings()
    {
        AdditionalFoldersExpander.IsExpanded = _settings.AdditionalSystemFoldersExpanded;
        Emulator1Expander.IsExpanded = _settings.Emulator1Expanded;
        Emulator2Expander.IsExpanded = _settings.Emulator2Expanded;
        Emulator3Expander.IsExpanded = _settings.Emulator3Expanded;
        Emulator4Expander.IsExpanded = _settings.Emulator4Expanded;
        Emulator5Expander.IsExpanded = _settings.Emulator5Expanded;
    }

    private async Task LoadSystemsAsync()
    {
        try
        {
            SetLoadingState(true,
                (string)Application.Current.TryFindResource("Loadingsystems") ?? "Loading systems...");
            var systems = await Task.Run(() => SystemManagerService.LoadSystemManagers(_configuration));

            if (systems == null)
            {
                // Notify user on UI thread
                await _messageBox.SystemXmlNotFoundMessageBoxAsync();
                _quitSimpleLauncher.SimpleQuitApplication(); // Or just Close();
            }
            else
            {
                _systems = systems.ToList();
                PopulateSystemNamesDropdown();

                if (!string.IsNullOrEmpty(_preSelectedSystemName))
                    SystemNameDropdown.SelectedItem = _preSelectedSystemName;
            }
        }
        catch (Exception ex)
        {
            // Notify developer
            _logger.Error(ex, "Error loading systems into Edit window");
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private void ChooseSystemFolder(object sender, RoutedEventArgs e)
    {
        var pleaseselecttheSystemFolder2 = (string)Application.Current.TryFindResource("PleaseselecttheSystemFolder") ??
                                           "Please select the System Folder";

        // Create new OpenFolderDialog
        var openFolderDialog = new OpenFolderDialog
        {
            Title = pleaseselecttheSystemFolder2
        };

        // Show dialog and handle result
        if (openFolderDialog.ShowDialog() != true) return;

        var foldername = openFolderDialog.FolderName;
        SystemFolderTextBox.Text = foldername;

        MarkValid(SystemFolderTextBox);
    }

    private void ChooseSystemImageFolder(object sender, RoutedEventArgs e)
    {
        var pleaseselecttheSystemImage2 = (string)Application.Current.TryFindResource("PleaseselecttheSystemImage") ??
                                          "Please select the System Image Folder";

        // Create new OpenFolderDialog
        var openFolderDialog = new OpenFolderDialog
        {
            Title = pleaseselecttheSystemImage2
        };

        // Show dialog and handle result
        var result = openFolderDialog.ShowDialog();

        if (result != true) return;

        var foldername = openFolderDialog.FolderName.Trim();
        SystemImageFolderTextBox.Text = foldername;
        MarkValid(SystemImageFolderTextBox);
    }

    private void ChooseEmulator1Path(object sender, RoutedEventArgs e)
    {
        ChooseEmulatorPath(1, Emulator1PathTextBox);
    }

    private void ChooseEmulator2Path(object sender, RoutedEventArgs e)
    {
        ChooseEmulatorPath(2, Emulator2PathTextBox);
    }

    private void ChooseEmulator3Path(object sender, RoutedEventArgs e)
    {
        ChooseEmulatorPath(3, Emulator3PathTextBox);
    }

    private void ChooseEmulator4Path(object sender, RoutedEventArgs e)
    {
        ChooseEmulatorPath(4, Emulator4PathTextBox);
    }

    private void ChooseEmulator5Path(object sender, RoutedEventArgs e)
    {
        ChooseEmulatorPath(5, Emulator5PathTextBox);
    }

    private void ChooseEmulatorPath(int emulatorNumber, TextBox pathTextBox)
    {
        var selectEmulator = (string)Application.Current.TryFindResource($"SelectEmulator{emulatorNumber}") ??
                             $"Select Emulator {emulatorNumber}";
        var dialog = new OpenFileDialog
        {
            DefaultExt = ".exe",
            Filter = "Executable Files (*.exe;*.bat)|*.exe;*.bat",
            Title = selectEmulator
        };

        var result = dialog.ShowDialog();
        if (result == true)
        {
            var filename = dialog.FileName;
            pathTextBox.Text = filename;
            MarkValid(pathTextBox);
        }

        // Update the HelpUserTextBlock
        HelpUserTextBlock.Document.Blocks.Clear();
        _helpUserService.UpdateHelpUserTextBlock(HelpUserTextBlock, SystemNameTextBox.Text.Trim());
    }

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

            HelpUserTextBlock.Document.Blocks.Clear();

            SaveSystemButton.IsEnabled = true;
            DeleteSystemButton.IsEnabled = false;

            // Notify user
            await _messageBox.YouCanAddANewSystemMessageBoxAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method AddSystemButton_ClickAsync");
        }
    }

    private void EnableFields()
    {
        SetAllEditableFields(true);
    }

    private void DisableAllEditableFields()
    {
        SetAllEditableFields(false);
    }

    // Enables (enabled: true) or disables (enabled: false) every editable field.
    private void SetAllEditableFields(bool enabled)
    {
        SetReadOnlyField(SystemNameTextBox, enabled);
        SetReadOnlyField(SystemFolderTextBox, enabled);
        AdditionalFoldersListBox.IsEnabled = enabled;
        AddFolderButton.IsEnabled = enabled;
        RemoveFolderButton.IsEnabled = enabled;
        SetReadOnlyField(SystemImageFolderTextBox, enabled);
        SetReadOnlyField(FormatToSearchTextBox, enabled);
        ExtractFileBeforeLaunchComboBox.IsEnabled = enabled;
        GroupByFolderComboBox.IsEnabled = enabled;
        DisableRecursiveSearchComboBox.IsEnabled = enabled;
        SetReadOnlyField(FormatToLaunchTextBox, enabled);

        foreach (var textBox in new[]
                 {
                     Emulator1NameTextBox, Emulator1PathTextBox, Emulator1ParametersTextBox,
                     Emulator2NameTextBox, Emulator2PathTextBox, Emulator2ParametersTextBox,
                     Emulator3NameTextBox, Emulator3PathTextBox, Emulator3ParametersTextBox,
                     Emulator4NameTextBox, Emulator4PathTextBox, Emulator4ParametersTextBox,
                     Emulator5NameTextBox, Emulator5PathTextBox, Emulator5ParametersTextBox
                 })
        {
            SetReadOnlyField(textBox, enabled);
        }

        foreach (var notification in new[]
                 {
                     ReceiveANotificationOnEmulatorError1, ReceiveANotificationOnEmulatorError2,
                     ReceiveANotificationOnEmulatorError3, ReceiveANotificationOnEmulatorError4,
                     ReceiveANotificationOnEmulatorError5
                 })
        {
            notification.IsEnabled = enabled;
        }

        foreach (var button in new[]
                 {
                     ChooseSystemFolderButton, ChooseSystemImageFolderButton,
                     ChooseEmulator1PathButton, ChooseEmulator2PathButton, ChooseEmulator3PathButton,
                     ChooseEmulator4PathButton, ChooseEmulator5PathButton, ChooseSystemImageButton,
                     SuggestEmulator1ParametersButton, SuggestEmulator2ParametersButton,
                     SuggestEmulator3ParametersButton, SuggestEmulator4ParametersButton,
                     SuggestEmulator5ParametersButton
                 })
        {
            button.IsEnabled = enabled;
        }
    }

    private static void SetReadOnlyField(TextBox textBox, bool enabled)
    {
        textBox.IsReadOnly = !enabled;
        textBox.IsEnabled = enabled;
    }

    private void ClearFields()
    {
        SystemNameDropdown.SelectedItem = null;
        ClearFieldsForNoSelection();
    }

    // Clears fields when no system is selected, without affecting SystemNameDropdown itself.
    private void ClearFieldsForNoSelection()
    {
        SystemNameTextBox.Text = "";
        MarkValid(SystemNameTextBox);

        SystemFolderTextBox.Text = "";
        MarkValid(SystemFolderTextBox);
        AdditionalFoldersListBox.Items.Clear();

        SystemImageFolderTextBox.Text = "";
        MarkValid(SystemImageFolderTextBox);

        FormatToSearchTextBox.Text = "";
        MarkValid(FormatToSearchTextBox);

        ExtractFileBeforeLaunchComboBox.SelectedItem = null;

        GroupByFolderComboBox.SelectedItem = null;

        DisableRecursiveSearchComboBox.SelectedItem = null;

        FormatToLaunchTextBox.Text = "";
        MarkValid(FormatToLaunchTextBox);

        ClearAllEmulatorFieldsInternal();
        UpdateSystemImagePreview();
    }

    private void ClearAllEmulatorFieldsInternal()
    {
        Emulator1NameTextBox.Text = "";
        MarkValid(Emulator1NameTextBox);
        Emulator1PathTextBox.Text = "";
        MarkValid(Emulator1PathTextBox);
        Emulator1ParametersTextBox.Text = "";
        MarkValid(Emulator1ParametersTextBox);
        ReceiveANotificationOnEmulatorError1.SelectedItem = null;

        Emulator2NameTextBox.Text = "";
        MarkValid(Emulator2NameTextBox);
        Emulator2PathTextBox.Text = "";
        MarkValid(Emulator2PathTextBox);
        Emulator2ParametersTextBox.Text = "";
        MarkValid(Emulator2ParametersTextBox);
        ReceiveANotificationOnEmulatorError2.SelectedItem = null;

        Emulator3NameTextBox.Text = "";
        MarkValid(Emulator3NameTextBox);
        Emulator3PathTextBox.Text = "";
        MarkValid(Emulator3PathTextBox);
        Emulator3ParametersTextBox.Text = "";
        MarkValid(Emulator3ParametersTextBox);
        ReceiveANotificationOnEmulatorError3.SelectedItem = null;

        Emulator4NameTextBox.Text = "";
        MarkValid(Emulator4NameTextBox);
        Emulator4PathTextBox.Text = "";
        MarkValid(Emulator4PathTextBox);
        Emulator4ParametersTextBox.Text = "";
        MarkValid(Emulator4ParametersTextBox);
        ReceiveANotificationOnEmulatorError4.SelectedItem = null;

        Emulator5NameTextBox.Text = "";
        MarkValid(Emulator5NameTextBox);
        Emulator5PathTextBox.Text = "";
        MarkValid(Emulator5PathTextBox);
        Emulator5ParametersTextBox.Text = "";
        MarkValid(Emulator5ParametersTextBox);
        ReceiveANotificationOnEmulatorError5.SelectedItem = null;
    }

    private async void DeleteSystemButton_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            HelpUserTextBlock.Document.Blocks.Clear();

            if (SystemNameDropdown.SelectedItem == null)
            {
                // Notify user
                await _messageBox.SelectASystemToDeleteMessageBoxAsync();
                return;
            }

            var selectedSystemName = SystemNameDropdown.SelectedItem.ToString()!;

            var result = await _messageBox.AreYouSureDoYouWantToDeleteThisSystemMessageBoxAsync();
            if (result != CoreMessageBoxResult.Yes) return;

            await SystemManagerService.DeleteSystemAsync(selectedSystemName, _logger);
            _playSoundEffects.PlayNotificationSound();

            await LoadSystemsAsync();
            if (SystemNameDropdown.Items.Count == 0 || SystemNameDropdown.SelectedItem == null)
                PopulateSystemNamesDropdown();

            // Notify user
            await _messageBox.SystemHasBeenDeletedMessageBoxAsync(selectedSystemName);
        }
        catch (Exception ex)
        {
            _logger.Debug($"Error in method DeleteSystemButton_ClickAsync: {ex.Message}");
            _logger.Error(ex, "Error in method DeleteSystemButton_ClickAsync");
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void EditSystem_Closing(object? sender, CancelEventArgs e)
    {
        // Unsubscribe emergency button
        if (_emergencyReturnButton != null)
        {
            _emergencyReturnButton.Click -= EmergencyOverlayRelease_Click;
            _emergencyReturnButton = null;
        }

        // Save expander states
        _settings.AdditionalSystemFoldersExpanded = AdditionalFoldersExpander.IsExpanded;
        _settings.Emulator1Expanded = Emulator1Expander.IsExpanded;
        _settings.Emulator2Expanded = Emulator2Expander.IsExpanded;
        _settings.Emulator3Expanded = Emulator3Expander.IsExpanded;
        _settings.Emulator4Expanded = Emulator4Expander.IsExpanded;
        _settings.Emulator5Expanded = Emulator5Expander.IsExpanded;
        _settings.SaveAsync();

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
                    // VACUUM INTO: a plain File.Copy of a WAL database can omit the most
                    // recent transactions (the system write that just happened).
                    UnifiedSettingsDatabase.BackupDatabase(Path.Combine(dbFolder, backupFileName));
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
            // Notify developer
            _logger.Error(ex, "Error in method EditSystem_Closing");
        }
    }

    private async void HelpLink_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            var searchUrl = _configuration.GetValue<string>("WikiParametersUrl") ??
                            "https://github.com/purelogiccode/SimpleLauncher/wiki/parameters/";

            // The URL comes from editable configuration: allowlist http(s) before it can
            // reach a shell handler (WPF-04).
            if (!UrlHelper.IsHttpUrl(searchUrl))
            {
                _logger.Information($"Blocked non-web help URL from configuration: {searchUrl}");
                await _messageBox.ErrorOpeningUrlMessageBoxAsync();
                return;
            }

            try
            {
                using (Process.Start(new ProcessStartInfo
                       {
                           FileName = searchUrl,
                           UseShellExecute = true
                       }))
                {
                }
            }
            catch (Win32Exception ex) // Catch Win32Exception specifically
            {
                if (CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex))
                {
                    // Specific message for application control policy blocking links
                    await _messageBox.ApplicationControlPolicyBlockedManualLinkMessageBoxAsync(searchUrl);
                    _logger.Error(ex, "Application control policy blocked opening HelpLink");
                }
                else
                {
                    // Existing error handling for other Win32Exceptions
                    _logger.Error(ex, "Error in method HelpLink_ClickAsync");
                    await _messageBox.ErrorOpeningUrlMessageBoxAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in method HelpLink_ClickAsync");
                await _messageBox.ErrorOpeningUrlMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method HelpLink_ClickAsync");
        }
    }

    private void SystemNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // Update HelpUserTextBlock
        HelpUserTextBlock.Document.Blocks.Clear();
        _helpUserService.UpdateHelpUserTextBlock(HelpUserTextBlock, SystemNameTextBox.Text.Trim());
    }

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var title = (string)Application.Current.TryFindResource("SelectAdditionalSystemFolder") ??
                    "Select an additional system folder";
        var openFolderDialog = new OpenFolderDialog { Title = title };
        if (openFolderDialog.ShowDialog() == true) AdditionalFoldersListBox.Items.Add(openFolderDialog.FolderName);
    }

    private void RemoveFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (AdditionalFoldersListBox.SelectedItem != null)
            AdditionalFoldersListBox.Items.Remove(AdditionalFoldersListBox.SelectedItem);
    }

    private void EmergencyOverlayRelease_Click(object sender, RoutedEventArgs e)
    {
        _playSoundEffects.PlayNotificationSound();
        // Hide overlay and re-enable UI
        LoadingOverlay.Visibility = Visibility.Collapsed;
        MainContentGrid?.IsEnabled = true;

        _logger.Debug("[Emergency] User forced overlay dismissal in EditSystemWindow");
        (Application.Current.MainWindow as MainWindow)?.UpdateStatusBarService.UpdateContent(
            "Emergency reset performed.");
    }

    private async void ChooseSystemImageButton_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            var systemName = SystemNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(systemName))
            {
                await _messageBox.SystemNameRequiredBeforeChoosingImageMessageBoxAsync();
                return;
            }

            // The system name becomes a file name under images/systems: sanitize it so
            // "..", separators, reserved device names, or overlong names cannot escape
            // that folder or throw (WPF-06).
            var safeSystemName = GetSafeSystemImageFileName(systemName);
            if (string.IsNullOrEmpty(safeSystemName))
            {
                await _messageBox.FailedToCopySystemImageMessageBoxAsync(
                    $"The system name '{systemName}' cannot be used as an image file name.");
                return;
            }

            var dialog = new OpenFileDialog
            {
                DefaultExt = ".png",
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                Title = "Select System Image"
            };

            if (dialog.ShowDialog() != true) return;

            var sourceFilePath = dialog.FileName;
            var extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            if (!string.Equals(extension, ".png", StringComparison.Ordinal) &&
                !string.Equals(extension, ".jpg", StringComparison.Ordinal) &&
                !string.Equals(extension, ".jpeg", StringComparison.Ordinal))
            {
                await _messageBox.InvalidImageFormatMessageBoxAsync();
                return;
            }

            var imagesSystemsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "systems");
            try
            {
                if (!Directory.Exists(imagesSystemsDir)) Directory.CreateDirectory(imagesSystemsDir);

                var destFilePath = Path.Combine(imagesSystemsDir, $"{safeSystemName}{extension}");
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
                        // the Windows Explorer preview pane): not a bug, keep it out of the bug report.
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

    /// <summary>
    ///     Reduces a raw system name to a file name safe under the images/systems folder.
    ///     Returns null when nothing usable remains (WPF-06).
    /// </summary>
    private static string? GetSafeSystemImageFileName(string systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName)) return null;

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(systemName.Trim().Length);
        foreach (var c in systemName.Trim())
            builder.Append(Array.IndexOf(invalid, c) < 0 ? c : '_');

        var safe = builder.ToString().Trim().Trim('.');
        if (string.IsNullOrEmpty(safe)) return null;

        // Reserved DOS device names (CON, PRN, AUX, NUL, COM1-9, LPT1-9, ...) cannot be
        // created as files even with an extension.
        var stem = safe.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
            (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) ||
                                  stem.StartsWith("LPT", StringComparison.Ordinal)) &&
             char.IsDigit(stem[3])))
        {
            safe = "_" + safe;
        }

        // Cap length so the final path stays well under MAX_PATH.
        if (safe.Length > 100) safe = safe[..100].TrimEnd('.');

        return string.IsNullOrEmpty(safe) ? null : safe;
    }

    private void UpdateSystemImagePreview()
    {
        // Same sanitization as the copy path so preview resolves the stored file (WPF-06).
        var systemName = GetSafeSystemImageFileName(SystemNameTextBox.Text.Trim());
        var imagesSystemsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "systems");
        string? imagePath = null;

        if (!string.IsNullOrEmpty(systemName))
        {
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
            {
                var path = Path.Combine(imagesSystemsDir, $"{systemName}{ext}");
                if (File.Exists(path))
                {
                    imagePath = path;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(imagePath)) imagePath = Path.Combine(imagesSystemsDir, "default.png");

        if (!File.Exists(imagePath))
        {
            SystemImagePreview.Source = null;
            return;
        }

        try
        {
            var imageBytes = _imageLoader.LoadImageBytes(imagePath);
            SystemImagePreview.Source = imageBytes?.ToBitmapImage();
        }
        catch
        {
            SystemImagePreview.Source = null;
        }
    }

    private async void SuggestEmulator1Parameters_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await SuggestParametersAsync(
                Emulator1NameTextBox.Text,
                Emulator1PathTextBox.Text,
                Emulator1ParametersTextBox.Text,
                SuggestEmulator1ParametersButton);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method SuggestEmulator1Parameters_ClickAsync");
        }
    }

    private async void SuggestEmulator2Parameters_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await SuggestParametersAsync(
                Emulator2NameTextBox.Text,
                Emulator2PathTextBox.Text,
                Emulator2ParametersTextBox.Text,
                SuggestEmulator2ParametersButton);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method SuggestEmulator2Parameters_ClickAsync");
        }
    }

    private async void SuggestEmulator3Parameters_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await SuggestParametersAsync(
                Emulator3NameTextBox.Text,
                Emulator3PathTextBox.Text,
                Emulator3ParametersTextBox.Text,
                SuggestEmulator3ParametersButton);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method SuggestEmulator3Parameters_ClickAsync");
        }
    }

    private async void SuggestEmulator4Parameters_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await SuggestParametersAsync(
                Emulator4NameTextBox.Text,
                Emulator4PathTextBox.Text,
                Emulator4ParametersTextBox.Text,
                SuggestEmulator4ParametersButton);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method SuggestEmulator4Parameters_ClickAsync");
        }
    }

    private async void SuggestEmulator5Parameters_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await SuggestParametersAsync(
                Emulator5NameTextBox.Text,
                Emulator5PathTextBox.Text,
                Emulator5ParametersTextBox.Text,
                SuggestEmulator5ParametersButton);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method SuggestEmulator5Parameters_ClickAsync");
        }
    }

    private async Task SuggestParametersAsync(string emulatorName, string emulatorPath, string currentParameters,
        Button suggestButton)
    {
        var successTitle = (string)Application.Current.TryFindResource("ParameterResolverSuccess") ??
                           "Parameter Suggestion";
        var errorTitle = (string)Application.Current.TryFindResource("ParameterResolverError") ?? "Error";
        var errorMessage = (string)Application.Current.TryFindResource("ErrorProcessingRequest") ??
                           "There was an error processing your request.";
        var confirmMessage = (string)Application.Current.TryFindResource("ParameterResolverConfirmApply") ??
                             "Do you want to apply this parameter?";

        if (string.IsNullOrWhiteSpace(emulatorName))
        {
            var enterEmulatorNameMsg =
                (string)Application.Current.TryFindResource("ParameterResolverEnterEmulatorName") ??
                "Please enter an emulator name first.";
            await _messageBox.WarningMessageBoxAsync(enterEmulatorNameMsg);
            return;
        }

        if (string.IsNullOrWhiteSpace(SystemNameTextBox.Text))
        {
            var enterSystemNameMsg =
                (string)Application.Current.TryFindResource("ParameterResolverEnterSystemName") ??
                "Please enter a system name first.";
            await _messageBox.WarningMessageBoxAsync(enterSystemNameMsg);
            return;
        }

        suggestButton.IsEnabled = false;
        Mouse.OverrideCursor = Cursors.Wait;

        var loadingMessage = (string)Application.Current.TryFindResource("ParameterResolverLoading") ??
                             "Resolving parameters, please wait...";
        LoadingOverlay.Content = loadingMessage;
        LoadingOverlay.Visibility = Visibility.Visible;

        try
        {
            var request = new ParameterResolverRequest
            {
                SystemName = SystemNameTextBox.Text.Trim(),
                SystemFolder = SystemFolderTextBox.Text.Trim(),
                FileFormatsToSearch = SplitAndTrim(FormatToSearchTextBox.Text) ?? [],
                ExtractFileBeforeLaunch = string.Equals(ExtractFileBeforeLaunchComboBox.SelectedItem?.ToString(),
                    "true", StringComparison.Ordinal),
                FileFormatsToLaunch = SplitAndTrim(FormatToLaunchTextBox.Text) ?? [],
                GroupByFolder = string.Equals(GroupByFolderComboBox.SelectedItem?.ToString(), "true",
                    StringComparison.Ordinal),
                DisableRecursiveSearch = string.Equals(DisableRecursiveSearchComboBox.SelectedItem?.ToString(), "true",
                    StringComparison.Ordinal),
                EmulatorName = emulatorName.Trim(),
                EmulatorPath = emulatorPath?.Trim() ?? "",
                CurrentParameters = currentParameters?.Trim() ?? ""
            };

            var result = await _parameterResolverService.ResolveParametersAsync(request);

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
                    textBox?.Text = suggestedParam;
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
            LoadingOverlay.Visibility = Visibility.Collapsed;
            suggestButton.IsEnabled = true;
            Mouse.OverrideCursor = null;
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

    private static List<string>? SplitAndTrim(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return text.Split(SplitSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}