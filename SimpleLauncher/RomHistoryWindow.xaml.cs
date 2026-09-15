using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
using SimpleLauncher.Core.Services;
using SimpleLauncher.ViewModels;

namespace SimpleLauncher;

/// <summary>
///     Window displaying ROM history information with hyperlinked references.
/// </summary>
public partial class RomHistoryWindow
{
    private readonly ILogger _logger;
    private readonly RequestNavigateEventHandler _requestNavigateHandler;
    private bool _handlersWired;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RomHistoryWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing ROM history data.</param>
    /// <param name="logger">The debug logger.</param>
    public RomHistoryWindow(RomHistoryViewModel viewModel, ILogger logger)
    {
        InitializeComponent();
        App.ApplyThemeToWindow(this);

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _requestNavigateHandler = OnHyperlinkRequestNavigate;

        // Subscribe only once: repeated Loaded events would otherwise stack duplicate
        // handlers and launch N processes per click (WPF-25).
        Loaded += (_, _) =>
        {
            if (_handlersWired) return;

            _handlersWired = true;
            HistoryMarkdownViewer.AddHandler(Hyperlink.RequestNavigateEvent, _requestNavigateHandler);
        };

        Closed += (_, _) =>
        {
            HistoryMarkdownViewer.RemoveHandler(Hyperlink.RequestNavigateEvent, _requestNavigateHandler);
            _handlersWired = false;
        };

        Loaded += async (_, _) =>
        {
            try
            {
                await viewModel.LoadRomHistoryAsync();
            }
            catch (Exception ex)
            {
                _logger.Debug($"Error loading ROM history: {ex.Message}");
            }
        };

        DataContext = viewModel;
    }

    /// <summary>
    ///     Initializes the window with ROM and system information for history lookup.
    /// </summary>
    /// <param name="romName">The name of the ROM.</param>
    /// <param name="systemName">The name of the system.</param>
    /// <param name="searchTerm">The search term for filtering history.</param>
    public void Initialize(string romName, string systemName, string searchTerm)
    {
        ((RomHistoryViewModel)DataContext).Initialize(romName, systemName, searchTerm);
    }

    private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        // Allowlist http(s) only: markdown links must never reach a shell handler (WPF-04).
        if (!UrlHelper.TryOpenHttpUrlInBrowser(e.Uri.AbsoluteUri))
        {
            _logger.Warning($"Blocked non-web or unloadable history link: {e.Uri.AbsoluteUri}");
            System.Windows.MessageBox.Show(
                $"The link could not be opened because it is not a valid web address:\n{e.Uri.AbsoluteUri}",
                "Could Not Open Link", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        e.Handled = true;
    }
}