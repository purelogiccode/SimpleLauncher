using SimpleLauncher.Avalonia.Services.AvaloniaServices;
using SimpleLauncher.Core.Services.CheckPaths;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     LB-05 regression tests: the emulator file pickers must not restrict the dialog to
///     Windows executables on Linux/macOS, where emulators are extensionless binaries or
///     AppImages. Windows keeps the *.exe/*.bat filters.
/// </summary>
public class ExecutableFileFilterTests
{
    [Fact]
    public void EmulatorFilter_OnWindows_OffersExecutableFilesOnly()
    {
        if (!OperatingSystem.IsWindows()) return;

        var types = AvaloniaFilePickerService.ParseFilter(ExecutableFileFilter.ForEmulator());

        var type = Assert.Single(types!);
        Assert.Equal("Executable Files (*.exe;*.bat)", type.Name);
        Assert.Contains("*.exe", type.Patterns!, StringComparer.Ordinal);
        Assert.Contains("*.bat", type.Patterns!, StringComparer.Ordinal);
    }

    [Fact]
    public void EmulatorFilter_OnUnix_HidesNothing()
    {
        if (OperatingSystem.IsWindows()) return;

        // "*.*" parses to null, which the picker passes to the storage provider as "no
        // filter": extensionless binaries (retroarch), AppImages and .sh wrappers all stay
        // selectable, matching CheckPath.IsValidEmulatorExecutablePath on Unix.
        Assert.Null(AvaloniaFilePickerService.ParseFilter(ExecutableFileFilter.ForEmulator()));
        Assert.Null(AvaloniaFilePickerService.ParseFilter(
            ExecutableFileFilter.ForNamedExecutable("Ares Executable", "ares.exe")));
    }

    [Fact]
    public void NamedExecutableFilter_OnWindows_OffersTheSpecificNameAndAllExecutables()
    {
        if (!OperatingSystem.IsWindows()) return;

        var types = AvaloniaFilePickerService.ParseFilter(
            ExecutableFileFilter.ForNamedExecutable("Ares Executable", "ares.exe"));

        Assert.NotNull(types);
        Assert.Equal(2, types.Count);
        Assert.Equal("Ares Executable", types[0].Name);
        Assert.Contains("ares.exe", types[0].Patterns!, StringComparer.Ordinal);
        Assert.Equal("All Executables", types[1].Name);
        Assert.Contains("*.exe", types[1].Patterns!, StringComparer.Ordinal);
    }

    [Fact]
    public void ParseFilter_KeepsSpecificFiltersAndTheAllFilesEntry()
    {
        // "All files|*.*" must not drop the whole filter list (LB-10) and must not itself be
        // dropped either: the Sound Configuration picker passes MP3 + All files, and users must
        // still be able to choose WAV/FLAC/Ogg files the playback layer supports.
        var types = AvaloniaFilePickerService.ParseFilter("MP3 files (*.mp3)|*.mp3|All files (*.*)|*.*");

        Assert.NotNull(types);
        Assert.Equal(2, types.Count);
        Assert.Equal("MP3 files (*.mp3)", types[0].Name);
        Assert.Contains("*.mp3", types[0].Patterns!, StringComparer.Ordinal);
        Assert.Equal("All files (*.*)", types[1].Name);
        Assert.Contains("*", types[1].Patterns!, StringComparer.Ordinal);
    }

    [Fact]
    public void ParseFilter_AllFilesEntryFirst_KeepsOrderAndSpecificFilters()
    {
        var types = AvaloniaFilePickerService.ParseFilter("All Files (*.*)|*.*|NeoGeo CD BIOS (neocd.bin)|neocd.bin");

        Assert.NotNull(types);
        Assert.Equal(2, types.Count);
        Assert.Equal("All Files (*.*)", types[0].Name);
        Assert.Contains("*", types[0].Patterns!, StringComparer.Ordinal);
        Assert.Equal("NeoGeo CD BIOS (neocd.bin)", types[1].Name);
        Assert.Contains("neocd.bin", types[1].Patterns!, StringComparer.Ordinal);
    }

    [Fact]
    public void ParseFilter_AllFilesOnly_ReturnsNoFilter()
    {
        Assert.Null(AvaloniaFilePickerService.ParseFilter("All files (*.*)|*.*"));
    }
}
