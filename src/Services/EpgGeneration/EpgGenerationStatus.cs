namespace xmlTVGuide.Services;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the status of a single EPG source fetch attempt.
/// </summary>
public class SourceFetchStatus
{
    public string? Url { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? HttpStatusCode { get; set; }
    public long ResponseTimeMs { get; set; }
    public long ResponseSizeBytes { get; set; }
    public DateTime FetchedAt { get; set; }
}

/// <summary>
/// Represents the current state of EPG generation, persisted in memory.
/// This is used by health checks to report on the last generation run.
/// </summary>
public class EpgGenerationStatus
{
    /// <summary>
    /// Whether the current process has recorded at least one generation attempt.
    /// </summary>
    public bool HasRecordedRun { get; set; }

    /// <summary>
    /// When the last generation was attempted (UTC).
    /// </summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>
    /// How long the last generation took in milliseconds.
    /// </summary>
    public long? LastRunDurationMs { get; set; }

    /// <summary>
    /// Whether the last generation was successful.
    /// </summary>
    public bool LastRunSuccess { get; set; }

    /// <summary>
    /// Overall message from the last run.
    /// </summary>
    public string? LastRunMessage { get; set; }

    /// <summary>
    /// When the generated guide file was created/updated.
    /// </summary>
    public DateTime? GuideGeneratedAt { get; set; }

    /// <summary>
    /// Size of the generated guide.xml file in bytes.
    /// </summary>
    public long GuideFileSizeBytes { get; set; }

    /// <summary>
    /// Number of EPG sources that were successfully fetched.
    /// </summary>
    public int SuccessfulSources { get; set; }

    /// <summary>
    /// Total number of EPG sources that were attempted.
    /// </summary>
    public int TotalSources { get; set; }

    /// <summary>
    /// Per-source fetch results from the last run.
    /// </summary>
    public List<SourceFetchStatus> SourceResults { get; set; } = new();

    /// <summary>
    /// Any warnings or non-fatal issues from the last run.
    /// </summary>
    public List<string> WarningDetails { get; set; } = new();

    /// <summary>
    /// Any errors or fatal issues from the last run.
    /// </summary>
    public List<string> ErrorDetails { get; set; } = new();

    /// <summary>
    /// Overall health status based on the last run and current file state.
    /// </summary>
    public string HealthStatus { get; set; } = "unknown";
}

/// <summary>
/// Service for tracking and retrieving EPG generation status.
/// Stores the most recent generation result for reporting in health checks.
/// </summary>
public interface IEpgGenerationStatusTracker
{
    /// <summary>
    /// Updates the current status with the results of a generation run.
    /// </summary>
    void UpdateStatus(EpgGenerationStatus status);

    /// <summary>
    /// Gets the current EPG generation status.
    /// </summary>
    EpgGenerationStatus GetCurrentStatus();

    /// <summary>
    /// Clears all status information.
    /// </summary>
    void ClearStatus();
}

/// <summary>
/// In-memory implementation of EPG generation status tracking.
/// Status is stored for the lifetime of the application.
/// </summary>
public class InMemoryEpgGenerationStatusTracker : IEpgGenerationStatusTracker
{
    private EpgGenerationStatus _currentStatus = new();
    private readonly object _lockObj = new();

    public void UpdateStatus(EpgGenerationStatus status)
    {
        lock (_lockObj)
        {
            _currentStatus = status ?? new EpgGenerationStatus();
        }
    }

    public EpgGenerationStatus GetCurrentStatus()
    {
        lock (_lockObj)
        {
            return _currentStatus;
        }
    }

    public void ClearStatus()
    {
        lock (_lockObj)
        {
            _currentStatus = new EpgGenerationStatus();
        }
    }
}
