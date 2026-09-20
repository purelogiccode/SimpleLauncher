using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the video and info link configuration window.
/// </summary>
public partial class SetLinksViewModel : ObservableObject
{
    private readonly IConfiguration _configuration;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settingsManager;
    [ObservableProperty] public partial string InfoUrl { get; set; }

    [ObservableProperty] public partial string VideoUrl { get; set; }

    /// <summary>Initializes a new instance of the <see cref="SetLinksViewModel" /> class.</summary>
    /// <param name="settingsManager">The settings manager for reading and saving link URLs.</param>
    /// <param name="configuration">The application configuration for default URL values.</param>
    /// <param name="messageBox">The message box service for displaying dialogs.</param>
    /// <param name="resourceProvider">The resource provider for localized strings.</param>
    public SetLinksViewModel(SettingsManagerService settingsManager, IConfiguration configuration,
        IMessageBoxLibraryService messageBox, IResourceProvider resourceProvider)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _configuration = configuration;
        _messageBox = messageBox;
        _ = resourceProvider;
        VideoUrl = _settingsManager.VideoUrl;
        InfoUrl = _settingsManager.InfoUrl;

        // Root UI icons are embedded in the assembly (AvaloniaResource) and resolved through
        // the asset loader by PathToImageConverter (avares://), like the context menu icons.
        VideoIconPath = "avares://SimpleLauncher.Avalonia/images/video.png";
        InfoIconPath = "avares://SimpleLauncher.Avalonia/images/info.png";
    }

    /// <summary>Gets the path to the video link button icon.</summary>
    public string VideoIconPath { get; }

    /// <summary>Gets the path to the info link button icon.</summary>
    public string InfoIconPath { get; }

    /// <summary>Event raised when settings have been saved.</summary>
    public event EventHandler SaveCompleted = null!;

    /// <summary>Event raised when the window should be closed.</summary>
    public event EventHandler CloseRequested = null!;

    [RelayCommand]
    private async Task SaveAsync()
    {
        _settingsManager.VideoUrl = string.IsNullOrWhiteSpace(VideoUrl)
            ? "https://www.youtube.com/results?search_query="
            : VideoUrl;

        _settingsManager.InfoUrl = string.IsNullOrWhiteSpace(InfoUrl)
            ? "https://www.igdb.com/search?q="
            : InfoUrl;

        await _settingsManager.SaveAsync();

        await _messageBox.LinksSavedMessageBoxAsync();

        SaveCompleted?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task RevertAsync()
    {
        _settingsManager.VideoUrl = _configuration.GetValue<string>("Urls:YouTubeSearch") ??
                                    "https://www.youtube.com/results?search_query=";
        _settingsManager.InfoUrl =
            _configuration.GetValue<string>("Urls:IgdbSearch") ?? "https://www.igdb.com/search?q=";

        VideoUrl = _settingsManager.VideoUrl;
        InfoUrl = _settingsManager.InfoUrl;

        await _settingsManager.SaveAsync();

        await _messageBox.LinksRevertedMessageBoxAsync();

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}