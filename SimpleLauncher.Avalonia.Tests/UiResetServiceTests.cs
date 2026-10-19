using Moq;
using SimpleLauncher.Avalonia.Services.UIReset;
using SimpleLauncher.Core;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the Avalonia UiResetService: verifies the reset flow clears filters,
///     selections, pagination, and calls DisplaySystemSelectionScreenAsync on the host
///     (WPF UiResetService parity).
/// </summary>
public class UiResetServiceTests
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Mock<IUiResetHost> _host;
    private readonly UiResetService _service;

    public UiResetServiceTests()
    {
        _host = new Mock<IUiResetHost>();
        _host.Setup(h => h.CurrentCancellationToken).Returns(_cts.Token);
        _host.Setup(h => h.IsUiUpdating).Returns(false);
        _host.Setup(h => h.IsLoadingGames).Returns(false);

        _service = new UiResetService(TestDependencies.Logger().Object);
        _service.Initialize(_host.Object);
    }

    [Fact]
    public async Task ResetUiAsync_ClearsFiltersAndSelections()
    {
        await _service.ResetUiAsync();

        _host.Verify(h => h.SetSearchTextBoxText(""), Times.Once);
        // ReSharper disable once PlaceAssignmentExpressionIntoBlock
        _host.VerifySet(h => h.CurrentFilter = null, Times.Once);
        // ReSharper disable once PlaceAssignmentExpressionIntoBlock
        _host.VerifySet(h => h.ActiveSearchQueryOrMode = null, Times.Once);
        // ReSharper disable once PlaceAssignmentExpressionIntoBlock
        _host.VerifySet(h => h.SelectedSystem = null, Times.Once);
        _host.Verify(h => h.ClearPreviewImage(), Times.Once);
        _host.Verify(h => h.SetSystemComboBoxSelectedItem(null), Times.Once);
        _host.Verify(h => h.SetEmulatorComboBoxSelectedItem(null), Times.Once);
        _host.Verify(h => h.ResetPaginationButtons(), Times.Once);
    }

    [Fact]
    public async Task ResetUiAsync_DisplaysSystemSelectionScreen()
    {
        await _service.ResetUiAsync();

        _host.Verify(h => h.DisplaySystemSelectionScreenAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetUiAsync_HidesLoadingOverlay_WhenLoadingGames()
    {
        _host.Setup(h => h.IsLoadingGames).Returns(true);

        await _service.ResetUiAsync();

        _host.Verify(h => h.SetLoadingOverlayVisible(false), Times.Once);
        // ReSharper disable once PlaceAssignmentExpressionIntoBlock
        _host.VerifySet(h => h.IsLoadingGames = false, Times.Once);
    }

    [Fact]
    public async Task ResetUiAsync_DoesNotEnter_WhenUiIsUpdating()
    {
        _host.Setup(h => h.IsUiUpdating).Returns(true);

        await _service.ResetUiAsync();

        // Should not touch any UI state
        _host.Verify(h => h.SetSearchTextBoxText(It.IsAny<string>()), Times.Never);
        _host.Verify(h => h.ResetPaginationButtons(), Times.Never);
    }

    [Fact]
    public async Task ResetUiAsync_SetsMameSortOrderToFileName()
    {
        await _service.ResetUiAsync();

        // ReSharper disable once PlaceAssignmentExpressionIntoBlock
        _host.VerifySet(h => h.MameSortOrder = AppConstants.MameSortOrderFileName, Times.Once);
    }

    [Fact]
    public async Task ResetUiAsync_CancelsAndRecreatesToken()
    {
        await _service.ResetUiAsync();

        _host.Verify(h => h.CancelAndRecreateToken(), Times.Once);
    }

    [Fact]
    public async Task ResetUiAsync_SetsIsUiUpdating_GuardFlag()
    {
        var isUpdatingSequence = new List<bool>();
        _host.SetupSet(h => h.IsUiUpdating = It.IsAny<bool>())
            .Callback<bool>(isUpdatingSequence.Add);

        // IsUiUpdating getter returns false initially, then true during body
        var getSequence = new Queue<bool>([false, true]);
        _host.Setup(h => h.IsUiUpdating).Returns(() => getSequence.Count > 0 && getSequence.Dequeue());

        await _service.ResetUiAsync();

        // Should set true before body, false after
        Assert.Contains(true, isUpdatingSequence);
        Assert.Contains(false, isUpdatingSequence);
    }
}