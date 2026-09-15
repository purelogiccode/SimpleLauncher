using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
using SimpleLauncher.ViewModels;

namespace SimpleLauncher;

/// <summary>
///     Window displaying the application's update history in markdown format.
/// </summary>
public partial class UpdateHistoryWindow
{
    private readonly ILogger _logger;
    private readonly UpdateHistoryViewModel _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UpdateHistoryWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing update history data.</param>
    /// <param name="logger">The debug logger.</param>
    public UpdateHistoryWindow(UpdateHistoryViewModel viewModel, ILogger logger)
    {
        InitializeComponent();
        App.ApplyThemeToWindow(this);

        _viewModel = viewModel;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        DataContext = _viewModel;

        HistoryMarkdownViewer.AddHandler(Hyperlink.RequestNavigateEvent,
            new RequestNavigateEventHandler(OnHyperlinkRequestNavigate));
        Loaded += UpdateHistoryWindow_LoadedAsync;
        Closed += UpdateHistoryWindow_Closed;
    }

    private async void UpdateHistoryWindow_LoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error initializing UpdateHistoryWindow");
        }
    }

    private void UpdateHistoryWindow_Closed(object? sender, EventArgs e)
    {
        HistoryMarkdownViewer.RemoveHandler(Hyperlink.RequestNavigateEvent,
            new RequestNavigateEventHandler(OnHyperlinkRequestNavigate));
    }

    private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        _viewModel.OnHyperlinkRequestNavigate(e.Uri);
        e.Handled = true;
    }
}