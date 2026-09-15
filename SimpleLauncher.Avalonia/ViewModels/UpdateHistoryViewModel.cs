using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the update history window that displays release notes.
/// </summary>
public class UpdateHistoryViewModel : ObservableObject
{
    private readonly ILogger _logger;
    private readonly IResourceProvider _resourceProvider;
    private string _markdownContent = "";

    /// <summary>Initializes a new instance of the <see cref="UpdateHistoryViewModel" /> class.</summary>
    /// <param name="logErrors">The logger for recording errors.</param>
    /// <param name="resourceProvider">The resource provider for localized strings.</param>
    public UpdateHistoryViewModel(ILogger logErrors, IResourceProvider resourceProvider)
    {
        _logger = logErrors;
        _resourceProvider = resourceProvider;
    }

    /// <summary>Gets the content of the update history to display.</summary>
    public string MarkdownContent
    {
        get => _markdownContent;
        private set => SetProperty(ref _markdownContent, value);
    }

    /// <summary>Asynchronously loads the update history content from the whatsnew.md file.</summary>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    public async Task InitializeAsync()
    {
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WhatsNew.md");

        try
        {
            var defaultContent = _resourceProvider.GetString("WhatsNewFileNotFound",
                "# 'whatsnew.md' not found. The update history file could not be found.");
            MarkdownContent = File.Exists(filePath)
                ? await File.ReadAllTextAsync(filePath)
                : defaultContent;
        }
        catch (Exception ex)
        {
            const string contextMessage = "Failed to load 'whatsnew.md'.";
            _logger.Error(ex, contextMessage);

            MarkdownContent = _resourceProvider.GetString("UpdateHistoryLoadError",
                "Error. Could not load the update history. The error has been logged.");
        }
    }

    /// <summary>Opens the specified URI in the default browser.</summary>
    /// <param name="uri">The URI to navigate to.</param>
    public void OnHyperlinkRequestNavigate(Uri uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening hyperlink in browser");
        }
    }
}