using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IOFile = System.IO.File;
using System.Reflection;
using xmlTVGuide.Services;

namespace xmlTVGuide.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly IEpgGenerationStatusTracker _statusTracker;

    public HealthController(IEpgGenerationStatusTracker statusTracker, ILogger<HealthController> logger)
    {
        _statusTracker = statusTracker;
    }

    /// <summary>
    /// Gets the assembly version from metadata.
    /// </summary>
    private string GetVersionInfo()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "unknown";
            var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;
            return informationalVersion;
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Gets the build/release date from assembly metadata.
    /// </summary>
    private DateTime? GetBuildDate()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            if (string.IsNullOrEmpty(fileVersion))
                return null;

            var filePath = assembly.Location;
            if (!IOFile.Exists(filePath))
                return null;

            return IOFile.GetLastWriteTimeUtc(filePath);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Determines health status based on guide file, last generation, and source availability.
    /// </summary>
    private (string status, string details) DetermineHealth(string outputPath, EpgGenerationStatus generationStatus)
    {
        var guideExists = IOFile.Exists(outputPath);
        var guideAge = guideExists ? DateTime.UtcNow - new FileInfo(outputPath).LastWriteTimeUtc : (TimeSpan?)null;
        var twoHoursAgo = TimeSpan.FromHours(2);

        // If no guide file, definitely unhealthy
        if (!guideExists)
        {
            var details = "No guide.xml file present";
            if (generationStatus.LastRunSuccess)
                details += " (last generation succeeded but file missing)";
            else if (generationStatus.LastRunMessage != null)
                details += $" (last generation failed: {generationStatus.LastRunMessage})";
            return ("unhealthy", details);
        }

        // If guide is very old and last generation failed, unhealthy
        if (guideAge > twoHoursAgo)
        {
            if (!generationStatus.HasRecordedRun)
                return ("degraded", $"Guide is stale ({guideAge:h\\:mm} old) and no generation history is available since startup");

            if (!generationStatus.LastRunSuccess)
                return ("unhealthy", $"Guide is stale ({guideAge:h\\:mm} old) and generation is failing");
        }

        // If last generation had all sources fail, unhealthy
        if (generationStatus.LastRunSuccess == false && generationStatus.TotalSources > 0 && generationStatus.SuccessfulSources == 0)
            return ("unhealthy", $"All {generationStatus.TotalSources} EPG source(s) failed in last run");

        // If some sources failed, degraded
        if (generationStatus.LastRunSuccess && generationStatus.TotalSources > 0 && generationStatus.SuccessfulSources < generationStatus.TotalSources)
            return ("degraded", $"Only {generationStatus.SuccessfulSources}/{generationStatus.TotalSources} EPG source(s) succeeded");

        // Guide is reasonably fresh and last generation succeeded, healthy
        return ("healthy", "Guide present and up-to-date");
    }

    [HttpGet]
    [Route("/health")]
    public IActionResult GetHealth()
    {
        var outputPath = Environment.GetEnvironmentVariable("OUTPUT_PATH") ?? "/app/output/guide.xml";
        var channelMapPath = Environment.GetEnvironmentVariable("CHANNEL_MAP_PATH") ?? "/app/ChannelMap.json";
        var epgUrlsPath = Environment.GetEnvironmentVariable("EPG_URL_FILES") ?? "/app/epg_urls.txt";

        var generationStatus = _statusTracker.GetCurrentStatus();
        var (healthStatus, healthDetails) = DetermineHealth(outputPath, generationStatus);

        var guideExists = IOFile.Exists(outputPath);
        var guideFileInfo = guideExists ? new FileInfo(outputPath) : null;
        var guideAge = guideFileInfo != null ? DateTime.UtcNow - guideFileInfo.LastWriteTimeUtc : (TimeSpan?)null;

        var health = new
        {
            status = healthStatus,
            details = healthDetails,
            timestamp = DateTime.UtcNow,
            service = "xmltvguide-generator",
            version = GetVersionInfo(),
            buildDate = GetBuildDate(),
            checks = new
            {
                guideFile = new
                {
                    exists = guideExists,
                    path = outputPath,
                    lastModified = guideFileInfo?.LastWriteTimeUtc,
                    ageMinutes = guideAge?.TotalMinutes,
                    sizeBytes = guideFileInfo?.Length ?? 0
                },
                channelMap = new
                {
                    exists = IOFile.Exists(channelMapPath),
                    path = channelMapPath
                },
                epgUrls = new
                {
                    exists = IOFile.Exists(epgUrlsPath),
                    path = epgUrlsPath
                }
            },
            lastGeneration = new
            {
                attemptedAt = generationStatus.LastRunAt,
                durationMs = generationStatus.LastRunDurationMs,
                success = generationStatus.HasRecordedRun ? generationStatus.LastRunSuccess : (bool?)null,
                message = generationStatus.HasRecordedRun ? generationStatus.LastRunMessage : null,
                successfulSources = generationStatus.SuccessfulSources,
                totalSources = generationStatus.TotalSources,
                failedSources = Math.Max(0, generationStatus.TotalSources - generationStatus.SuccessfulSources),
                warningCount = generationStatus.WarningDetails.Count,
                errorCount = generationStatus.ErrorDetails.Count
            }
        };

        return Ok(health);
    }

    [HttpGet]
    [Route("/health/ready")]
    public IActionResult GetReadiness()
    {
        var outputPath = Environment.GetEnvironmentVariable("OUTPUT_PATH") ?? "/app/output/guide.xml";
        var channelMapPath = Environment.GetEnvironmentVariable("CHANNEL_MAP_PATH") ?? "/app/ChannelMap.json";

        var guideExists = IOFile.Exists(outputPath);
        var channelMapExists = IOFile.Exists(channelMapPath);

        // Service is ready if channel map exists (guide can be generated on first run)
        var isReady = channelMapExists;

        var readiness = new
        {
            status = isReady ? "ready" : "not_ready",
            timestamp = DateTime.UtcNow,
            checks = new
            {
                channelMapConfigured = channelMapExists,
                guideGenerated = guideExists
            }
        };

        return isReady ? Ok(readiness) : StatusCode(503, readiness);
    }

    [HttpGet]
    [Route("/health/live")]
    public IActionResult GetLiveness()
    {
        // Simple liveness check - service is running if this endpoint responds
        return Ok(new
        {
            status = "alive",
            timestamp = DateTime.UtcNow
        });
    }
}
