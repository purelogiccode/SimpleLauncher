using Avalonia.Controls;
using SimpleLauncher.Avalonia.ViewModels;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window displaying the application's update history in markdown format.
/// </summary>
public partial class UpdateHistoryWindow : Window
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

        _viewModel = viewModel;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        DataContext = _viewModel;

        Loaded += UpdateHistoryWindow_LoadedAsync;
    }

    private async void UpdateHistoryWindow_LoadedAsync(object? sender, EventArgs e)
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
}