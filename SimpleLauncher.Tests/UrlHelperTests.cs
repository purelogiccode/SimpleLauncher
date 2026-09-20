using SimpleLauncher.Core.Services;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="UrlHelper" />: only absolute http/https URLs may be launched
///     (WPF-04/WPF-05 allowlist).
/// </summary>
public class UrlHelperTests
{
    [Theory]
    [InlineData("https://example.com/game.zip", true)]
    [InlineData("http://example.com/page?q=1", true)]
    [InlineData("  https://example.com/padded  ", true)]
    [InlineData("HTTPS://EXAMPLE.COM/UPPER", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("file:///C:/evil.exe", false)]
    [InlineData("ms-msdt:/id/anything", false)]
    [InlineData("search-ms:query=game", false)]
    [InlineData(@"C:\evil.exe --payload", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("example.com/no-scheme", false)]
    [InlineData("//example.com/protocol-relative", false)]
    public void IsHttpUrlAllowlistBehavesAsExpected(string? url, bool expected)
    {
        Assert.Equal(expected, UrlHelper.IsHttpUrl(url));
    }
}