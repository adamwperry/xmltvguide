namespace xmlTVGuide.Models;

public class CronLogRequest
{
    public string Message { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
