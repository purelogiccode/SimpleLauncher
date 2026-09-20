using System.Diagnostics;
using Avalonia.Controls;
using Moq;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.GameFileWatcher;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the Phase 3 menu services (<see cref="AvaloniaLanguageMenuService" />,
///     <see cref="AvaloniaMenuCheckMarkService" />) and the game file watcher wrapper
///     (<see cref="AvaloniaGameFileWatcherService" />, real FileSystemWatcher on temp folders).
/// </summary>
public class AvaloniaLanguageMenuAndWatcherServiceTests
{
    // ── AvaloniaLanguageMenuService ──

    [Fact]
    public void NameToCode_MatchesTheCanonicalLanguageSet()
    {
        var menuCodes = AvaloniaLanguageMenuService.NameToCode.Values
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
        var canonical = LocalizationService.AvailableLanguages.Keys
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(canonical, menuCodes);
        Assert.Equal(18, menuCodes.Count);
    }

    [Fact]
    public void GetLanguageCodeFromMenuItemName_ReturnsCode()
    {
        Assert.Equal("pt-BR", AvaloniaLanguageMenuService.GetLanguageCodeFromMenuItemName("LanguagePortugueseBr"));
        Assert.Equal("zh-Hans", AvaloniaLanguageMenuService.GetLanguageCodeFromMenuItemName("LanguageChineseSimplified"));
        Assert.Null(AvaloniaLanguageMenuService.GetLanguageCodeFromMenuItemName("NotALanguageItem"));
        Assert.Null(AvaloniaLanguageMenuService.GetLanguageCodeFromMenuItemName(null));
    }

    [Fact]
    public void GetMenuItemNameForLanguageCode_IsCaseInsensitive()
    {
        Assert.Equal("LanguagePortugueseBr", AvaloniaLanguageMenuService.GetMenuItemNameForLanguageCode("pt-BR"));
        Assert.Equal("LanguagePortugueseBr",
            AvaloniaLanguageMenuService.GetMenuItemNameForLanguageCode("pt-br")); // WPF-style code
        Assert.Equal("LanguageChineseSimplified", AvaloniaLanguageMenuService.GetMenuItemNameForLanguageCode("zh-hans"));
        Assert.Null(AvaloniaLanguageMenuService.GetMenuItemNameForLanguageCode("xx"));
    }

    [Fact]
    public void IsLanguageMenuItem_DetectsLanguageItemsOnly()
    {
        Assert.True(AvaloniaLanguageMenuService.IsLanguageMenuItem("LanguageEnglish"));
        Assert.True(AvaloniaLanguageMenuService.IsLanguageMenuItem("LanguageUrdu"));
        Assert.False(AvaloniaLanguageMenuService.IsLanguageMenuItem("LanguageGroup")); // group name, not an item
        Assert.False(AvaloniaLanguageMenuService.IsLanguageMenuItem(null));
    }

    // ── AvaloniaMenuCheckMarkService ──

    [Fact]
    public void UpdateCheckedByTag_ChecksExactlyTheMatchingItem()
    {
        HeadlessAvalonia.EnsureInitialized();
        var items = new[]
        {
            new MenuItem { Tag = "100" },
            new MenuItem { Tag = "200" },
            new MenuItem { Tag = "300" }
        };
        AvaloniaMenuCheckMarkService.UpdateCheckedByTag(items, 200);

        Assert.False(items[0].IsChecked);
        Assert.True(items[1].IsChecked);
        Assert.False(items[2].IsChecked);
    }

    [Fact]
    public void UpdateCheckedByTag_NonNumericTagsAreNeverChecked()
    {
        HeadlessAvalonia.EnsureInitialized();
        var items = new[] { new MenuItem { Tag = "abc" }, new MenuItem { Tag = null } };
        AvaloniaMenuCheckMarkService.UpdateCheckedByTag(items, 100);

        Assert.All(items, i => Assert.False(i.IsChecked));
    }

    [Fact]
    public void UpdateCheckedByName_ChecksExactlyTheMatchingItem()
    {
        HeadlessAvalonia.EnsureInitialized();
        var items = new[]
        {
            new MenuItem { Name = "Square" },
            new MenuItem { Name = "Wider" },
            new MenuItem { Name = "Taller" }
        };
        AvaloniaMenuCheckMarkService.UpdateCheckedByName(items, "Taller");

        Assert.False(items[0].IsChecked);
        Assert.False(items[1].IsChecked);
        Assert.True(items[2].IsChecked);
    }

    // ── AvaloniaGameFileWatcherService ──

    [Fact]
    public async Task StartWatching_RaisesGameFilesChanged_WhenAFileAppears()
    {
        HeadlessAvalonia.EnsureInitialized();
        using var tempDir = new TempDirectory();
        var coreWatcher = new GameFileWatcherService(new Mock<ILogger>().Object);
        using var watcher = new AvaloniaGameFileWatcherService(coreWatcher, new Mock<ILogger>().Object);
        watcher.DebounceDelay = TimeSpan.FromMilliseconds(50);

        var changed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.GameFilesChanged += (_, e) => changed.TrySetResult(e.Value);

        watcher.StartWatchingForSystems(
        [
            new SystemManagerConfig
            {
                SystemName = "Arcade",
                SystemFolders = [tempDir.Path],
                FileFormatsToSearch = [".zip"]
            }
        ]);

        File.WriteAllText(Path.Combine(tempDir.Path, "new.zip"), "x");

        var completed = await Task.WhenAny(changed.Task, Task.Delay(10000));
        Assert.True(completed == changed.Task, "GameFilesChanged was not raised within 10 seconds.");
        Assert.Equal("Arcade", await changed.Task);
    }

    [Fact]
    public async Task StopWatching_SuppressesFurtherEvents()
    {
        HeadlessAvalonia.EnsureInitialized();
        using var tempDir = new TempDirectory();
        var coreWatcher = new GameFileWatcherService(new Mock<ILogger>().Object);
        using var watcher = new AvaloniaGameFileWatcherService(coreWatcher, new Mock<ILogger>().Object);
        watcher.DebounceDelay = TimeSpan.FromMilliseconds(30);

        var eventCount = 0;
        watcher.GameFilesChanged += (_, _) => Interlocked.Increment(ref eventCount);

        watcher.StartWatchingForSystems(
        [
            new SystemManagerConfig
            {
                SystemName = "Arcade",
                SystemFolders = [tempDir.Path],
                FileFormatsToSearch = [".zip"]
            }
        ]);

        File.WriteAllText(Path.Combine(tempDir.Path, "one.zip"), "x");
        await WaitForAsync(() => eventCount == 1);

        watcher.StopWatching();
        var countAfterStop = eventCount;

        File.WriteAllText(Path.Combine(tempDir.Path, "two.zip"), "x");
        await Task.Delay(400);
        Assert.Equal(countAfterStop, eventCount);
    }

    [Fact]
    public void StartWatching_WithNoValidFolders_DoesNotThrow()
    {
        HeadlessAvalonia.EnsureInitialized();
        var coreWatcher = new GameFileWatcherService(new Mock<ILogger>().Object);
        using var watcher = new AvaloniaGameFileWatcherService(coreWatcher, new Mock<ILogger>().Object);

        watcher.StartWatchingForSystems(
        [
            new SystemManagerConfig
            {
                SystemName = "Arcade",
                SystemFolders = [@"C:\does\not\exist"],
                FileFormatsToSearch = [".zip"]
            }
        ]);

        watcher.StopWatching();
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 10000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs) throw new TimeoutException("Condition not met within the timeout.");

            await Task.Delay(5);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "sl_av_watcher_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}