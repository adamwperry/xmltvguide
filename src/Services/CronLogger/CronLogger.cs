using System.Text.Json;
using IOFile = System.IO.File;

namespace xmlTVGuide.Services.CronLogger;

public class CronLogger : ICronLogger
{
    private readonly string _logFilePath;
    private readonly int _maxLogEntries = 100;
    private readonly object _lock = new object();

    public CronLogger()
    {
        // Use environment variable or default path
        var logDir = Environment.GetEnvironmentVariable("LOG_PATH");
        if (string.IsNullOrEmpty(logDir))
        {
            // When running locally (development mode)
            logDir = Path.Combine(Environment.CurrentDirectory, "logs");
        }

        Directory.CreateDirectory(logDir);
        _logFilePath = Path.Combine(logDir, "cron.log.json");
    }

    public void LogCronRun(string message, DateTime timestamp, bool success, string? errorMessage = null)
    {
        lock (_lock)
        {
            var logs = LoadLogs();

            var newEntry = new CronLogEntry
            {
                Timestamp = timestamp,
                Message = message,
                Success = success,
                ErrorMessage = errorMessage
            };

            logs.Insert(0, newEntry); // Add to beginning

            // Keep only the last N entries
            if (logs.Count > _maxLogEntries)
                logs = logs.Take(_maxLogEntries).ToList();

            SaveLogs(logs);
        }
    }

    public List<CronLogEntry> GetLastLogs(int count = 100)
    {
        lock (_lock)
        {
            var logs = LoadLogs();
            return logs.Take(Math.Min(count, logs.Count)).ToList();
        }
    }

    public void ClearLogs()
    {
        lock (_lock)
        {
            SaveLogs(new List<CronLogEntry>());
        }
    }

    private List<CronLogEntry> LoadLogs()
    {
        try
        {
            if (!IOFile.Exists(_logFilePath))
                return new List<CronLogEntry>();

            var json = IOFile.ReadAllText(_logFilePath);
            var logs = JsonSerializer.Deserialize<List<CronLogEntry>>(json) ?? new List<CronLogEntry>();
            return logs;
        }
        catch (Exception)
        {
            // If there's an error reading the log file, return empty list
            return new List<CronLogEntry>();
        }
    }

    private void SaveLogs(List<CronLogEntry> logs)
    {
        try
        {
            var json = JsonSerializer.Serialize(logs, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            IOFile.WriteAllText(_logFilePath, json);
        }
        catch (Exception)
        {
            // Silently fail - don't want logging to break the application
        }
    }
}
