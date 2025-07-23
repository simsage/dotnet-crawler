namespace Crawlers;

using System;
using System.IO;

/// <summary>
/// Defines the different levels of log messages.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// For detailed debugging information.
    /// </summary>
    Debug,
    /// <summary>
    /// For general information about the application's flow.
    /// </summary>
    Info,
    /// <summary>
    /// For warnings about potentially problematic situations.
    /// </summary>
    Warning,
    /// <summary>
    /// For errors that prevent the application from functioning correctly.
    /// </summary>
    Error,
    /// <summary>
    /// For critical errors that cause the application to crash or stop.
    /// </summary>
    Critical
}

/// <summary>
/// A simple logger class that writes messages to the console and a file.
/// </summary>
public class RockLogger
{
    private readonly string _logFilePath;
    private readonly string _objectType;
    private static IExternalSourceLogger? _externalSourceLogger;
    private readonly object _lock = new(); // Used for thread-safe file writing

    /// <summary>
    /// Creates and returns an instance of the RockLogger class for the specified type.
    /// </summary>
    /// <param name="type">The Type for which the logger is being created. This is typically the class type using the logger.</param>
    /// <returns>An instance of the RockLogger class associated with the specified type.</returns>
    public static RockLogger GetLogger(Type type)
    {
        return new RockLogger("logs/crawler-log.txt", type);
    }

    public static void SetUpExternalSourceLogger(IExternalSourceLogger? externalSourceLogger)
    {
        _externalSourceLogger = externalSourceLogger;
    }

    /// <summary>
    /// Initializes a new instance of the SimpleLogger class.
    /// </summary>
    /// <param name="logFilePath">The full path to the log file (e.g., "C:\\Logs\\app.log").</param>
    /// <param name="type"></param>
    private RockLogger(string logFilePath, Type type)
    {
        _logFilePath = logFilePath;
        _objectType = type.ToString();
        // Ensure the directory for the log file exists
        var logDirectory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
        {
            try
            {
                Directory.CreateDirectory(logDirectory);
            }
            catch
            {
                Console.WriteLine($"Error creating log directory '{logDirectory}'");
            }
        }
    }

    /// <summary>
    /// Writes a log message with a specified log level.
    /// </summary>
    /// <param name="level">The severity level of the log message.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">An optional exception to include in the log.</param>
    private void Log(LogLevel level, string message, Exception? exception = null)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] {_objectType} [{level.ToString().ToUpper()}] {message}";

        if (exception != null)
        {
            logEntry += Environment.NewLine + $"Exception: {exception.GetType().Name} - {exception.Message}";
            logEntry += Environment.NewLine + $"StackTrace: {exception.StackTrace}";
            if (exception.InnerException != null)
            {
                logEntry += Environment.NewLine + $"Inner Exception: {exception.InnerException.GetType().Name} - {exception.InnerException.Message}";
                logEntry += Environment.NewLine + $"Inner StackTrace: {exception.InnerException.StackTrace}";
            }
        }

        // Write to console
        WriteToConsole(level, logEntry);

        // Write to file (thread-safe)
        WriteToFile(logEntry);
        
        // Write to SimSage
        ExternalCrawlerOut(logEntry);
    }

    /// <summary>
    /// Writes a debug message.
    /// </summary>
    public void Debug(string message, Exception? exception = null) => Log(LogLevel.Debug, message, exception);

    /// <summary>
    /// Writes an informational message.
    /// </summary>
    public void Info(string message, Exception? exception = null) => Log(LogLevel.Info, message, exception);

    /// <summary>
    /// Writes a warning message.
    /// </summary>
    public void Warn(string message, Exception? exception = null) => Log(LogLevel.Warning, message, exception);

    /// <summary>
    /// Writes an error message.
    /// </summary>
    public void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);

    /// <summary>
    /// Writes a critical error message.
    /// </summary>
    public void Critical(string message, Exception? exception = null) => Log(LogLevel.Critical, message, exception);

    /// <summary>
    /// Writes the log entry to the console with appropriate coloring.
    /// </summary>
    /// <param name="level">The log level.</param>
    /// <param name="logEntry">The formatted log entry string.</param>
    private void WriteToConsole(LogLevel level, string logEntry)
    {
        ConsoleColor originalColor = Console.ForegroundColor;
        switch (level)
        {
            case LogLevel.Debug:
                Console.ForegroundColor = ConsoleColor.Gray;
                break;
            case LogLevel.Info:
                Console.ForegroundColor = ConsoleColor.White;
                break;
            case LogLevel.Warning:
                Console.ForegroundColor = ConsoleColor.Yellow;
                break;
            case LogLevel.Error:
                Console.ForegroundColor = ConsoleColor.Red;
                break;
            case LogLevel.Critical:
                Console.ForegroundColor = ConsoleColor.DarkRed;
                break;
            default:
                Console.ForegroundColor = ConsoleColor.White;
                break;
        }
        Console.WriteLine(logEntry);
        Console.ForegroundColor = originalColor; // Restore original color
    }

    /// <summary>
    /// Writes the log entry to the specified log file in a thread-safe manner.
    /// </summary>
    /// <param name="logEntry">The formatted log entry string.</param>
    private void WriteToFile(string logEntry)
    {
        lock (_lock) // Ensure only one thread writes to the file at a time
        {
            try
            {
                // Append the log entry to the file, creating it if it doesn't exist
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // If writing to file fails, log to console as a fallback
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error writing to log file '{_logFilePath}': {ex.Message}");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }
    }
    
    
    /**
     * output a log message for external crawlers if enabled
     */
    private static void ExternalCrawlerOut(string message) {
        _externalSourceLogger?.TransmitLogEntryToPlatform(message);
    }
    
}
