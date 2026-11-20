using System.Runtime.InteropServices;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Core.Services.CleanAndDeleteFiles;

/// <summary>
///     Provides cleanup routines that delete temporary and architecture-specific files from the SimpleLauncher
///     installation.
/// </summary>
public class CleanSimpleLauncherFolderService : ICleanSimpleLauncherFolderService
{
    private static readonly string AppDirectory = AppDomain.CurrentDomain.BaseDirectory;

    private static readonly string[] DirectoriesToClean =
    [
        Path.Combine(AppDirectory, "temp"),
        Path.Combine(AppDirectory, "temp2"),
        Path.Combine(AppDirectory, "tools", "BatchVerifyCHDFiles"),
        Path.Combine(AppDirectory, "tools", "BatchVerifyCompressedFiles"),
        Path.Combine(AppDirectory, "cache"),
        Path.Combine(AppDirectory, "x64"),
        Path.Combine(AppDirectory, "x86"),
        // Obsolete tool folders from earlier releases.
        Path.Combine(AppDirectory, "tools", "BatchConvertIsoToXiso"),
        Path.Combine(AppDirectory, "tools", "BatchConvertToCHD"),
        Path.Combine(AppDirectory, "tools", "BatchConvertToRVZ"),
        Path.Combine(AppDirectory, "tools", "PSXPackager"),
        Path.Combine(AppDirectory, "tools", "RAHasher"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForSegaModel3Games"),
        Path.Combine(AppDirectory, "tools", "xbox-iso-vfs"),

        // Optional GUI apps that are no longer bundled; remove their payloads from upgraded
        // installs. Simple Launcher now ships only the helpers it needs at runtime.
        Path.Combine(AppDirectory, "tools", "XISOStudio"),
        Path.Combine(AppDirectory, "tools", "CHDStudio"),
        Path.Combine(AppDirectory, "tools", "RVZStudio"),
        Path.Combine(AppDirectory, "tools", "BatchConvertToCompressedFile"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForPS3Games"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForScummVMGames"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForWindowsGames"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForXbox360XBLAGames"),
        Path.Combine(AppDirectory, "tools", "FindRomCover"),
        Path.Combine(AppDirectory, "tools", "RetroGameCoverDownloader"),
        Path.Combine(AppDirectory, "tools", "RomValidator"),

        Path.Combine(AppDirectory, "tools", "SimpleXisoDrive", "logs"),

        Path.Combine(AppDirectory, "resources"),
        Path.Combine(AppDirectory, "de"),
        Path.Combine(Path.GetTempPath(), "SimpleLauncher"),
        Path.Combine(Path.GetTempPath(), "SimpleZipDrive"),
        Path.Combine(Path.GetTempPath(), "SimpleXisoDrive")
    ];

    private static readonly string[] FilesToClean =
    [
        Path.Combine(AppDirectory, "update.zip"),
        Path.Combine(AppDirectory, "mame.xml"),
        Path.Combine(AppDirectory, "Updater.deps.json"),
        Path.Combine(AppDirectory, "Updater.dll"),
        Path.Combine(AppDirectory, "Updater.pdb"),
        Path.Combine(AppDirectory, "Updater.runtimeconfig.json"),
        Path.Combine(AppDirectory, "SimpleLauncher.pdb"),
        Path.Combine(AppDirectory, "7z.dll"),
        Path.Combine(AppDirectory, "7z_x64.dll"),
        Path.Combine(AppDirectory, "7z_arm64.dll"),
        Path.Combine(AppDirectory, "7z_x86.dll"),
        Path.Combine(AppDirectory, "7z.exe"),
        Path.Combine(AppDirectory, "7z_x86.exe"),
        Path.Combine(AppDirectory, "whatsnew.txt"),
        Path.Combine(AppDirectory, "easymode.xml"),
        Path.Combine(AppDirectory, "easymode_arm64.xml"),
        Path.Combine(AppDirectory, "error_user.log"),
        Path.Combine(AppDirectory, "helpuser.xml"),

        // Obsolete top-level CHDMounter files; the tool now ships per-OS/architecture payloads.
        Path.Combine(AppDirectory, "tools", "CHDMounter", "CHDMounter.exe"),
        Path.Combine(AppDirectory, "tools", "CHDMounter", "CHDMounter_arm64.exe"),
        Path.Combine(AppDirectory, "tools", "CHDMounter", "README.md"),

        Path.Combine(AppDirectory, "tools", "SimpleZipDrive", "7z.dll"),
        Path.Combine(AppDirectory, "tools", "SimpleZipDrive", "7z_arm64.dll"),
        Path.Combine(AppDirectory, "tools", "SimpleZipDrive", "LICENSE.txt"),
        Path.Combine(AppDirectory, "tools", "SimpleZipDrive", "ReadMe.md"),
        Path.Combine(AppDirectory, "tools", "SimpleZipDrive", "SimpleZipDrive.exe"),
        Path.Combine(AppDirectory, "tools", "SimpleZipDrive", "WhatsNew.md"),

        Path.Combine(AppDirectory, "tools", "RetroAchievementsSharp", "LICENSE"),
        Path.Combine(AppDirectory, "tools", "RetroAchievementsSharp", "README.md"),
        Path.Combine(AppDirectory, "tools", "RetroAchievementsSharp", "RetroAchievementsSharp"),
        Path.Combine(AppDirectory, "tools", "RetroAchievementsSharp", "RetroAchievementsSharp.exe"),
        Path.Combine(AppDirectory, "tools", "RetroAchievementsSharp", "RetroAchievementsSharp_arm64"),
        Path.Combine(AppDirectory, "tools", "RetroAchievementsSharp", "RetroAchievementsSharp_arm64.exe"),
        Path.Combine(AppDirectory, "tools", "RetroAchievementsSharp", "THIRD-PARTY-NOTICES.md"),

        Path.Combine(AppDirectory, "tools", "SimpleXisoDrive", "SimpleXisoDrive.exe")
    ];

    private static readonly string[] FilesToDeleteIfCurrentArchitectureIsX64 =
    [
        Path.Combine(AppDirectory, "7z_arm64.dll"),
        Path.Combine(AppDirectory, "easymode_arm64.xml"),
        Path.Combine(AppDirectory, "tools", "SimpleZipDrive", "SimpleZipDrive_arm64.exe"),
        Path.Combine(AppDirectory, "tools", "SimpleXisoDrive", "SimpleXisoDrive_arm64.exe")
    ];

    private static readonly string[] FilesToDeleteIfCurrentArchitectureIsArm64 =
    [
        Path.Combine(AppDirectory, "7z_x64.dll"),
        Path.Combine(AppDirectory, "easymode.xml"),
        Path.Combine(AppDirectory, "tools", "SimpleZipDrive", "SimpleZipDrive.exe"),
        Path.Combine(AppDirectory, "tools", "SimpleXisoDrive", "SimpleXisoDrive.exe")
    ];

    // The remaining helpers ship one payload per OS/architecture under tools\<tool>; the
    // release packages already prune the other architectures, so this only clears leftovers
    // from an updated install or a hand-copied tools folder.
    internal static readonly string[] DirectoriesToDeleteIfCurrentArchitectureIsX64 =
    [
        Path.Combine(AppDirectory, "tools", "CHDMounter", "win-arm64"),
        Path.Combine(AppDirectory, "tools", "CHDMounter", "linux-arm64"),
        Path.Combine(AppDirectory, "tools", "RetroAchievementsSharp", "win-arm64"),
        Path.Combine(AppDirectory, "tools", "RetroAchievementsSharp", "linux-arm64"),
        Path.Combine(AppDirectory, "tools", "SimpleXisoDrive", "win-arm64"),
        Path.Combine(AppDirectory, "tools", "SimpleXisoDrive", "linux-arm64"),
        Path.Combine(AppDirectory, "tools", "SimpleZipDrive", "win-arm64"),
        Path.Combine(AppDirectory, "tools", "SimpleZipDrive", "linux-arm64")
    ];

    // These helper folders must never be deleted as a whole on an ARM64 OS: each ships a
    // native ARM64 payload, so removing the whole folder would destroy the ARM64 helpers at
    // every startup. Only the other architecture's payload folders are removed.
    internal static readonly string[] DirectoriesToDeleteIfCurrentArchitectureIsArm64 =
    [
        Path.Combine(AppDirectory, "tools", "CHDMounter", "win-x64"),
        Path.Combine(AppDirectory, "tools", "CHDMounter", "linux-x64"),
        Path.Combine(AppDirectory, "tools", "RetroAchievementsSharp", "win-x64"),
        Path.Combine(AppDirectory, "tools", "RetroAchievementsSharp", "linux-x64"),
        Path.Combine(AppDirectory, "tools", "SimpleXisoDrive", "win-x64"),
        Path.Combine(AppDirectory, "tools", "SimpleXisoDrive", "linux-x64"),
        Path.Combine(AppDirectory, "tools", "SimpleZipDrive", "win-x64"),
        Path.Combine(AppDirectory, "tools", "SimpleZipDrive", "linux-x64")
    ];

    private readonly IDeleteFilesService _deleteFilesService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CleanSimpleLauncherFolderService" /> class.
    /// </summary>
    /// <param name="deleteFilesService">The service used to delete files and directories.</param>
    public CleanSimpleLauncherFolderService(IDeleteFilesService deleteFilesService)
    {
        _deleteFilesService = deleteFilesService;
    }

    /// <summary>
    ///     Deletes the temporary and obsolete files and directories from the SimpleLauncher installation folder.
    /// </summary>
    public void CleanupTrash()
    {
        // Clean directories
        foreach (var directory in DirectoriesToClean) _deleteFilesService.TryDeleteDirectory(directory);

        // Clean files
        foreach (var file in FilesToClean) DeleteFileSafely(file);

        // The Windows release renamed the Avalonia executable to SimpleLauncher.exe.
        // Remove the obsolete binary left by an upgrade (never on Linux/macOS, where the
        // running app is still named SimpleLauncher.Avalonia).
        if (OperatingSystem.IsWindows())
            DeleteFileSafely(Path.Combine(AppDirectory, "SimpleLauncher.Avalonia.exe"));

        var currentArchitecture = RuntimeInformation.OSArchitecture;
        CleanupArchitectureSpecificFiles(currentArchitecture);
        CleanupArchitectureSpecificFolders(currentArchitecture);
    }

    /// <summary>
    ///     Deletes the SimpleLauncher temporary folders located in the system temp directory.
    /// </summary>
    public void CleanupTempFiles()
    {
        _deleteFilesService.TryDeleteDirectory(Path.Combine(Path.GetTempPath(), "SimpleLauncher"));
        _deleteFilesService.TryDeleteDirectory(Path.Combine(Path.GetTempPath(), "SimpleZipDrive"));
        _deleteFilesService.TryDeleteDirectory(Path.Combine(Path.GetTempPath(), "SimpleXisoDrive"));
    }

    private void CleanupArchitectureSpecificFiles(Architecture currentArchitecture)
    {
        string[] filesToDelete;

        switch (currentArchitecture)
        {
            case Architecture.X64:
                filesToDelete = FilesToDeleteIfCurrentArchitectureIsX64;
                break;
            case Architecture.Arm64:
                filesToDelete = FilesToDeleteIfCurrentArchitectureIsArm64;
                break;
            default:
                // Unknown architectures (e.g. X86) have no arch-specific files to
                // clean; no-op instead of aborting startup cleanup (CORE-31).
                Log.Debug(
                    $"[CleanSimpleLauncherFolderService] No arch-specific files to clean for {currentArchitecture}.");
                return;
        }

        foreach (var file in filesToDelete) DeleteFileSafely(file);
    }

    private void CleanupArchitectureSpecificFolders(Architecture currentArchitecture)
    {
        string[] foldersToDelete;

        switch (currentArchitecture)
        {
            case Architecture.X64:
                foldersToDelete = DirectoriesToDeleteIfCurrentArchitectureIsX64;
                break;
            case Architecture.Arm64:
                foldersToDelete = DirectoriesToDeleteIfCurrentArchitectureIsArm64;
                break;
            default:
                // Unknown architectures (e.g. X86) have no arch-specific folders to
                // clean; no-op instead of aborting startup cleanup (CORE-31).
                Log.Debug(
                    $"[CleanSimpleLauncherFolderService] No arch-specific folders to clean for {currentArchitecture}.");
                return;
        }

        foreach (var folder in foldersToDelete) _deleteFilesService.TryDeleteDirectory(folder);
    }

    private void DeleteFileSafely(string path)
    {
        if (!File.Exists(path)) return;

        try
        {
            _deleteFilesService.TryDeleteFile(path);
        }
#pragma warning disable RCS1075
        catch (Exception)
#pragma warning restore RCS1075
        {
            // Ignore
        }
    }
}