using Serilog.Core;
using Serilog.Events;
using SimpleLauncher.Avalonia.ViewModels;

namespace SimpleLauncher.Avalonia.Services;

/// <summary>
///     A Serilog sink that forwards log events to the debug window view model,
///     buffering messages until it is connected. Avalonia port of the WPF DebugWindowSink.
/// </summary>
public class DebugWindowSink : ILogEventSink
{
    internal const int MaxBufferedMessages = 5000;
    private static readonly Lock SinkLock = new();
    private static readonly Queue<string> MessageBuffer = new();
    private static DebugViewModel? _viewModel;

    /// <summary>
    ///     Gets the number of messages currently held in the pre-connect buffer (test hook).
    /// </summary>
    internal static int BufferedMessageCount
    {
        get
        {
            lock (SinkLock)
            {
                return MessageBuffer.Count;
            }
        }
    }

    /// <summary>
    ///     Gets the debug view model currently connected to the sink.
    /// </summary>
    public static DebugViewModel? ViewModel
    {
        get
        {
            lock (SinkLock)
            {
                return _viewModel;
            }
        }
    }

    /// <summary>
    ///     Emits a log event to the sink, appending the formatted message to the buffer and the connected view model.
    /// </summary>
    /// <param name="logEvent">The log event to emit.</param>
    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage();
        var formattedMessage = $"{logEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{logEvent.Level}] {message}";

        lock (SinkLock)
        {
            AppendToBufferLocked(formattedMessage);

            _viewModel?.AppendLogMessage(formattedMessage);
        }
    }

    /// <summary>
    ///     Appends one message to the pre-connect buffer (applying the cap) without
    ///     forwarding it to a connected view model. Test hook: emitting thousands of
    ///     events through the sink would flood the UI dispatcher when a debug view
    ///     model happens to be connected by a parallel test.
    /// </summary>
    /// <param name="formattedMessage">The formatted message to buffer.</param>
    internal static void BufferMessage(string formattedMessage)
    {
        lock (SinkLock)
        {
            AppendToBufferLocked(formattedMessage);
        }
    }

    /// <summary>
    ///     Empties the pre-connect buffer. Test hook: the buffer is static, so a test that
    ///     fills it would otherwise leak thousands of entries into unrelated tests.
    /// </summary>
    internal static void ClearBuffer()
    {
        lock (SinkLock)
        {
            MessageBuffer.Clear();
        }
    }

    /// <summary>
    ///     Enqueues a message and trims the oldest entries beyond the cap. Callers must hold
    ///     <see cref="SinkLock" />.
    /// </summary>
    private static void AppendToBufferLocked(string formattedMessage)
    {
        MessageBuffer.Enqueue(formattedMessage);

        // Ring-buffer behavior: the connected view model keeps at most
        // MaxBufferedMessages lines, so the pre-connect buffer is capped the same way
        // to keep a long-running process from growing without bound.
        if (MessageBuffer.Count > MaxBufferedMessages) MessageBuffer.Dequeue();
    }

    /// <summary>
    ///     Connects the sink to the given debug view model and flushes any buffered messages.
    /// </summary>
    /// <param name="viewModel">The debug view model to connect.</param>
    public static void Connect(DebugViewModel viewModel)
    {
        lock (SinkLock)
        {
            _viewModel = viewModel;
            if (_viewModel != null && MessageBuffer.Count > 0) _viewModel.LoadBufferedMessages(MessageBuffer.ToList());
        }
    }

    /// <summary>
    ///     Disconnects the sink from the currently connected debug view model.
    /// </summary>
    public static void Disconnect()
    {
        lock (SinkLock)
        {
            _viewModel = null;
        }
    }
}