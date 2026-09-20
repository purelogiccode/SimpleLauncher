using Moq;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for <see cref="AvaloniaPaginationService" /> (Phase 3) — page slicing,
///     button states via a fake <see cref="IPaginationHost" />, and the localized label.
/// </summary>
public class AvaloniaPaginationServiceTests
{
    private static (AvaloniaPaginationService Service, FakePaginationHost Host) Create(int filesPerPage = 10,
        int threshold = 10)
    {
        var resourceProvider = TestDependencies.ResourceProvider().Object;
        var service = new AvaloniaPaginationService(resourceProvider)
        {
            FilesPerPage = filesPerPage,
            PaginationThreshold = threshold
        };
        var host = new FakePaginationHost();
        service.Initialize(host);
        return (service, host);
    }

    private static List<string> Files(int count)
    {
        return Enumerable.Range(1, count).Select(i => $"game{i}.zip").ToList();
    }

    [Fact]
    public void ApplyPagination_UnderThreshold_ReturnsAllFilesAndDisablesButtons()
    {
        var (service, host) = Create(10, 10);
        var files = Files(8);

        var result = service.ApplyPagination(files);

        Assert.Equal(files, result);
        Assert.False(host.PrevEnabled);
        Assert.False(host.NextEnabled);
        Assert.Equal("Displaying files 1 to 8 out of 8 total", host.Label);
        Assert.Equal(1, service.CurrentPage);
    }

    [Fact]
    public void ApplyPagination_OverThreshold_ReturnsFirstPageAndEnablesNext()
    {
        var (service, host) = Create(10, 10);

        var result = service.ApplyPagination(Files(25));

        Assert.Equal(10, result.Count);
        Assert.Equal("game1.zip", result[0]);
        Assert.Equal("game10.zip", result[^1]);
        Assert.False(host.PrevEnabled);
        Assert.True(host.NextEnabled);
        Assert.Equal("Displaying files 1 to 10 out of 25 total", host.Label);
    }

    [Fact]
    public void GoToNextPage_ThenReapply_ReturnsSecondPage()
    {
        var (service, host) = Create();
        service.ApplyPagination(Files(25));

        service.GoToNextPage();
        var result = service.ApplyPagination(Files(25));

        Assert.Equal(2, service.CurrentPage);
        Assert.Equal("game11.zip", result[0]);
        Assert.Equal("game20.zip", result[^1]);
        Assert.True(host.PrevEnabled);
        Assert.True(host.NextEnabled);
        Assert.Equal("Displaying files 11 to 20 out of 25 total", host.Label);
    }

    [Fact]
    public void ApplyPagination_ListShrinksBelowCurrentPage_ClampsToLastPage()
    {
        var (service, host) = Create(10, 10);
        service.ApplyPagination(Files(25));
        service.GoToNextPage();
        service.GoToNextPage();
        service.ApplyPagination(Files(25));
        Assert.Equal(3, service.CurrentPage);

        var result = service.ApplyPagination(Files(12));

        Assert.Equal(2, service.CurrentPage);
        Assert.Equal("game11.zip", result[0]);
        Assert.Equal("game12.zip", result[^1]);
        Assert.Equal("Displaying files 11 to 12 out of 12 total", host.Label);
    }

    [Fact]
    public void GoToNextPage_OnLastPage_DoesNotMove()
    {
        var (service, _) = Create(10, 10);
        service.ApplyPagination(Files(25));

        service.GoToNextPage(); // page 2
        service.GoToNextPage(); // page 3
        Assert.Equal(3, service.CurrentPage);
        Assert.False(service.CanGoNext());

        service.GoToNextPage(); // stays on 3
        Assert.Equal(3, service.CurrentPage);
    }

    [Fact]
    public void GoToPreviousPage_OnFirstPage_DoesNotMove()
    {
        var (service, _) = Create(10, 10);

        Assert.False(service.CanGoPrev());
        service.GoToPreviousPage();
        Assert.Equal(1, service.CurrentPage);
    }

    [Fact]
    public void ApplyPagination_EmptyList_AddsNoFilesMessageAndZeroLabel()
    {
        var (service, host) = Create();

        var result = service.ApplyPagination([]);

        Assert.Empty(result);
        Assert.Equal(1, host.NoFilesMessageCount);
        Assert.False(host.PrevEnabled);
        Assert.False(host.NextEnabled);
        Assert.Equal("Displaying files 0 to 0 out of 0 total", host.Label);
    }

    [Fact]
    public void Reset_ReturnsToFirstPageAndDisablesButtons()
    {
        var (service, host) = Create();
        service.ApplyPagination(Files(25));
        service.GoToNextPage();
        service.ApplyPagination(Files(25));

        service.Reset();

        Assert.Equal(1, service.CurrentPage);
        Assert.False(host.PrevEnabled);
        Assert.False(host.NextEnabled);
        Assert.Null(host.Label);
        Assert.Equal(1, host.ScrollToTopCount);
    }

    [Fact]
    public void ApplyPagination_UsesLocalizedLabelFromResourceProvider()
    {
        var resourceProvider = new Mock<IResourceProvider>();
        resourceProvider.Setup(r => r.GetString("Pagination.Displaying", It.IsAny<string>()))
            .Returns("Mostrando archivos del {0} al {1} de {2} en total");
        var service = new AvaloniaPaginationService(resourceProvider.Object)
        {
            FilesPerPage = 10,
            PaginationThreshold = 10
        };
        var host = new FakePaginationHost();
        service.Initialize(host);

        service.ApplyPagination(Files(5));

        Assert.Equal("Mostrando archivos del 1 al 5 de 5 en total", host.Label);
    }

    private sealed class FakePaginationHost : IPaginationHost
    {
        public bool PrevEnabled { get; private set; }
        public bool NextEnabled { get; private set; }
        public int ScrollToTopCount { get; private set; }
        public string? Label { get; private set; }
        public int NoFilesMessageCount { get; private set; }

        public void SetPrevPageButtonEnabled(bool enabled)
        {
            PrevEnabled = enabled;
        }

        public void SetNextPageButtonEnabled(bool enabled)
        {
            NextEnabled = enabled;
        }

        public void ScrollToTop()
        {
            ScrollToTopCount++;
        }

        public void UpdateTotalFilesLabel(string? text)
        {
            Label = text;
        }

        public void AddNoFilesMessage()
        {
            NoFilesMessageCount++;
        }
    }
}