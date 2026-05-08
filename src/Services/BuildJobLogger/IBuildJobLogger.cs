namespace xmlTVGuide.Services.BuildJobLogger;

public interface IBuildJobLogger
{
    void LogBuildJob(DateTime startTime, DateTime? endTime, TimeSpan? duration, bool success, string message, string? errorMessage = null);
    List<BuildJobEntry> GetLastJobs(int count = 100);
    void ClearLogs();
}

public class BuildJobEntry
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
