namespace xmlTVGuide.Services.BackgroundJobs;

public class JobStatus
{
    public bool IsRunning { get; set; }
    public DateTime? StartTime { get; set; }
    public string? CurrentMessage { get; set; }
    public int? ProgressPercent { get; set; }
}

public class JobHistoryEntry
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
}
