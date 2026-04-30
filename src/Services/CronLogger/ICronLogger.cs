namespace xmlTVGuide.Services.CronLogger;

public interface ICronLogger
{
    void LogCronRun(string message, DateTime timestamp, bool success, string? errorMessage = null);
    List<CronLogEntry> GetLastLogs(int count = 100);
    void ClearLogs();
}

public class CronLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
