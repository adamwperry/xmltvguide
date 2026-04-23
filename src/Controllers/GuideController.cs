using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using xmlTVGuide.Services;
using xmlTVGuide.Services.CronLogger;
using xmlTVGuide.Services.BackgroundJobs;
using IOFile = System.IO.File;

namespace xmlTVGuide.Controllers;

[Route("")]
[Authorize]
public class GuideController : ControllerBase
{
    private readonly ICronLogger _cronLogger;
    private readonly IEpgGenerationService _generationService;
    private readonly IBackgroundJobService _backgroundJobService;

    public GuideController(ICronLogger cronLogger, IEpgGenerationService generationService, IBackgroundJobService backgroundJobService)
    {
        _cronLogger = cronLogger;
        _generationService = generationService;
        _backgroundJobService = backgroundJobService;
    }

    [HttpGet("guide.xml")]
    public IActionResult GetGuideXml()
    {
        var outputPath = Environment.GetEnvironmentVariable("OUTPUT_PATH") ?? "/app/output/guide.xml";

        if (!IOFile.Exists(outputPath))
            return NotFound("Guide XML file not found. The EPG generation may not have completed yet.");

        try
        {
            return PhysicalFile(outputPath, "application/xml");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error reading guide XML: {ex.Message}");
        }
    }

    [HttpGet("status")]
    public IActionResult GetGuideStatus()
    {
        var outputPath = Environment.GetEnvironmentVariable("OUTPUT_PATH") ?? "/app/output/guide.xml";
        var exists = IOFile.Exists(outputPath);

        var status = new
        {
            guideExists = exists,
            guidePath = outputPath,
            lastModified = exists ? IOFile.GetLastWriteTime(outputPath) : (DateTime?)null,
            fileSize = exists ? new FileInfo(outputPath).Length : 0
        };

        return Ok(status);
    }

    [HttpPost("rebuild")]
    public async Task<IActionResult> RebuildGuide()
    {
        try
        {
            var epgUrlsPath = Environment.GetEnvironmentVariable("EPG_URL_FILES") ?? "/app/epg_urls.txt";
            var channelMapPath = Environment.GetEnvironmentVariable("CHANNEL_MAP_PATH") ?? "/app/ChannelMap.json";
            var outputPath = Environment.GetEnvironmentVariable("OUTPUT_PATH") ?? "/app/output/guide.xml";

            // Check if EPG URLs file exists
            if (!IOFile.Exists(epgUrlsPath))
                return BadRequest("EPG URLs file not found. Please configure EPG sources first.");

            // Check if Channel Map file exists
            if (!IOFile.Exists(channelMapPath))
                return BadRequest("Channel map file not found. Please configure channel mapping first.");

            // Attempt to start the rebuild job
            async Task RunRebuild()
            {
                try
                {
                    var result = await _generationService.GenerateAsync(new[] {
                        $"--epgUrlFiles={epgUrlsPath}",
                        $"--channelmap={channelMapPath}",
                        $"--output={outputPath}"
                    });

                    if (!result.Success)
                        throw new Exception(result.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"EPG generation error: {ex.Message}");
                    throw;
                }
            }

            var (canStart, message) = await _backgroundJobService.TryStartJobAsync(RunRebuild, "EPG Rebuild");

            if (!canStart)
                return Conflict(new { message });

            return Accepted(new { message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error starting EPG rebuild: {ex.Message}" });
        }
    }

    [HttpGet("api/rebuild/status")]
    public IActionResult GetRebuildStatus()
    {
        var status = _backgroundJobService.GetCurrentStatus();
        return Ok(status);
    }

    [HttpGet("api/rebuild/history")]
    public IActionResult GetRebuildHistory([FromQuery] int count = 50)
    {
        var history = _backgroundJobService.GetHistory(Math.Min(count, 200));
        return Ok(new { history, count = history.Count });
    }

    [HttpPost("api/rebuild/cancel")]
    public IActionResult CancelRebuild()
    {
        _backgroundJobService.CancelCurrent();
        return Ok(new { message = "Cancellation request sent" });
    }
}