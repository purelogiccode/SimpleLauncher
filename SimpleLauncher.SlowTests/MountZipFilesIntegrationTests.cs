using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using SimpleLauncher.Core.Services.GameLauncher.MountFiles;
using Xunit;
using Xunit.Sdk;

namespace SimpleLauncher.SlowTests;

/// <summary>
///     Integration tests that mount real ZIP archives with SimpleZipDrive and verify the mount succeeds.
///     Tests are skipped at runtime when the ZIP file, the SimpleZipDrive tool, or the Dokan driver is unavailable.
/// </summary>
public sealed class MountZipFilesIntegrationTests
{
    private static readonly string SimpleZipDriveExePath = Path.Combine(
        AppContext.BaseDirectory, "tools", "SimpleZipDrive", GetSimpleZipDriveExecutableName());

    private static readonly TimeSpan MountTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    ///     Gets the test data rows for ZIP mount integration tests, specifying the file path and a descriptive game name.
    /// </summary>
    public static TheoryData<string, string> ZipFiles => new()
    {
        { @"G:\ScummVM\Castle, The (Windows).zip", "Castle, The (Windows)" },
        { @"G:\ScummVM\Accuse (IF).zip", "Accuse (IF)" },
        { @"G:\ScummVM\Bloody Life, A (IF).zip", "Bloody Life, A (IF)" }
    };

