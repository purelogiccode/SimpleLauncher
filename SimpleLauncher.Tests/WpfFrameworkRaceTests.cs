using System.Reflection;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Regression tests for bugs 67562/67563/67564: a known WPF weak-event/
///     collection-view race surfaces during DataGrid layout as an NRE inside
///     PropertyChangedEventManager (67562) or an InvalidCastException inside
///     WeakEventManager.DeliverEvent while virtualizing rows are cleaned up
///     (67563/67564). The stacks are entirely framework frames — the dispatcher
///     already handles them — so <c>App.ReportException</c> must log them at
///     Information, never filing a bug report, while any exception with an app
///     frame (or another type) stays at Error.
/// </summary>
[Collection(nameof(GlobalLogMutating))]
public class WpfFrameworkRaceTests
{
    private const string NreStack67562 =
        """
           at System.ComponentModel.PropertyChangedEventManager.OnPropertyChanged(Object sender, PropertyChangedEventArgs args)
           at System.Windows.Data.ListCollectionView.RefreshOverride()
           at System.Windows.Data.CollectionView.RefreshInternal()
           at System.Windows.Data.CollectionView.DeferHelper.Dispose()
           at System.Windows.Controls.ItemCollection.SetCollectionView(CollectionView view)
           at System.Windows.Controls.ItemsControl.OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
           at System.Windows.Controls.Primitives.DataGridCellsPresenter.OnApplyTemplate()
           at System.Windows.FrameworkElement.ApplyTemplate()
        """;

    private const string InvalidCastStack67563 =
        """
           at System.Windows.WeakEventManager.ListenerList`1.DeliverEvent(Object sender, EventArgs e, Type managerType)
           at System.ComponentModel.PropertyChangedEventManager.OnPropertyChanged(Object sender, PropertyChangedEventArgs args)
           at System.Windows.Controls.ItemsControl.OnItemCollectionChanged2(Object sender, NotifyCollectionChangedEventArgs e)
           at System.Windows.Data.ListCollectionView.ProcessCollectionChangedWithAdjustedIndex(NotifyCollectionChangedEventArgs args, Int32 adjustedOldIndex, Int32 adjustedNewIndex)
           at System.Windows.Controls.DataGridRow.ClearRow(DataGrid owningDataGrid)
           at System.Windows.Controls.VirtualizingStackPanel.CleanupContainers(Int32 firstItemInExtendedViewportIndex, Int32 itemsInExtendedViewportCount, ItemsControl itemsControl, Boolean timeBound, Int32 startTickCount)
        """;

    private const string AppFrameStack =
        """
           at SimpleLauncher.MainWindow.LoadGameFilesAsync()
           at System.ComponentModel.PropertyChangedEventManager.OnPropertyChanged(Object sender, PropertyChangedEventArgs args)
           at System.Windows.Data.ListCollectionView.RefreshOverride()
        """;

    /// <summary>
    ///     Real dispatcher stacks always carry the generated application entry point at the
    ///     bottom; the race frames themselves are still entirely framework code.
    /// </summary>
    private const string RealisticDispatcherStack67562 =
        """
           at System.ComponentModel.PropertyChangedEventManager.OnPropertyChanged(Object sender, PropertyChangedEventArgs args)
           at System.Windows.Data.ListCollectionView.RefreshOverride()
           at System.Windows.Data.CollectionView.RefreshInternal()
           at System.Windows.Controls.ItemCollection.SetCollectionView(CollectionView view)
           at System.Windows.Controls.ItemsControl.OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
           at System.Windows.Threading.DispatcherOperation.InvokeDelegateCore()
           at System.Windows.Threading.DispatcherOperation.InvokeImpl()
           at System.Windows.Threading.Dispatcher.PushFrameImpl(DispatcherFrame frame)
           at System.Windows.Threading.Dispatcher.PushFrame(DispatcherFrame frame)
           at System.Windows.Threading.Dispatcher.Run()
           at System.Windows.Application.RunDispatcher(Object ignore)
           at System.Windows.Application.RunInternal(Window window)
           at System.Windows.Application.Run()
           at SimpleLauncher.App.Main()
        """;

    /// <summary>
    ///     Same dispatcher entry frame, but with a real application frame above it: the
    ///     exception originates in our code and must stay at Error.
    /// </summary>
    private const string RealisticAppBugStack =
        """
           at System.ComponentModel.PropertyChangedEventManager.OnPropertyChanged(Object sender, PropertyChangedEventArgs args)
           at SimpleLauncher.MainWindow.RefreshGameList()
           at System.Windows.Threading.DispatcherOperation.InvokeDelegateCore()
           at System.Windows.Threading.Dispatcher.Run()
           at SimpleLauncher.App.Main()
        """;

    /// <summary>
    ///     The 67562 stack shape (NRE in PropertyChangedEventManager during an
    ///     ItemsSource change) is recognized as the framework race.
    /// </summary>
    [Fact]
    public void NreInPropertyChangedEventManager_IsClassifiedAsFrameworkRace()
    {
        Assert.True(InvokePredicate(new NullReferenceException("Object reference not set to an instance of an object."),
            NreStack67562));
    }

    /// <summary>
    ///     The 67563/67564 stack shape (InvalidCast in WeakEventManager during
    ///     DataGrid row cleanup) is recognized as the same framework race.
    /// </summary>
    [Fact]
    public void InvalidCastInWeakEventManager_IsClassifiedAsFrameworkRace()
    {
        Assert.True(InvokePredicate(
            new InvalidCastException("Unable to cast object of type 'System.Windows.Controls.ItemCollection'."),
            InvalidCastStack67563));
    }

