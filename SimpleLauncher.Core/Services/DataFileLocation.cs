using Microsoft.Extensions.Configuration;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Core.Services;

/// <summary>
///     Resolves the storage location of an application data file, choosing between a portable path next to the executable
///     and the local app data folder.
/// </summary>
public sealed class DataFileLocation
{
    private readonly string _fileName;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DataFileLocation" /> class using the given file name.
    /// </summary>
    /// <param name="fileName">The name of the data file.</param>
    public DataFileLocation(string fileName)
    {
        _fileName = fileName;
        var portablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _fileName);
        Initialize(portablePath);
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DataFileLocation" /> class using the configured data file path.
    /// </summary>
    /// <param name="configuration">The application configuration containing the data file path setting.</param>
    /// <param name="configKey">The configuration key that holds the data file path.</param>
    /// <param name="defaultFileName">The default file name used when no path is configured.</param>
    public DataFileLocation(IConfiguration configuration, string configKey, string defaultFileName)
    {
        var configuredPath = configuration.GetValue<string>(configKey) ?? defaultFileName;

        // The configured value can contain a folder (e.g. %BASEFOLDER%\system.xml) and callers
        // may pass it as the default name. The LocalAppData fallback must use a bare file name,
        // otherwise Path.Combine creates a literal '%BASEFOLDER%' folder under LocalAppData.
        var sanitizedFileName = Path.GetFileName(configuredPath);
        if (string.IsNullOrEmpty(sanitizedFileName)) sanitizedFileName = Path.GetFileName(defaultFileName);
        _fileName = string.IsNullOrEmpty(sanitizedFileName) ? defaultFileName : sanitizedFileName;

        var portablePath = PathHelper.ResolveRelativeToAppDirectory(configuredPath) ??
                           Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _fileName);
        Initialize(portablePath);
    }

    /// <summary>
    ///     Gets the resolved full path of the data file.
    /// </summary>
    public string FilePath { get; private set; } = null!;

    /// <summary>
    ///     Gets the temporary file path used while writing the data file.
    /// </summary>
    public string TempFilePath => FilePath + ".tmp";

    /// <summary>
    ///     Gets a value indicating whether the data file is stored in portable mode next to the executable.
    /// </summary>
    public bool IsPortableMode { get; private set; }

    private void Initialize(string portablePath)
    {
        var appDataFolder = AppDataPaths.SimpleLauncherDataFolder;
        var localAppDataPath = Path.Combine(appDataFolder, _fileName);
        var portableExists = File.Exists(portablePath);
        var localExists = File.Exists(localAppDataPath);

        switch (portableExists)
        {
            case true when !localExists:
                FilePath = portablePath;
                IsPortableMode = true;
                break;
            case false when localExists:
                FilePath = localAppDataPath;
                IsPortableMode = false;
                break;
            case true when localExists:
            {
                var portableInfo = new FileInfo(portablePath);
                var localInfo = new FileInfo(localAppDataPath);
                if (portableInfo.LastWriteTimeUtc > localInfo.LastWriteTimeUtc)
                {
                    FilePath = portablePath;
                    IsPortableMode = true;
                }
                else
                {
                    FilePath = localAppDataPath;
                    IsPortableMode = false;
                }

                break;
            }
            default:
            {
                if (IsDirectoryWritable(AppDomain.CurrentDomain.BaseDirectory))
                {
                    FilePath = portablePath;
                    IsPortableMode = true;
                }
                else
                {
                    EnsureDirectoryExists(appDataFolder);
                    FilePath = localAppDataPath;
                    IsPortableMode = false;
                }

                break;
            }
        }
    }

    /// <summary>
    ///     Gets the path of the data file inside the local app data folder for SimpleLauncher.
    /// </summary>
    /// <returns>The full path of the data file in the local app data folder.</returns>
    public string GetLocalAppDataPath()
    {
        var appDataFolder = AppDataPaths.SimpleLauncherDataFolder;
        return Path.Combine(appDataFolder, _fileName);
    }

    /// <summary>
    ///     Switches the data file location to the local app data folder, falling back when the current location is not usable.
    /// </summary>
    /// <returns>True if the fallback to local app data succeeded, false otherwise.</returns>
    public bool TryFallbackToLocalAppData()
    {
        try
        {
            var appDataFolder = AppDataPaths.SimpleLauncherDataFolder;
            var newFilePath = Path.Combine(appDataFolder, _fileName);

            EnsureDirectoryExists(appDataFolder);

            FilePath = newFilePath;
            IsPortableMode = false;
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug($"[DataFileLocation] TryFallbackToLocalAppData failed: {ex.Message}");
            return false;
        }
    }

    private static bool IsDirectoryWritable(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
                return false;

            var testFilePath = Path.Combine(directoryPath, $".write_test_{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFilePath, "test");
            File.Delete(testFilePath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug($"[DataFileLocation] IsDirectoryWritable failed for '{directoryPath}': {ex.Message}");
            return false;
        }
    }

    private static void EnsureDirectoryExists(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
        catch (Exception ex)
        {
            Log.Debug($"[DataFileLocation] EnsureDirectoryExists failed for '{path}': {ex.Message}");
        }
    }
}