    /// <summary>
    ///     Verifies that a real ZIP archive can be mounted and unmounted cleanly with SimpleZipDrive,
    ///     checking that the mounted drive is accessible and contains at least one entry.
    /// </summary>
    /// <param name="zipFilePath">The full path to the ZIP file to mount.</param>
    /// <param name="gameName">The display name of the game for assertion messages.</param>
    [Theory]
    [MemberData(nameof(ZipFiles))]
    public async Task MountRealZipSucceeds_And_UnmountsCleanly(string zipFilePath, string gameName)
    {
        if (!File.Exists(zipFilePath)) throw SkipException.ForSkip($"ZIP file not found: {zipFilePath}");

#pragma warning disable CA1416
        if (!DokanValidation.IsDokanInstalled())
#pragma warning restore CA1416
            throw SkipException.ForSkip("Dokan driver is not installed. ZIP cannot be mounted.");

        if (!File.Exists(SimpleZipDriveExePath))
            throw SkipException.ForSkip($"SimpleZipDrive executable not found: {SimpleZipDriveExePath}");

        var driveLetter = GetAvailableDriveLetter();
        if (driveLetter == null) throw SkipException.ForSkip("No available drive letters found between D: and Z:.");

        var mountArg = driveLetter.Value.ToString().ToLowerInvariant();
        var driveRoot = $"{driveLetter.Value}:\\";

        var psi = new ProcessStartInfo
        {
            FileName = SimpleZipDriveExePath,
            Arguments = $"\"{zipFilePath}\" \"{mountArg}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(SimpleZipDriveExePath) ?? AppContext.BaseDirectory
        };

        using var mountProcess = new Process();
        mountProcess.StartInfo = psi;
        try
        {
            var started = mountProcess.Start();
            Assert.True(started, $"Failed to start SimpleZipDrive for '{gameName}'.");

            var mountSuccessful = false;
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < MountTimeout)
            {
                if (Directory.Exists(driveRoot))
                {
                    mountSuccessful = true;
                    break;
                }

                if (mountProcess.HasExited)
                {
                    var exitCode = mountProcess.ExitCode;
                    Assert.Fail($"SimpleZipDrive exited prematurely for '{gameName}' with code {exitCode}.");
                }

                await Task.Delay(PollInterval);
            }

            stopwatch.Stop();

            Assert.True(mountSuccessful,
                $"Drive {driveRoot} did not appear within {MountTimeout.TotalSeconds}s for '{gameName}'.");

            var entries = Directory.GetFileSystemEntries(driveRoot);
            Assert.NotEmpty(entries);

            // SimpleZipDrive stays alive while the drive is mounted, so its stdout/stderr streams never
            // reach EOF. Only drain them if the process has already exited; otherwise ReadToEndAsync would
            // block forever. The process is killed in the finally block below.
            if (mountProcess.HasExited)
            {
                _ = await mountProcess.StandardOutput.ReadToEndAsync();
                var stderr = await mountProcess.StandardError.ReadToEndAsync();
                Assert.True(
                    string.IsNullOrEmpty(stderr) || !stderr.Contains("error", StringComparison.OrdinalIgnoreCase),
                    $"SimpleZipDrive reported errors for '{gameName}': {stderr}");
            }
        }
        finally
        {
            if (!mountProcess.HasExited)
            {
                mountProcess.Kill(true);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                try
                {
                    await mountProcess.WaitForExitAsync(cts.Token);
                }
                catch (TaskCanceledException)
                {
                    // Process did not exit in time; continue cleanup.
                }
            }

            await WaitForDriveToDisappearAsync(driveRoot);
        }
    }

    /// <summary>
    ///     Verifies that mounting a ZIP and listing its contents returns a non-empty set of entries,
    ///     and that the entries have reasonable names (non-empty, no invalid path characters).
    /// </summary>
    /// <param name="zipFilePath">The full path to the ZIP file to mount.</param>
    /// <param name="gameName">The display name of the game for assertion messages.</param>
    [Theory]
    [MemberData(nameof(ZipFiles))]
    public async Task MountRealZip_ListsValidEntries(string zipFilePath, string gameName)
    {
        if (!File.Exists(zipFilePath)) throw SkipException.ForSkip($"ZIP file not found: {zipFilePath}");

#pragma warning disable CA1416
        if (!DokanValidation.IsDokanInstalled())
#pragma warning restore CA1416
            throw SkipException.ForSkip("Dokan driver is not installed. ZIP cannot be mounted.");

        if (!File.Exists(SimpleZipDriveExePath))
            throw SkipException.ForSkip($"SimpleZipDrive executable not found: {SimpleZipDriveExePath}");

        var driveLetter = GetAvailableDriveLetter();
        if (driveLetter == null) throw SkipException.ForSkip("No available drive letters found between D: and Z:.");

        var mountArg = driveLetter.Value.ToString().ToLowerInvariant();
        var driveRoot = $"{driveLetter.Value}:\\";

        var psi = new ProcessStartInfo
        {
            FileName = SimpleZipDriveExePath,
            Arguments = $"\"{zipFilePath}\" \"{mountArg}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(SimpleZipDriveExePath) ?? AppContext.BaseDirectory
        };

        using var mountProcess = new Process();
        mountProcess.StartInfo = psi;
        try
        {
            var started = mountProcess.Start();
            Assert.True(started, $"Failed to start SimpleZipDrive for '{gameName}'.");

            await WaitForDriveAsync(mountProcess, driveRoot, gameName);

            Assert.True(Directory.Exists(driveRoot),
                $"Mounted drive {driveRoot} is not accessible for '{gameName}'.");

            var allEntries = Directory.GetFileSystemEntries(driveRoot, "*", SearchOption.AllDirectories);
            Assert.NotEmpty(allEntries);

            foreach (var entry in allEntries)
            {
                var name = Path.GetFileName(entry);
                Assert.False(string.IsNullOrWhiteSpace(name),
                    $"Entry with empty/whitespace name found in '{gameName}': {entry}");

                foreach (var invalidChar in Path.GetInvalidFileNameChars())
                {
                    Assert.False(name.Contains(invalidChar),
                        $"Entry name contains invalid character 0x{(int)invalidChar:X2} in '{gameName}': {name}");
                }
            }
        }
        finally
        {
            if (!mountProcess.HasExited)
            {
                mountProcess.Kill(true);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                try
                {
                    await mountProcess.WaitForExitAsync(cts.Token);
                }
                catch (TaskCanceledException)
                {
                    // Process did not exit in time; continue cleanup.
                }
            }

            await WaitForDriveToDisappearAsync(driveRoot);
        }
    }

    /// <summary>
    ///     Verifies that mounting a ZIP to a drive letter, then killing the SimpleZipDrive process,
    ///     cleanly unmounts the drive within a reasonable time.
    /// </summary>
    /// <param name="zipFilePath">The full path to the ZIP file to mount.</param>
    /// <param name="gameName">The display name of the game for assertion messages.</param>
    [Theory]
    [MemberData(nameof(ZipFiles))]
    public async Task MountRealZip_UnmountCleansUpDrive(string zipFilePath, string gameName)
    {
        if (!File.Exists(zipFilePath)) throw SkipException.ForSkip($"ZIP file not found: {zipFilePath}");

#pragma warning disable CA1416
        if (!DokanValidation.IsDokanInstalled())
#pragma warning restore CA1416
            throw SkipException.ForSkip("Dokan driver is not installed. ZIP cannot be mounted.");

        if (!File.Exists(SimpleZipDriveExePath))
            throw SkipException.ForSkip($"SimpleZipDrive executable not found: {SimpleZipDriveExePath}");

        var driveLetter = GetAvailableDriveLetter();
        if (driveLetter == null) throw SkipException.ForSkip("No available drive letters found between D: and Z:.");

        var mountArg = driveLetter.Value.ToString().ToLowerInvariant();
        var driveRoot = $"{driveLetter.Value}:\\";

        var psi = new ProcessStartInfo
        {
            FileName = SimpleZipDriveExePath,
            Arguments = $"\"{zipFilePath}\" \"{mountArg}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(SimpleZipDriveExePath) ?? AppContext.BaseDirectory
        };

        using var mountProcess = new Process();
        mountProcess.StartInfo = psi;
        var started = mountProcess.Start();
        Assert.True(started, $"Failed to start SimpleZipDrive for '{gameName}'.");

        await WaitForDriveAsync(mountProcess, driveRoot, gameName);

        Assert.True(Directory.Exists(driveRoot),
            $"Mounted drive {driveRoot} is not accessible for '{gameName}'.");

        // Unmount by killing the process
        mountProcess.Kill(true);
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
        {
            try
            {
                await mountProcess.WaitForExitAsync(cts.Token);
            }
            catch (TaskCanceledException)
            {
                // Process did not exit in time.
            }
        }

        await WaitForDriveToDisappearAsync(driveRoot);

        Assert.False(Directory.Exists(driveRoot),
            $"Drive {driveRoot} still exists after unmount for '{gameName}'.");
    }

    private static async Task WaitForDriveAsync(Process mountProcess, string driveRoot, string gameName)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < MountTimeout)
        {
            if (Directory.Exists(driveRoot)) return;

            if (mountProcess.HasExited)
            {
                var exitCode = mountProcess.ExitCode;
                Assert.Fail($"SimpleZipDrive exited prematurely for '{gameName}' with code {exitCode}.");
            }

            await Task.Delay(PollInterval);
        }
    }

    private static async Task WaitForDriveToDisappearAsync(string? driveRoot)
    {
        if (string.IsNullOrEmpty(driveRoot)) return;

        const int maxRetries = 20;
        for (var i = 0; i < maxRetries; i++)
        {
            if (!Directory.Exists(driveRoot)) return;

            await Task.Delay(500);
        }

        Assert.False(Directory.Exists(driveRoot), $"Drive {driveRoot} still exists after unmount.");
    }

    private static char? GetAvailableDriveLetter()
    {
        var existingDrives = Environment.GetLogicalDrives()
            .Select(static d => char.ToUpper(d[0], CultureInfo.InvariantCulture))
            .ToHashSet();

        // Search from Z: down to D:
        for (var letter = 'Z'; letter >= 'D'; letter--)
        {
            if (!existingDrives.Contains(letter))
                return letter;
        }

        return null;
    }

    private static string GetSimpleZipDriveExecutableName()
    {
        return RuntimeInformation.ProcessArchitecture ==
               Architecture.Arm64
            ? "SimpleZipDrive_arm64.exe"
            : "SimpleZipDrive.exe";
    }
}