    /// <summary>
    ///     Any SimpleLauncher frame on the stack means it is our code: the race
    ///     classifier must reject it so it keeps being logged at Error.
    /// </summary>
    [Fact]
    public void RaceLikeStackWithAppFrame_IsNotClassifiedAsFrameworkRace()
    {
        Assert.False(InvokePredicate(new NullReferenceException("boom"), AppFrameStack));
    }

    /// <summary>
    ///     A real dispatcher stack (which always carries the generated App.Main entry
    ///     frame at the bottom) is still classified as the framework race.
    /// </summary>
    [Fact]
    public void RealisticDispatcherStack_WithEntryPointFrameOnly_IsClassifiedAsFrameworkRace()
    {
        Assert.True(InvokePredicate(new NullReferenceException("boom"), RealisticDispatcherStack67562));
    }

    /// <summary>
    ///     A real dispatcher stack with an application frame above the entry point is
    ///     our code: it must stay at Error.
    /// </summary>
    [Fact]
    public void RealisticDispatcherStack_WithAppFrame_IsNotClassifiedAsFrameworkRace()
    {
        Assert.False(InvokePredicate(new NullReferenceException("boom"), RealisticAppBugStack));
    }

    /// <summary>
    ///     Only NRE/InvalidCast are covered: other exception types with a
    ///     framework-only race-looking stack stay at Error.
    /// </summary>
    [Fact]
    public void OtherExceptionType_IsNotClassifiedAsFrameworkRace()
    {
        Assert.False(InvokePredicate(new InvalidOperationException("real bug"), NreStack67562));
    }

    /// <summary>
    ///     An exception that was never thrown has no stack: not classified.
    /// </summary>
    [Fact]
    public void NullStackTrace_IsNotClassifiedAsFrameworkRace()
    {
        Assert.False(InvokePredicate(new NullReferenceException("never thrown"), null));
    }

    /// <summary>
    ///     End-to-end: ReportException logs the 67562 shape at Information —
    ///     no Warning/Error, so the bug sink never sees it.
    /// </summary>
    [Fact]
    public void ReportException_NreFrameworkRaceStack_IsLoggedAtInformation()
    {
        var sink = new CaptureSink();
        var previous = Log.Logger;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
        try
        {
            InvokeReportException(new FakeStackNre(NreStack67562), "Unhandled dispatcher exception.");

            Assert.DoesNotContain(sink.Events, static e => e.Level >= LogEventLevel.Warning);
            Assert.Contains(sink.Events, static e => e.Level == LogEventLevel.Information);
        }
        finally
        {
            Log.Logger = previous;
        }
    }

    /// <summary>
    ///     End-to-end: ReportException logs the 67563/67564 InvalidCast shape at
    ///     Information as well.
    /// </summary>
    [Fact]
    public void ReportException_InvalidCastFrameworkRaceStack_IsLoggedAtInformation()
    {
        var sink = new CaptureSink();
        var previous = Log.Logger;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
        try
        {
            InvokeReportException(new FakeStackInvalidCast(InvalidCastStack67563),
                "Unhandled dispatcher exception.");

            Assert.DoesNotContain(sink.Events, static e => e.Level >= LogEventLevel.Warning);
            Assert.Contains(sink.Events, static e => e.Level == LogEventLevel.Information);
        }
        finally
        {
            Log.Logger = previous;
        }
    }

    /// <summary>
    ///     End-to-end: a race-looking stack that contains an app frame is a real
    ///     app bug and must still be logged at Error.
    /// </summary>
    [Fact]
    public void ReportException_RaceStackWithAppFrame_IsStillLoggedAtError()
    {
        var sink = new CaptureSink();
        var previous = Log.Logger;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
        try
        {
            InvokeReportException(new FakeStackNre(AppFrameStack), "Unhandled dispatcher exception.");

            Assert.Contains(sink.Events, static e => e.Level >= LogEventLevel.Error);
        }
        finally
        {
            Log.Logger = previous;
        }
    }

    private static bool InvokePredicate(Exception ex, string? stackTrace)
    {
        var method = typeof(App).GetMethod("IsKnownWpfFrameworkRace", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(null, [ex, stackTrace]);
        return Assert.IsType<bool>(result);
    }

    private static void InvokeReportException(Exception exception, string contextMessage)
    {
        var method = typeof(App).GetMethod("ReportException", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(null, [exception, contextMessage]);
    }

    /// <summary>NRE whose StackTrace is fabricated from the bug-report stacks.</summary>
    private sealed class FakeStackNre(string stackTrace) : NullReferenceException(
        "Object reference not set to an instance of an object.")
    {
        public override string StackTrace => stackTrace;
    }

    /// <summary>InvalidCast whose StackTrace is fabricated from the bug-report stacks.</summary>
    private sealed class FakeStackInvalidCast(string stackTrace) : InvalidCastException(
        "Unable to cast object of type 'System.Windows.Controls.ItemCollection' to type 'System.ComponentModel.PropertyChangedEventArgs'.")
    {
        public override string StackTrace => stackTrace;
    }

    /// <summary>Serilog sink that records every emitted event for level assertions.</summary>
    private sealed class CaptureSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            lock (Events)
            {
                Events.Add(logEvent);
            }
        }
    }
}