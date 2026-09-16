using SimpleLauncher.Avalonia.Services;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Regression tests for the pre-connect log buffer: it must stay bounded
///     (the old unbounded list grew forever in long-running sessions).
/// </summary>
public class DebugWindowSinkBufferTests
{
    [Fact]
    public void BufferMessage_CapsBufferedMessages()
    {
        DebugWindowSink.Disconnect();
        try
        {
            for (var i = 0; i < DebugWindowSink.MaxBufferedMessages + 25; i++)
                DebugWindowSink.BufferMessage($"buffered {i}");

            Assert.Equal(DebugWindowSink.MaxBufferedMessages, DebugWindowSink.BufferedMessageCount);
        }
        finally
        {
            DebugWindowSink.ClearBuffer();
            DebugWindowSink.Disconnect();
        }
    }
}
