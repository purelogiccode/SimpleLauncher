using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Avalonia.Services.LoadingOverlay;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the reference-counted loading overlay service (WPF LoadingOverlayService
///     parity): nested operations keep the overlay visible, the last decrement hides it
///     and resets the message, and EmergencyRelease force-resets the counter and the UI.
/// </summary>
public class AvaloniaLoadingOverlayServiceTests
{
    private static (AvaloniaLoadingOverlayService Service, FakeLoadingOverlayHost Host) Create()
    {
        var settings = TestDependencies.Settings();
        var service = new AvaloniaLoadingOverlayService(TestDependencies.PlaySound(settings));
        var host = new FakeLoadingOverlayHost();
        service.Initialize(host);
        return (service, host);
    }

    [Fact]
    public async Task SetLoadingState_ReferenceCountsNestedOperations()
    {
        var (service, host) = Create();

        service.SetLoadingState(true, "First");
        service.SetLoadingState(true, "Second");
        await HeadlessAvalonia.WaitUntilAsync(() =>
            string.Equals(host.Message, "Second", StringComparison.Ordinal));
        Assert.True(host.IsLoading);

        // One of two operations finished: the overlay must stay visible.
        service.SetLoadingState(false);
        await Task.Delay(50);
        Assert.True(host.IsLoading);

        service.SetLoadingState(false);
        await HeadlessAvalonia.WaitUntilAsync(() => !host.IsLoading);
        Assert.Equal("Loading…", host.Message);
    }

    [Fact]
    public async Task SetLoadingState_WithoutMessage_KeepsCurrentMessage()
    {
        var (service, host) = Create();

        service.SetLoadingState(true, "First");
        await HeadlessAvalonia.WaitUntilAsync(() =>
            string.Equals(host.Message, "First", StringComparison.Ordinal));

        // A message-less increment (WPF parity) must not clear the current message.
        service.SetLoadingState(true);
        await Task.Delay(50);
        Assert.Equal("First", host.Message);
        Assert.True(host.IsLoading);
    }

    [Fact]
    public async Task EmergencyRelease_ResetsCounterAndUi()
    {
        var (service, host) = Create();

        service.SetLoadingState(true, "Stuck");
        await HeadlessAvalonia.WaitUntilAsync(() => host.IsLoading);

        service.EmergencyRelease();

        await HeadlessAvalonia.WaitUntilAsync(() => !host.IsLoading);
        Assert.True(host.MainContentEnabled);
        Assert.Equal(1, host.CancelTokenCalls);
        Assert.Equal(1, host.ResetUiCalls);
        Assert.Equal("Loading…", host.Message);

        // The counter was force-reset: a subsequent operation still shows and hides cleanly.
        service.SetLoadingState(true, "Again");
        await HeadlessAvalonia.WaitUntilAsync(() => host.IsLoading);
        service.SetLoadingState(false);
        await HeadlessAvalonia.WaitUntilAsync(() => !host.IsLoading);
    }
}

/// <summary>Recording <see cref="IAvaloniaLoadingOverlayHost" /> for overlay tests.</summary>
internal sealed class FakeLoadingOverlayHost : IAvaloniaLoadingOverlayHost
{
    private readonly Lock _lock = new();
    private readonly List<bool> _states = [];
    private readonly List<string> _messages = [];

    public bool IsLoading
    {
        get
        {
            lock (_lock)
            {
                return _states.Count > 0 && _states[^1];
            }
        }
    }

    public string Message { get; private set; } = "";

    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_lock)
            {
                return [.. _messages];
            }
        }
    }

    public bool MainContentEnabled { get; private set; } = true;
    public int CancelTokenCalls { get; private set; }
    public int ResetUiCalls { get; private set; }

    public IReadOnlyList<bool> States
    {
        get
        {
            lock (_lock)
            {
                return [.. _states];
            }
        }
    }

    public void SetIsLoading(bool isLoading)
    {
        lock (_lock)
        {
            _states.Add(isLoading);
        }
    }

    public void SetLoadingMessage(string message)
    {
        Message = message;
        lock (_lock)
        {
            _messages.Add(message);
        }
    }

    public Task ResetUiAsync()
    {
        ResetUiCalls++;
        return Task.CompletedTask;
    }

    public void CancelAndRecreateToken()
    {
        CancelTokenCalls++;
    }

    public void SetMainContentGridEnabled(bool enabled)
    {
        MainContentEnabled = enabled;
    }
}