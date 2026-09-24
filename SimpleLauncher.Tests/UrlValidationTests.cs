using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.EasyMode;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests that URLs referenced in parameters.md and EasyMode configuration files are reachable and valid.
/// </summary>
public partial class UrlValidationTests
{
    private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    })
    {
        Timeout = TimeSpan.FromSeconds(20),
        DefaultRequestHeaders = { { "User-Agent", "SimpleLauncherTests/1.0" } }
    };

    private static readonly SemaphoreSlim Semaphore = new(10, 10);

    private static string GetProjectFilePath(string relativePath)
    {
        var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (assemblyLocation == null)
            throw new InvalidOperationException("Could not determine executing assembly location.");

        var path = Path.Combine(assemblyLocation, "..", "..", "..", "..", relativePath);
        return Path.GetFullPath(path);
    }

    /// <summary>
    ///     Verifies that all URLs found in parameters.md are reachable via HTTP requests.
    /// </summary>
    [Fact]
    public async Task ParametersMdAllUrlsAreReachable()
    {
        var filePath = GetProjectFilePath(Path.Combine("SimpleLauncher", "parameters.md"));
        Assert.True(File.Exists(filePath), $"File not found: {filePath}");

        var content = await File.ReadAllTextAsync(filePath);
        var matches = MyRegex().Matches(content);
        var urls = matches
            .Select(static m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.NotEmpty(urls);

        var brokenUrls = new List<string>();
        var tasks = urls.Select(async url =>
        {
            await Semaphore.WaitAsync();
            try
            {
                var error = await CheckUrlAsync(url);
                if (error != null)
                {
                    lock (brokenUrls)
                    {
                        brokenUrls.Add(error);
                    }
                }
            }
            finally
            {
                Semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        if (brokenUrls.Count != 0)
        {
            var message = string.Join(Environment.NewLine, brokenUrls);
            Assert.Fail($"The following URLs in parameters.md are broken:{Environment.NewLine}{message}");
        }
    }

    /// <summary>
    ///     Verifies that the EasyMode API endpoints return valid JSON array responses.
    /// </summary>
    [Theory]
    [InlineData("https://www.purelogiccode.com/simplelauncheradmin/api/Systems/x64")]
    [InlineData("https://www.purelogiccode.com/simplelauncheradmin/api/Systems/arm64")]
    public async Task EasyModeApiEndpointIsReachable(string url)
    {
        using var response = await HttpClient.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode, $"API endpoint {url} returned {(int)response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content), $"API endpoint {url} returned empty content");
        Assert.True(content.TrimStart().StartsWith('['), $"API endpoint {url} did not return a JSON array");
    }

    /// <summary>
    ///     Verifies that all URLs found in the local EasyMode sample XML files are reachable via HTTP requests.
    /// </summary>
    [Theory]
    [InlineData("easymode.xml")]
    [InlineData("easymode_arm64.xml")]
    public async Task EasyModeSampleLocalXmlContainsValidUrls(string xmlFileName)
    {
        var filePath =
            GetProjectFilePath(Path.Combine("SimpleLauncher", "Services", "EasyMode", "Samples", xmlFileName));
        Assert.True(File.Exists(filePath), $"File not found: {filePath}");

        var xmlContent = await File.ReadAllTextAsync(filePath);

        var serializer = new XmlSerializer(typeof(EasyModeManager));
        using var stringReader = new StringReader(xmlContent);
        using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });

        var manager = (EasyModeManager?)serializer.Deserialize(xmlReader);
        Assert.NotNull(manager);
        Assert.NotNull(manager.Systems);
        Assert.NotEmpty(manager.Systems);

        var urlProperties = new[]
        {
            nameof(EmulatorConfig.EmulatorDownloadPage),
            nameof(EmulatorConfig.EmulatorDownloadLink),
            nameof(EmulatorConfig.CoreDownloadLink),
            nameof(EmulatorConfig.ImagePackDownloadLink),
            nameof(EmulatorConfig.ImagePackDownloadLink2),
            nameof(EmulatorConfig.ImagePackDownloadLink3),
            nameof(EmulatorConfig.ImagePackDownloadLink4),
            nameof(EmulatorConfig.ImagePackDownloadLink5)
        };

        var urls = new List<string>();
        foreach (var system in manager.Systems)
        {
            var emulator = system.Emulators?.Emulator;
            if (emulator == null) continue;

            foreach (var propName in urlProperties)
            {
                var prop = typeof(EmulatorConfig).GetProperty(propName);
                var value = prop?.GetValue(emulator) as string;
                if (!string.IsNullOrWhiteSpace(value))
                    urls.Add(value);
            }
        }

        urls = urls.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Assert.NotEmpty(urls);

        var brokenUrls = new List<string>();
        var tasks = urls.Select(async url =>
        {
            await Semaphore.WaitAsync();
            try
            {
                var error = await CheckUrlAsync(url);
                if (error != null)
                {
                    lock (brokenUrls)
                    {
                        brokenUrls.Add($"{xmlFileName} -> {error}");
                    }
                }
            }
            finally
            {
                Semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        if (brokenUrls.Count != 0)
        {
            var message = string.Join(Environment.NewLine, brokenUrls);
            Assert.Fail(
                $"The following URLs inside the local EasyMode XML sample are broken:{Environment.NewLine}{message}");
        }
    }

    /// <summary>
    ///     Verifies that EasyMode fallback XML files are reachable and contain valid, reachable download URLs.
    /// </summary>
    [Theory]
    [InlineData("https://assets.purelogiccode.com/Simple%20Launcher/Simple%20Launcher/easymode.xml")]
    [InlineData("https://assets.purelogiccode.com/Simple%20Launcher/Simple%20Launcher/easymode_arm64.xml")]
    public async Task EasyModeFallbackXmlIsReachableAndContainsValidUrls(string xmlUrl)
    {
        using var response = await HttpClient.GetAsync(xmlUrl);
        Assert.True(response.IsSuccessStatusCode, $"Fallback XML {xmlUrl} returned {(int)response.StatusCode}");

        var xmlContent = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(xmlContent), $"Fallback XML {xmlUrl} returned empty content");

        var serializer = new XmlSerializer(typeof(EasyModeManager));
        using var stringReader = new StringReader(xmlContent);
        using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });

        var manager = (EasyModeManager?)serializer.Deserialize(xmlReader);
        Assert.NotNull(manager);
        Assert.NotNull(manager.Systems);
        Assert.NotEmpty(manager.Systems);

        var urlProperties = new[]
        {
            nameof(EmulatorConfig.EmulatorDownloadPage),
            nameof(EmulatorConfig.EmulatorDownloadLink),
            nameof(EmulatorConfig.CoreDownloadLink),
            nameof(EmulatorConfig.ImagePackDownloadLink),
            nameof(EmulatorConfig.ImagePackDownloadLink2),
            nameof(EmulatorConfig.ImagePackDownloadLink3),
            nameof(EmulatorConfig.ImagePackDownloadLink4),
            nameof(EmulatorConfig.ImagePackDownloadLink5)
        };

        var urls = new List<string>();
        foreach (var system in manager.Systems)
        {
            var emulator = system.Emulators?.Emulator;
            if (emulator == null) continue;

            foreach (var propName in urlProperties)
            {
                var prop = typeof(EmulatorConfig).GetProperty(propName);
                var value = prop?.GetValue(emulator) as string;
                if (!string.IsNullOrWhiteSpace(value))
                    urls.Add(value);
            }
        }

        urls = urls.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Assert.NotEmpty(urls);

        var brokenUrls = new List<string>();
        var tasks = urls.Select(async url =>
        {
            await Semaphore.WaitAsync();
            try
            {
                var error = await CheckUrlAsync(url);
                if (error != null)
                {
                    lock (brokenUrls)
                    {
                        brokenUrls.Add($"{xmlUrl} -> {error}");
                    }
                }
            }
            finally
            {
                Semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        if (brokenUrls.Count != 0)
        {
            var message = string.Join(Environment.NewLine, brokenUrls);
            Assert.Fail($"The following URLs inside the EasyMode XML are broken:{Environment.NewLine}{message}");
        }
    }

    private static async Task<string?> CheckUrlAsync(string url)
    {
        const int maxRetries = 2;
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                using var headResponse = await HttpClient.SendAsync(headRequest);

                if (headResponse.IsSuccessStatusCode)
                    return null;

                // Some servers block HEAD or return non-success codes for it.
                // Fall back to GET (headers only) for a more accurate check.
                using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
                using var getResponse =
                    await HttpClient.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead);
                if (getResponse.IsSuccessStatusCode)
                    return null;

                // Many servers block automated requests with 403 but work fine in browsers.
                // Treat 403 as acceptable for URL validation.
                if ((int)getResponse.StatusCode == 403)
                    return null;

                return $"{url} -> HTTP {(int)getResponse.StatusCode}";
            }
            catch (TaskCanceledException)
            {
                if (attempt < maxRetries)
                {
                    await Task.Delay(2000);
                    continue;
                }

                return $"{url} -> Timeout";
            }
            catch (Exception ex)
            {
                return $"{url} -> {ex.GetType().Name}: {ex.Message}";
            }
        }

        return null;
    }

    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing group is needed to extract the URL")]
    [GeneratedRegex(@"\((https?://[^)\s]+)\)", RegexOptions.IgnoreCase, 1000, "pt-BR")]
    private static partial Regex MyRegex();
}