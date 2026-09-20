using System;
using System.IO;
using System.Threading.Tasks;

namespace XmlToBinaryConverter.Services;

/// <summary>
///     Provides error logging functionality to a file.
/// </summary>
public static class LogError
{
    private const string LogFileName = "error_log.txt";

    private static string LogFilePath => Path.Combine(AppContext.BaseDirectory, LogFileName);

    /// <summary>
    ///     Logs an exception to the error log file asynchronously.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    public static async Task LogAsync(Exception ex)
    {
        Log.Error(ex, "An error occurred");

        var errorMessage = $"[{DateTime.Now}] Error: {ex.Message}\nStackTrace: {ex.StackTrace}\n\n";

        try
        {
            await File.AppendAllTextAsync(LogFilePath, errorMessage);
        }
        catch (Exception)
        {
            // If the first attempt fails (e.g., directory doesn't exist), try creating the directory
            try
            {
                var logDirectory = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
                    Directory.CreateDirectory(logDirectory);

                // Try writing again
                await File.AppendAllTextAsync(LogFilePath, errorMessage);
            }
            catch (Exception innerEx)
            {
                Log.Fatal(innerEx, "Could not write to log file at {LogFilePath}", LogFilePath);
            }
        }
    }

    /// <summary>
    ///     Reads the error log file content asynchronously.
    /// </summary>
    /// <returns>The log file content, or a message indicating no log was found.</returns>
    public static async Task<string> ReadLogAsync()
    {
        if (File.Exists(LogFilePath))
        {
            try
            {
                return await File.ReadAllTextAsync(LogFilePath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error reading log file");
                return $"Error reading log file: {ex.Message}";
            }
        }

        return "No error log found.";
    }

    /// <summary>
    ///     Clears the error log file.
    /// </summary>
    public static void ClearLog()
    {
        if (File.Exists(LogFilePath))
        {
            try
            {
                File.Delete(LogFilePath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error clearing log file");
            }
        }
    }
}