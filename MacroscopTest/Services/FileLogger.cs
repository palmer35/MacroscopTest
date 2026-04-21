using System.IO;
using System.Text;

namespace MacroscopTest.Services;

/// <summary>
/// Writes simple text logs to daily files in the application folder.
/// </summary>
public sealed class FileLogger
{
    private const string LogDirectoryName = "Logs";
    private const string InfoLevel = "INFO";
    private const string ErrorLevel = "ERROR";

    private static readonly object SyncRoot = new();

    private readonly string _logDirectoryPath;

    public FileLogger()
    {
        _logDirectoryPath = Path.Combine(AppContext.BaseDirectory, LogDirectoryName);
        Directory.CreateDirectory(_logDirectoryPath);
    }

    public void LogInfo(string? message)
    {
        WriteLine(InfoLevel, message ?? string.Empty);
    }

    public void LogError(string? message, Exception? exception = null)
    {
        message ??= string.Empty;

        var fullMessage = exception is null
            ? message
            : $"{message} | {FormatException(exception)}";

        WriteLine(ErrorLevel, fullMessage);
    }

    private static string FormatException(Exception exception)
    {
        return exception.ToString()
            .Replace("\r", " ")
            .Replace("\n", " ");
    }

    private void WriteLine(string level, string message)
    {
        var now = DateTime.Now;
        var logFilePath = Path.Combine(_logDirectoryPath, $"{now:yyyy-MM-dd}.log");
        var line = $"[{now:HH:mm:ss}] [{level}] {message}{Environment.NewLine}";

        lock (SyncRoot)
        {
            File.AppendAllText(logFilePath, line, Encoding.UTF8);
        }
    }
}
