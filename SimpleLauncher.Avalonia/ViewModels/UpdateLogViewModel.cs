using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the UpdateLogWindow.
/// </summary>
public class UpdateLogViewModel : ObservableObject
{
    private readonly StringBuilder _logBuilder = new();

    /// <summary>
    ///     Gets the log text content.
    /// </summary>
    public string LogText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "";

    /// <summary>
    ///     Appends a message to the log.
    /// </summary>
    public void AppendLog(string message)
    {
        _logBuilder.Append(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        _logBuilder.Append(" - ");
        _logBuilder.AppendLine(message);
        LogText = _logBuilder.ToString();
    }
}