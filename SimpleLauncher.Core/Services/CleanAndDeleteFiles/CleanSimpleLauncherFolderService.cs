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
        Path.Combine(AppDirectory, "tools", "FindRomCover", "de"),
        Path.Combine(AppDirectory, "cache"),
        Path.Combine(AppDirectory, "x64"),
        Path.Combine(AppDirectory, "x86"),
        // Obsolete tool folder; replaced by tools\XISOStudio.
        Path.Combine(AppDirectory, "tools", "BatchConvertIsoToXiso"),
        // Obsolete tool folder; replaced by tools\CHDStudio.
        Path.Combine(AppDirectory, "tools", "BatchConvertToCHD"),
        // Obsolete tool folder; replaced by tools\RVZStudio.
        Path.Combine(AppDirectory, "tools", "BatchConvertToRVZ"),
        Path.Combine(AppDirectory, "tools", "PSXPackager"),
        Path.Combine(AppDirectory, "tools", "RAHasher"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForSegaModel3Games"),
        Path.Combine(AppDirectory, "tools", "xbox-iso-vfs"),
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

        Path.Combine(AppDirectory, "tools", "BatchConvertToCompressedFile", "7z.exe"),
        Path.Combine(AppDirectory, "tools", "BatchConvertToCompressedFile", "7z_x86.exe"),
        Path.Combine(AppDirectory, "tools", "BatchConvertToCompressedFile", "BatchConvertToCompressedFile.deps.json"),
        Path.Combine(AppDirectory, "tools", "BatchConvertToCompressedFile", "BatchConvertToCompressedFile.dll"),
        Path.Combine(AppDirectory, "tools", "BatchConvertToCompressedFile", "BatchConvertToCompressedFile.pdb"),
        Path.Combine(AppDirectory, "tools", "BatchConvertToCompressedFile",
            "BatchConvertToCompressedFile.runtimeconfig.json"),
        Path.Combine(AppDirectory, "tools", "BatchConvertToCompressedFile", "7z.dll"),

        // Obsolete top-level CHDMounter files; the tool now ships per-OS/architecture payloads.
        Path.Combine(AppDirectory, "tools", "CHDMounter", "CHDMounter.exe"),
        Path.Combine(AppDirectory, "tools", "CHDMounter", "CHDMounter_arm64.exe"),
        Path.Combine(AppDirectory, "tools", "CHDMounter", "README.md"),

        Path.Combine(AppDirectory, "tools", "FindRomCover", "ControlzEx.dll"),
        Path.Combine(AppDirectory, "tools", "FindRomCover", "error_user.log"),
        Path.Combine(AppDirectory, "tools", "FindRomCover", "FindRomCover.deps.json"),
        Path.Combine(AppDirectory, "tools", "FindRomCover", "FindRomCover.dll"),
        Path.Combine(AppDirectory, "tools", "FindRomCover", "FindRomCover.pdb"),
        Path.Combine(AppDirectory, "tools", "FindRomCover", "FindRomCover.runtimeconfig.json"),
        Path.Combine(AppDirectory, "tools", "FindRomCover", "MahApps.Metro.dll"),
        Path.Combine(AppDirectory, "tools", "FindRomCover", "Microsoft.Xaml.Behaviors.dll"),
        Path.Combine(AppDirectory, "tools", "FindRomCover", "Newtonsoft.Json.dll"),
        Path.Combine(AppDirectory, "tools", "FindRomCover", "appsettings.json"),

        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForPS3Games", "CreateBatchFilesForPS3Games.deps.json"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForPS3Games", "CreateBatchFilesForPS3Games.dll"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForPS3Games", "CreateBatchFilesForPS3Games.pdb"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForPS3Games",
            "CreateBatchFilesForPS3Games.runtimeconfig.json"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForPS3Games",
            "Microsoft.Extensions.DependencyInjection.Abstractions.dll"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForPS3Games",
            "Microsoft.Extensions.DependencyInjection.dll"),

        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForXbox360XBLAGames",
            "CreateBatchFilesForXbox360XBLAGames.deps.json"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForXbox360XBLAGames",
            "CreateBatchFilesForXbox360XBLAGames.dll"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForXbox360XBLAGames",
            "CreateBatchFilesForXbox360XBLAGames.pdb"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForXbox360XBLAGames",
            "CreateBatchFilesForXbox360XBLAGames.runtimeconfig.json"),

        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForWindowsGames",
            "CreateBatchFilesForWindowsGames.deps.json"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForWindowsGames", "CreateBatchFilesForWindowsGames.dll"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForWindowsGames", "CreateBatchFilesForWindowsGames.pdb"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForWindowsGames",
            "CreateBatchFilesForWindowsGames.runtimeconfig.json"),

        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForScummVMGames",
            "CreateBatchFilesForScummVMGames.deps.json"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForScummVMGames", "CreateBatchFilesForScummVMGames.dll"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForScummVMGames", "CreateBatchFilesForScummVMGames.pdb"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForScummVMGames",
            "CreateBatchFilesForScummVMGames.runtimeconfig.json")
    ];

    private static readonly string[] FilesToDeleteIfCurrentArchitectureIsX64 =
    [
        Path.Combine(AppDirectory, "7z_arm64.dll"),
        Path.Combine(AppDirectory, "easymode_arm64.xml"),
        Path.Combine(AppDirectory, "tools", "BatchConvertToCompressedFile", "7z_arm64.dll"),
        Path.Combine(AppDirectory, "tools", "BatchConvertToCompressedFile", "BatchConvertToCompressedFile_arm64.exe"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForPS3Games", "CreateBatchFilesForPS3Games_arm64.exe"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForScummVMGames",
            "CreateBatchFilesForScummVMGames_arm64.exe"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForWindowsGames",
            "CreateBatchFilesForWindowsGames_arm64.exe"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForXbox360XBLAGames",
            "CreateBatchFilesForXbox360XBLAGames_arm64.exe"),
        Path.Combine(AppDirectory, "tools", "FindRomCover", "FindRomCover_arm64.exe"),
        Path.Combine(AppDirectory, "tools", "RomValidator", "RomValidator_arm64.exe"),
        Path.Combine(AppDirectory, "tools", "SimpleZipDrive", "SimpleZipDrive_arm64.exe"),
        Path.Combine(AppDirectory, "tools", "RetroGameCoverDownloader", "RetroGameCoverDownloader_arm64.exe"),
        Path.Combine(AppDirectory, "tools", "FindRomCover", "Magick.Native-Q16-arm64.dll"),
        Path.Combine(AppDirectory, "tools", "RomValidator", "7z_arm64.dll"),
        Path.Combine(AppDirectory, "tools", "SimpleXisoDrive", "SimpleXisoDrive_arm64.exe")
    ];

    private static readonly string[] FilesToDeleteIfCurrentArchitectureIsArm64 =
    [
        Path.Combine(AppDirectory, "7z_x64.dll"),
        Path.Combine(AppDirectory, "easymode.xml"),
        Path.Combine(AppDirectory, "tools", "BatchConvertToCompressedFile", "7z_x64.dll"),
        Path.Combine(AppDirectory, "tools", "BatchConvertToCompressedFile", "BatchConvertToCompressedFile.exe"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForPS3Games", "CreateBatchFilesForPS3Games.exe"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForScummVMGames", "CreateBatchFilesForScummVMGames.exe"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForWindowsGames", "CreateBatchFilesForWindowsGames.exe"),
        Path.Combine(AppDirectory, "tools", "CreateBatchFilesForXbox360XBLAGames",
            "CreateBatchFilesForXbox360XBLAGames.exe"),
        Path.Combine(AppDirectory, "tools", "FindRomCover", "FindRomCover.exe"),
        Path.Combine(AppDirectory, "tools", "RomValidator", "RomValidator.exe"),
        Path.Combine(AppDirectory, "tools", "SimpleZipDrive", "SimpleZipDrive.exe"),
        Path.Combine(AppDirectory, "tools", "RetroGameCoverDownloader", "RetroGameCoverDownloader.exe"),
        Path.Combine(AppDirectory, "tools", "FindRomCover", "Magick.Native-Q16-x64.dll"),
        Path.Combine(AppDirectory, "tools", "RomValidator", "7z_x64.dll"),
        Path.Combine(AppDirectory, "tools", "SimpleXisoDrive", "SimpleXisoDrive.exe")
    ];

    // XISOStudio, CHDStudio, RVZStudio and CHDMounter ship one payload per OS/architecture
    // under tools\<tool>; the release packages already prune the other architectures, so this
    // only clears leftovers from an updated install or a hand-copied tools folder.
    internal static readonly string[] DirectoriesToDeleteIfCurrentArchitectureIsX64 =
    [
        Path.Combine(AppDirectory, "tools", "XISOStudio", "win-arm64"),
        Path.Combine(AppDirectory, "tools", "XISOStudio", "linux-arm64"),
        Path.Combine(AppDirectory, "tools", "CHDStudio", "win-arm64"),
        Path.Combine(AppDirectory, "tools", "CHDStudio", "linux-arm64"),
        Path.Combine(AppDirectory, "tools", "RVZStudio", "win-arm64"),
        Path.Combine(AppDirectory, "tools", "RVZStudio", "linux-arm64"),
        Path.Combine(AppDirectory, "tools", "CHDMounter", "win-arm64"),
        Path.Combine(AppDirectory, "tools", "CHDMounter", "linux-arm64")
    ];

    // The cross-platform tool folders must never be deleted as a whole on an ARM64 OS: each
    // ships a native ARM64 payload, so removing the whole folder destroyed the ARM64 tools at
    // every startup. Only the other architecture's payload folders are removed.
    internal static readonly string[] DirectoriesToDeleteIfCurrentArchitectureIsArm64 =
    [
        Path.Combine(AppDirectory, "tools", "xbox-iso-vfs"),
        Path.Combine(AppDirectory, "tools", "XISOStudio", "win-x64"),
        Path.Combine(AppDirectory, "tools", "XISOStudio", "linux-x64"),
        Path.Combine(AppDirectory, "tools", "CHDStudio", "win-x64"),
        Path.Combine(AppDirectory, "tools", "CHDStudio", "linux-x64"),
        Path.Combine(AppDirectory, "tools", "RVZStudio", "win-x64"),
        Path.Combine(AppDirectory, "tools", "RVZStudio", "linux-x64"),
        Path.Combine(AppDirectory, "tools", "CHDMounter", "win-x64"),
        Path.Combine(AppDirectory, "tools", "CHDMounter", "linux-x64")
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