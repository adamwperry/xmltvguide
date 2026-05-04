using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using xmlTVGuide.Services.BuildJobLogger;

namespace xmlTVGuide.Services.BackgroundJobs;

public class BackgroundJobService : IBackgroundJobService
{
    private readonly SemaphoreSlim _jobSemaphore = new(1, 1);
    private readonly IBuildJobLogger _buildJobLogger;
    private readonly ILogger<BackgroundJobService> _logger;
    private CancellationTokenSource? _currentJobCancellation;
    private Task? _currentJobTask;
    private JobStatus _currentStatus = new();

    public BackgroundJobService(IBuildJobLogger buildJobLogger, ILogger<BackgroundJobService> logger)
    {
        _buildJobLogger = buildJobLogger;
        _logger = logger;
    }

    public Task<(bool canStart, string message)> TryStartJobAsync(Func<CancellationToken, Task> jobAction, string jobName)
    {
        // Try to acquire the semaphore without blocking
        if (!_jobSemaphore.Wait(0))
            return Task.FromResult((false, "A job is already running. Please wait for it to complete."));

        try
        {
            // Mark job as started
            _currentStatus = new JobStatus
            {
                IsRunning = true,
                StartTime = DateTime.UtcNow,
                CurrentMessage = $"Starting {jobName}..."
            };

            _currentJobCancellation = new CancellationTokenSource();
            var cancellationToken = _currentJobCancellation.Token;

            // Fire the job on thread pool
            _currentJobTask = Task.Run(async () =>
            {
                var startTime = DateTime.UtcNow;

                try
                {
                    _logger.LogInformation("Background job '{JobName}' started", jobName);
                    await jobAction(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    _currentStatus.CurrentMessage = $"{jobName} completed successfully";

                    _logger.LogInformation("Background job '{JobName}' completed successfully", jobName);

                    // Log to persistent storage
                    _buildJobLogger.LogBuildJob(startTime, DateTime.UtcNow, DateTime.UtcNow - startTime, true, $"{jobName} completed successfully");
                }
                catch (OperationCanceledException)
                {
                    var duration = DateTime.UtcNow - startTime;
                    _currentStatus.CurrentMessage = $"{jobName} was cancelled";

                    _logger.LogInformation("Background job '{JobName}' was cancelled", jobName);

                    // Log to persistent storage
                    _buildJobLogger.LogBuildJob(startTime, DateTime.UtcNow, duration, false, $"{jobName} was cancelled", "Job was cancelled by user");
                }
                catch (Exception ex)
                {
                    var duration = DateTime.UtcNow - startTime;
                    _currentStatus.CurrentMessage = $"Error: {ex.Message}";

                    _logger.LogError(ex, "Background job '{JobName}' failed", jobName);

                    // Log to persistent storage
                    _buildJobLogger.LogBuildJob(startTime, DateTime.UtcNow, duration, false, $"{jobName} failed", ex.Message);
                }
                finally
                {
                    // Mark as completed
                    _currentStatus.IsRunning = false;
                    _currentJobCancellation?.Dispose();
                    _currentJobCancellation = null;
                    _currentJobTask = null;

                    // Release the semaphore for next job
                    _jobSemaphore.Release();
                }
            }, cancellationToken);

            return Task.FromResult((true, $"{jobName} started successfully"));
        }
        catch (Exception ex)
        {
            _jobSemaphore.Release();
            _logger.LogError(ex, "Error starting background job '{JobName}'", jobName);
            return Task.FromResult((false, $"Error starting job: {ex.Message}"));
        }
    }

    public JobStatus GetCurrentStatus()
    {
        return _currentStatus;
    }

    public List<JobHistoryEntry> GetHistory(int count = 50)
    {
        var buildJobs = _buildJobLogger.GetLastJobs(count);
        return buildJobs.Select(j => new JobHistoryEntry
        {
            StartTime = j.StartTime,
            EndTime = j.EndTime,
            Duration = j.Duration,
            DurationSeconds = j.Duration.HasValue ? (int)Math.Round(j.Duration.Value.TotalSeconds) : null,
            Success = j.Success,
            Message = j.Message,
            ErrorMessage = j.ErrorMessage
        }).ToList();
    }

    public void CancelCurrent()
    {
        if (_currentJobCancellation != null && !_currentJobCancellation.Token.IsCancellationRequested)
        {
            _logger.LogInformation("Cancellation requested for current background job");
            _currentJobCancellation.Cancel();
        }
    }

    public void ClearHistory()
    {
        _buildJobLogger.ClearLogs();
    }
}
