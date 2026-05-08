namespace xmlTVGuide.Services.BackgroundJobs;

public interface IBackgroundJobService
{
    /// <summary>
    /// Attempts to start a background job. Returns false if a job is already running.
    /// </summary>
    Task<(bool canStart, string message)> TryStartJobAsync(Func<CancellationToken, Task> jobAction, string jobName);

    /// <summary>
    /// Gets the current status of any running background job.
    /// </summary>
    JobStatus GetCurrentStatus();

    /// <summary>
    /// Gets the job history, limited to the specified count.
    /// </summary>
    List<JobHistoryEntry> GetHistory(int count = 50);

    /// <summary>
    /// Cancels the currently running job if one exists.
    /// </summary>
    void CancelCurrent();

    /// <summary>
    /// Clears persisted background job history.
    /// </summary>
    void ClearHistory();
}
