namespace xmlTVGuide.Services;

using xmlTVGuide.Services.ArgumentParser;

public interface IEpgGenerationService
{
    Task<EpgGenerationResult> GenerateAsync(string[] args, CancellationToken cancellationToken = default);
}

public class EpgGenerationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public Exception? Exception { get; set; }
    public List<string> ErrorDetails { get; set; } = new();
    public List<string> WarningDetails { get; set; } = new();
}

public class EpgGenerationService : IEpgGenerationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EpgGenerationService> _logger;
    private readonly IEpgGenerationStatusTracker _statusTracker;

    public EpgGenerationService(IServiceProvider serviceProvider, ILogger<EpgGenerationService> logger, IEpgGenerationStatusTracker statusTracker)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _statusTracker = statusTracker;
    }

    public async Task<EpgGenerationResult> GenerateAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var status = new EpgGenerationStatus
        {
            HasRecordedRun = true,
            LastRunAt = startTime
        };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Starting XMLTV Guide Generator...");
            _logger.LogInformation($"EPG_URL_FILES: {Environment.GetEnvironmentVariable("EPG_URL_FILES")}");
            _logger.LogInformation($"CHANNEL_MAP_PATH: {Environment.GetEnvironmentVariable("CHANNEL_MAP_PATH")}");
            _logger.LogInformation($"OUTPUT_PATH: {Environment.GetEnvironmentVariable("OUTPUT_PATH")}");

            var argumentParser = _serviceProvider.GetService<IAppArguments>();
            if (argumentParser == null)
            {
                stopwatch.Stop();
                status.LastRunDurationMs = stopwatch.ElapsedMilliseconds;
                status.LastRunSuccess = false;
                status.LastRunMessage = "Failed to resolve IAppArguments service.";
                status.HealthStatus = "unhealthy";
                status.ErrorDetails.Add(status.LastRunMessage);
                _statusTracker.UpdateStatus(status);
                return new EpgGenerationResult { Success = false, Message = status.LastRunMessage };
            }

            var arguments = argumentParser.ParseArguments(args);
            cancellationToken.ThrowIfCancellationRequested();

            if (arguments.HelpSet)
            {
                stopwatch.Stop();
                status.LastRunDurationMs = stopwatch.ElapsedMilliseconds;
                status.LastRunSuccess = true;
                status.LastRunMessage = arguments.HelpText;
                status.HealthStatus = "unknown";
                _statusTracker.UpdateStatus(status);
                return new EpgGenerationResult { Success = true, Message = arguments.HelpText };
            }

            if (arguments.Fake)
                arguments.Urls = arguments.Urls.Count == 0
                    ? new List<string> { Path.Combine(Directory.GetCurrentDirectory(), "src", "TestData", "tvguide.json") }
                    : arguments.Urls;
            else if (arguments.Urls.Count == 0)
            {
                stopwatch.Stop();
                status.LastRunDurationMs = stopwatch.ElapsedMilliseconds;
                status.LastRunSuccess = false;
                status.LastRunMessage = "No URLs provided for EPG generation.";
                status.HealthStatus = "unhealthy";
                status.ErrorDetails.Add(status.LastRunMessage);
                _statusTracker.UpdateStatus(status);
                return new EpgGenerationResult { Success = false, Message = status.LastRunMessage };
            }

            var dataFetcher = _serviceProvider.GetService<IDataFetcher>();
            if (dataFetcher == null)
            {
                stopwatch.Stop();
                status.LastRunDurationMs = stopwatch.ElapsedMilliseconds;
                status.LastRunSuccess = false;
                status.LastRunMessage = "Failed to resolve IDataFetcher service.";
                status.HealthStatus = "unhealthy";
                status.ErrorDetails.Add(status.LastRunMessage);
                _statusTracker.UpdateStatus(status);
                return new EpgGenerationResult { Success = false, Message = status.LastRunMessage };
            }

            var xmlTVBuilder = _serviceProvider.GetService<IXmlTVBuilder>();
            if (xmlTVBuilder == null)
            {
                stopwatch.Stop();
                status.LastRunDurationMs = stopwatch.ElapsedMilliseconds;
                status.LastRunSuccess = false;
                status.LastRunMessage = "Failed to resolve IXmlTVBuilder service.";
                status.HealthStatus = "unhealthy";
                status.ErrorDetails.Add(status.LastRunMessage);
                _statusTracker.UpdateStatus(status);
                return new EpgGenerationResult { Success = false, Message = status.LastRunMessage };
            }

            if (string.IsNullOrEmpty(arguments.ChannelMapPath) || string.IsNullOrEmpty(arguments.OutputPath))
            {
                stopwatch.Stop();
                status.LastRunDurationMs = stopwatch.ElapsedMilliseconds;
                status.LastRunSuccess = false;
                status.LastRunMessage = "Channel map path and output path are required.";
                status.HealthStatus = "unhealthy";
                status.ErrorDetails.Add(status.LastRunMessage);
                _statusTracker.UpdateStatus(status);
                return new EpgGenerationResult { Success = false, Message = status.LastRunMessage };
            }

            // Fetch data with detailed error tracking per source
            var fetchResults = await dataFetcher.FetchDataWithResultsAsync(arguments.Urls, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // Track successes and failures
            var successfulData = new List<string>();
            var result = new EpgGenerationResult();
            status.TotalSources = fetchResults.Count;

            foreach (var fetchResult in fetchResults)
            {
                var sourceStatus = new SourceFetchStatus
                {
                    Url = fetchResult.Url,
                    Success = fetchResult.Success,
                    ErrorMessage = fetchResult.ErrorMessage,
                    HttpStatusCode = fetchResult.StatusCode,
                    ResponseTimeMs = fetchResult.ResponseTimeMs,
                    ResponseSizeBytes = fetchResult.ResponseSize,
                    FetchedAt = fetchResult.FetchedAt
                };
                status.SourceResults.Add(sourceStatus);

                if (fetchResult.Success && !string.IsNullOrEmpty(fetchResult.Data))
                {
                    successfulData.Add(fetchResult.Data);
                    status.SuccessfulSources++;
                    _logger.LogInformation($"✓ Successfully fetched from {fetchResult.Url} ({fetchResult.ResponseSize} bytes, {fetchResult.ResponseTimeMs}ms)");
                }
                else
                {
                    var errorMsg = $"✗ Failed to fetch from {fetchResult.Url}: {fetchResult.ErrorMessage} ({fetchResult.ResponseTimeMs}ms)";
                    _logger.LogWarning(errorMsg);
                    result.WarningDetails.Add(errorMsg);
                    status.WarningDetails.Add(errorMsg);
                }
            }

            // If no sources succeeded, return error
            if (successfulData.Count == 0)
            {
                stopwatch.Stop();
                status.LastRunDurationMs = stopwatch.ElapsedMilliseconds;
                var errorSummary = $"All {fetchResults.Count} EPG source(s) failed to fetch. No data available for guide generation.";
                _logger.LogError(errorSummary);
                result.Success = false;
                result.Message = errorSummary;
                result.ErrorDetails.Add(errorSummary);
                status.LastRunSuccess = false;
                status.LastRunMessage = errorSummary;
                status.HealthStatus = "unhealthy";

                foreach (var failedResult in fetchResults.Where(r => !r.Success))
                {
                    var detail = $"  • {failedResult.Url}: {failedResult.ErrorMessage}";
                    result.ErrorDetails.Add(detail);
                    status.ErrorDetails.Add(detail);
                }

                _statusTracker.UpdateStatus(status);
                return result;
            }

            // If some sources failed but others succeeded, log warnings
            if (successfulData.Count < fetchResults.Count)
            {
                var partialFailureMsg = $"Partial EPG fetch: {successfulData.Count}/{fetchResults.Count} sources succeeded. Continuing with available data.";
                _logger.LogWarning(partialFailureMsg);
                result.WarningDetails.Add(partialFailureMsg);
                status.WarningDetails.Add(partialFailureMsg);
            }

            // Build XML TV from successful data
            xmlTVBuilder.BuildXmlTV(
                successfulData,
                arguments.ChannelMapPath,
                arguments.OutputPath,
                arguments.StripChannelNumbers,
                arguments.SortChannelsByIdThenDisplayName);
            cancellationToken.ThrowIfCancellationRequested();

            // Update status with file info
            var outputPath = arguments.OutputPath;
            if (File.Exists(outputPath))
            {
                var fileInfo = new FileInfo(outputPath);
                status.GuideGeneratedAt = fileInfo.LastWriteTimeUtc;
                status.GuideFileSizeBytes = fileInfo.Length;
            }

            stopwatch.Stop();
            status.LastRunDurationMs = stopwatch.ElapsedMilliseconds;
            var successMessage = $"XML guide.xml has been generated successfully ({successfulData.Count} source(s) used).";
            _logger.LogInformation(successMessage);
            result.Success = true;
            result.Message = successMessage;
            status.LastRunSuccess = true;
            status.LastRunMessage = successMessage;
            status.HealthStatus = successfulData.Count == fetchResults.Count ? "healthy" : "degraded";

            _statusTracker.UpdateStatus(status);
            return result;
        }
        catch (OperationCanceledException ex)
        {
            stopwatch.Stop();
            status.LastRunDurationMs = stopwatch.ElapsedMilliseconds;
            status.LastRunSuccess = false;
            status.LastRunMessage = "EPG generation was cancelled.";
            status.HealthStatus = "cancelled";
            status.ErrorDetails.Add(status.LastRunMessage);
            _statusTracker.UpdateStatus(status);
            return new EpgGenerationResult
            {
                Success = false,
                Message = status.LastRunMessage,
                Exception = ex,
                ErrorDetails = new() { status.LastRunMessage }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            status.LastRunDurationMs = stopwatch.ElapsedMilliseconds;
            _logger.LogError(ex, "An error occurred during EPG generation");
            status.LastRunSuccess = false;
            status.LastRunMessage = $"An error occurred: {ex.Message}";
            status.HealthStatus = "unhealthy";
            status.ErrorDetails.Add(ex.ToString());
            _statusTracker.UpdateStatus(status);
            return new EpgGenerationResult
            {
                Success = false,
                Message = status.LastRunMessage,
                Exception = ex,
                ErrorDetails = new() { ex.ToString() }
            };
        }
    }
}
