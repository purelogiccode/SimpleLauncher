using SimpleLauncher.Avalonia.Services.GameLauncher;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the Flatpak host process argument splitter. The emulator argument string
///     must reach the host process as real argv entries, never through a shell.
/// </summary>
public class CommandLineSplitterTests
{
    [Fact]
    public void SplitsPlainArguments()
    {
        Assert.Equal(["--fullscreen", "-f"], CommandLineSplitter.Split("--fullscreen -f"));
    }

    [Fact]
    public void PreservesQuotedPathsWithSpaces()
    {
        Assert.Equal(
            ["-L", "/home/user/roms/Test System/Alpha Quest.nes", "-f"],
            CommandLineSplitter.Split("-L \"/home/user/roms/Test System/Alpha Quest.nes\" -f"));
    }

    [Fact]
    public void DoesNotInterpretShellMetacharacters()
    {
        Assert.Equal(
            ["/home/user/roms/$(touch pwned).nes"],
            CommandLineSplitter.Split("\"/home/user/roms/$(touch pwned).nes\""));
    }

    [Fact]
    public void HandlesSingleQuotes()
    {
        Assert.Equal(["a b", "c"], CommandLineSplitter.Split("'a b' c"));
    }

    [Fact]
    public void HandlesEscapedQuotesAndBackslashes()
    {
        Assert.Equal(["a\"b", "c\\d"], CommandLineSplitter.Split("\"a\\\"b\" c\\\\d"));
    }

    [Fact]
    public void KeepsEmptyQuotedArgument()
    {
        Assert.Equal(["", "foo"], CommandLineSplitter.Split("\"\" foo"));
    }

    [Fact]
    public void EmptyCommandLineHasNoArguments()
    {
        Assert.Empty(CommandLineSplitter.Split(""));
        Assert.Empty(CommandLineSplitter.Split("   "));
    }
}
