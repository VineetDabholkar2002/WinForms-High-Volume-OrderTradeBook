using System;
using System.IO;
using System.Text;
using TradingDataVisualization.Infrastructure;
using TradingDataVisualization.Logs;

public class FileLogger : ILogger, IDisposable
{
    private readonly object _lock = new object();
    private readonly string _logDirectory;
    private StreamWriter _logWriter;
    private DateTime _currentLogDate;

    public FileLogger(string logDirectory = "Logs")
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
        InitializeWriter();
    }

    private void InitializeWriter()
    {
        _currentLogDate = DateTime.UtcNow.Date;
        var logPath = Path.Combine(_logDirectory, $"app_{_currentLogDate:yyyyMMdd}.log");
        _logWriter = new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
    }

    private void RollLogFileIfNeeded()
    {
        var today = DateTime.UtcNow.Date;
        if (today != _currentLogDate)
        {
            lock (_lock)
            {
                if (today != _currentLogDate)
                {
                    _logWriter?.Dispose();
                    InitializeWriter();
                }
            }
        }
    }

    private void Log(string level, string message)
    {
        RollLogFileIfNeeded();
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLine = $"[{timestamp}] [{level}] {message}";
        lock (_lock)
        {
            _logWriter.WriteLine(logLine);
        }
    }

    public void LogInformation(string message) => Log("INFO", message);

    public void LogError(string message, Exception ex = null)
    {
        Log("ERROR", message);
        if (ex != null)
        {
            Log("ERROR", $"Exception: {ex.Message}");
            Log("ERROR", ex.StackTrace ?? string.Empty);
        }
    }

    public void LogWarning(string message) => Log("WARN", message);

    public void LogDebug(string message) => Log("DEBUG", message);

    public void Dispose()
    {
        lock (_lock)
        {
            _logWriter?.Dispose();
            _logWriter = null;
        }
    }
}
