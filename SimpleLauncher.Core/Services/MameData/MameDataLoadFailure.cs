namespace SimpleLauncher.Core.Services.MameData;

/// <summary>
///     Describes how loading the MAME machine data file (mame.dat) ended.
/// </summary>
public enum MameDataLoadFailure
{
    /// <summary>The file loaded successfully.</summary>
    None,

    /// <summary>The file does not exist next to the application.</summary>
    MissingFile,

    /// <summary>The file exists but could not be deserialized.</summary>
    CorruptedFile
}
