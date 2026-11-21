using Avalonia.Controls;
using SimpleLauncher.Avalonia.ViewModels;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window displaying ROM history information.
/// </summary>
internal partial class RomHistoryWindow : Window
{
    private readonly ILogger _logger;
    private readonly RomHistoryViewModel _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RomHistoryWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing ROM history data.</param>
    /// <param name="logger">The debug logger.</param>
    public RomHistoryWindow(RomHistoryViewModel viewModel, ILogger logger)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Loaded += async (_, _) =>
        {
            try
            {
                await _viewModel.LoadRomHistoryAsync();
            }
            catch (Exception ex)
            {
                _logger.Debug($"Error loading ROM history: {ex.Message}");
            }
        };

        DataContext = _viewModel;
    }

    /// <summary>
    ///     Initializes the window with ROM and system information for history lookup.
    /// </summary>
    /// <param name="romName">The name of the ROM.</param>
    /// <param name="systemName">The name of the system.</param>
    /// <param name="searchTerm">The search term for filtering history.</param>
    public void Initialize(string romName, string systemName, string searchTerm)
    {
        _viewModel.Initialize(romName, systemName, searchTerm);
    }
}