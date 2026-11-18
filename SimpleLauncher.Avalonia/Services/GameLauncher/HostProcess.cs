using System.Diagnostics;
using System.Text;
using SimpleLauncher.Core.Services;

namespace SimpleLauncher.Avalonia.Services.GameLauncher;

/// <summary>
///     Creates <see cref="ProcessStartInfo" /> instances for programs that live outside an
///     application sandbox. Emulators are installed by the user on the host; a Flatpak build
///     must launch them through <c>flatpak-spawn --host</c> so they see the host libraries,
///     devices and display instead of the isolated runtime.
/// </summary>
internal static class HostProcess
{
    /// <summary>
    ///     Builds the start info for an emulator/tool launch, routing through the host when
    ///     the application runs inside a Flatpak sandbox. Outside Flatpak this is exactly the
    ///     direct launch the app has always used.
    /// </summary>
    public static ProcessStartInfo Create(
        string fileName,
        string arguments,
        string workingDirectory,
        bool useShellExecute = false,
        bool redirectOutput = false)
    {
        if (!PackagingEnvironment.IsFlatpak || useShellExecute)
        {
            return new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = useShellExecute,
                CreateNoWindow = true,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = redirectOutput,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "flatpak-spawn",
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
            CreateNoWindow = true,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.ArgumentList.Add("--host");
        if (!string.IsNullOrWhiteSpace(workingDirectory))
            startInfo.ArgumentList.Add($"--directory={workingDirectory}");

        // Pass the emulator and its arguments as real argv entries. Routing through a shell
        // would expand $/backticks embedded in ROM paths (and make quotes in filenames an
        // injection vector); flatpak-spawn forwards argv unchanged to the host process.
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(fileName);
        foreach (var argument in CommandLineSplitter.Split(arguments))
            startInfo.ArgumentList.Add(argument);

        return startInfo;
    }
}
