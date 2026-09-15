using System.Collections.ObjectModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Avalonia.Services;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the debug window, managing log message collection and display.
///     Avalonia port of the WPF DebugViewModel.
///     <para>
///     Performance (AV-18): log lines arrive in verbose bursts from any thread. The
///     previous implementation rebuilt the full <see cref="LogText" /> via
///     <c>string.Join</c> over up to 5000 entries on <i>every</i> appended line and
///     shifted the <see cref="ObservableCollection{T}" /> front per eviction, which
///     froze the UI. Appends are now coalesced: producers only enqueue into a
///     pending queue and schedule a single UI-thread flush, which applies the whole
///     batch with one eviction pass and one <see cref="LogText" /> rebuild. The
///     debug window binds only to <see cref="LogText" />, so a burst of N lines
///     costs one text update instead of N.
///     </para>
/// </summary>
public partial class DebugViewModel : ObservableObject
{
    private const int MaxMessageCount = 5000;
    private readonly StringBuilder _logBuilder = new();
    private readonly Lock _logLock = new();
    private readonly Queue<string> _pendingMessages = new();
    private string _logText = "";
    private bool _flushScheduled;

    /// <summary>Initializes a new instance of the <see cref="DebugViewModel" /> and connects to the debug window sink.</summary>
    public DebugViewModel()
    {
        DebugWindowSink.Connect(this);
    }

    /// <summary>Gets the collection of formatted log messages.</summary>
    public ObservableCollection<string> LogMessages { get; } = [];

    /// <summary>Gets the full log text for display or clipboard operations.</summary>
    public string LogText
    {
        get => _logText;
        private set => SetProperty(ref _logText, value);
    }

    /// <summary>Gets whether there are log messages that can be cleared.</summary>
    public bool CanClearLog => LogMessages.Count > 0;

    /// <summary>Gets whether there is log text that can be copied to the clipboard.</summary>
    public bool CanCopyLog => !string.IsNullOrEmpty(LogText);

    /// <summary>Appends a formatted log message to the log collection, evicting old entries if the limit is exceeded.</summary>
    /// <param name="formattedMessage">The formatted log message to append.</param>
    public void AppendLogMessage(string formattedMessage)
    {
        EnqueuePending([formattedMessage]);
    }

    /// <summary>Loads a batch of pre-formatted log messages into the log collection.</summary>
    /// <param name="formattedMessages">The collection of formatted messages to load.</param>
    public void LoadBufferedMessages(IEnumerable<string> formattedMessages)
    {
        EnqueuePending(formattedMessages);
    }

    /// <summary>
    ///     Thread-safe enqueue; the batch is applied on the UI thread by a single
    ///     coalesced flush (one eviction pass + one <see cref="LogText" /> rebuild
    ///     per burst instead of per line).
    /// </summary>
    private void EnqueuePending(IEnumerable<string> formattedMessages)
    {
        lock (_logLock)
        {
            foreach (var msg in formattedMessages) _pendingMessages.Enqueue(msg);

            if (_flushScheduled) return;
            _flushScheduled = true;
        }

        Dispatcher.UIThread.Post(FlushPendingMessages);
    }

    /// <summary>
    ///     Applies all pending messages on the UI thread (the only thread that ever
    ///     touches <see cref="LogMessages" /> and <see cref="_logBuilder" />).
    /// </summary>
    private void FlushPendingMessages()
    {
        List<string> batch;
        lock (_logLock)
        {
            if (_pendingMessages.Count == 0)
            {
                _flushScheduled = false;
                return;
            }

            batch = new List<string>(_pendingMessages);
            _pendingMessages.Clear();
            _flushScheduled = false;
        }

        ApplyBatch(batch);

        LogText = _logBuilder.ToString();
        OnPropertyChanged(nameof(CanClearLog));
        OnPropertyChanged(nameof(CanCopyLog));
        ClearLogCommand.NotifyCanExecuteChanged();
        CopyLogCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Appends a batch to <see cref="LogMessages" /> / <see cref="_logBuilder" />,
    ///     evicting the oldest entries in a single pass when over
    ///     <see cref="MaxMessageCount" />. Must run on the UI thread.
    /// </summary>
    private void ApplyBatch(List<string> batch)
    {
        if (batch.Count == 0) return;

        var overflow = LogMessages.Count + batch.Count - MaxMessageCount;
        if (overflow > 0)
        {
            if (overflow >= LogMessages.Count)
            {
                // The batch alone exceeds capacity: drop everything and keep its tail.
                LogMessages.Clear();
                _logBuilder.Clear();
                batch = batch.Skip(batch.Count - MaxMessageCount).ToList();
            }
            else
            {
                // Bulk eviction without per-item front shifts: rebuild once instead
                // of RemoveAt(0) per line (O(n) shift each).
                var kept = LogMessages.Skip(overflow).Concat(batch).ToList();
                LogMessages.Clear();
                _logBuilder.Clear();
                batch = kept;
            }
        }

        foreach (var msg in batch)
        {
            LogMessages.Add(msg);
            _logBuilder.Append(msg).Append(Environment.NewLine);
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearLog))]
    private void ClearLog()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.InvokeAsync(ClearLog);
            return;
        }

        lock (_logLock)
        {
            // Drop not-yet-flushed lines so a queued flush cannot resurrect
            // messages after the clear. Dispatcher FIFO ordering keeps an
            // already-drained flush (posted before this clear) applied first.
            _pendingMessages.Clear();
            _flushScheduled = false;
        }

        LogMessages.Clear();
        _logBuilder.Clear();
        LogText = "";
        OnPropertyChanged(nameof(CanClearLog));
        OnPropertyChanged(nameof(CanCopyLog));
        ClearLogCommand.NotifyCanExecuteChanged();
        CopyLogCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanCopyLog))]
    private async Task CopyLogAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(LogText) &&
                Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime &&
                TopLevel.GetTopLevel(lifetime.MainWindow)?.Clipboard is { } clipboard)
            {
                var dataTransfer = new DataTransfer();
                dataTransfer.Add(DataTransferItem.CreateText(LogText));
                await clipboard.SetDataAsync(dataTransfer);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error copying log to clipboard");
        }
    }
}
