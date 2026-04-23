using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IOFile = System.IO.File;

namespace xmlTVGuide.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    [HttpGet]
    [Route("/health")]
    public IActionResult GetHealth()
    {
        var outputPath = Environment.GetEnvironmentVariable("OUTPUT_PATH") ?? "/app/output/guide.xml";
        var channelMapPath = Environment.GetEnvironmentVariable("CHANNEL_MAP_PATH") ?? "/app/ChannelMap.json";
        var epgUrlsPath = Environment.GetEnvironmentVariable("EPG_URL_FILES") ?? "/app/epg_urls.txt";

        var guideExists = IOFile.Exists(outputPath);
        var channelMapExists = IOFile.Exists(channelMapPath);
        var epgUrlsExists = IOFile.Exists(epgUrlsPath);

        var health = new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "xmltvguide-generator",
            version = "1.2.0",
            checks = new
            {
                guideFile = new
                {
                    exists = guideExists,
                    path = outputPath,
                    lastModified = guideExists ? IOFile.GetLastWriteTimeUtc(outputPath) : (DateTime?)null,
                    sizeBytes = guideExists ? new FileInfo(outputPath).Length : 0
                },
                channelMap = new
                {
                    exists = channelMapExists,
                    path = channelMapPath
                },
                epgUrls = new
                {
                    exists = epgUrlsExists,
                    path = epgUrlsPath
                }
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
