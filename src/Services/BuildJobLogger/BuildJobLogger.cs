using System.Text.Json;
using IOFile = System.IO.File;

namespace xmlTVGuide.Services.BuildJobLogger;

public class BuildJobLogger : IBuildJobLogger
{
    private readonly string _logFilePath;
    private readonly int _maxLogEntries = 200;
    private readonly object _lock = new object();

    public BuildJobLogger()
    {
        // Use environment variable or default path
        var logDir = Environment.GetEnvironmentVariable("LOG_PATH");
        if (string.IsNullOrEmpty(logDir))
        {
            // When running locally (development mode)
            logDir = Path.Combine(Environment.CurrentDirectory, "logs");
        }

        Directory.CreateDirectory(logDir);
        _logFilePath = Path.Combine(logDir, "rebuild.log.json");
    }

    public void LogBuildJob(DateTime startTime, DateTime? endTime, TimeSpan? duration, bool success, string message, string? errorMessage = null)
    {
        lock (_lock)
        {
            var logs = LoadLogs();

            var newEntry = new BuildJobEntry
            {
                StartTime = startTime,
                EndTime = endTime,
                Duration = duration,
                Success = success,
                Message = message,
                ErrorMessage = errorMessage
            };

            logs.Insert(0, newEntry); // Add to beginning

            // Keep only the last N entries
            if (logs.Count > _maxLogEntries)
                logs = logs.Take(_maxLogEntries).ToList();

            SaveLogs(logs);
        }
    }

    public List<BuildJobEntry> GetLastJobs(int count = 100)
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
            SaveLogs(new List<BuildJobEntry>());
        }
    }

    private List<BuildJobEntry> LoadLogs()
    {
        try
        {
            if (!IOFile.Exists(_logFilePath))
                return new List<BuildJobEntry>();

            var json = IOFile.ReadAllText(_logFilePath);
            var logs = JsonSerializer.Deserialize<List<BuildJobEntry>>(json) ?? new List<BuildJobEntry>();
            return logs;
        }
        catch (Exception)
        {
            // If there's an error reading the log file, return empty list
            return new List<BuildJobEntry>();
        }
    }

    private void SaveLogs(List<BuildJobEntry> logs)
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
