using Microsoft.AspNetCore.Mvc;
using xmlTVGuide.Services.CronLogger;
using IOFile = System.IO.File;

namespace xmlTVGuide.Controllers;

[Route("")]
public class GuideController : ControllerBase
{
    private readonly ICronLogger _cronLogger;

    public GuideController(ICronLogger cronLogger)
    {
        _cronLogger = cronLogger;
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
    public IActionResult RebuildGuide()
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

            // Log the manual rebuild
            _cronLogger.LogCronRun("Manual EPG rebuild initiated via web interface", DateTime.UtcNow, true);

            // Run EPG generation in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await xmlTVGuide.Program.RunEpgGenerationForWeb(new[] {
                        $"--epgUrlFiles={epgUrlsPath}",
                        $"--channelmap={channelMapPath}",
                        $"--output={outputPath}"
                    });
                    
                    // Log successful completion
                    _cronLogger.LogCronRun("Manual EPG rebuild completed successfully", DateTime.UtcNow, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"EPG generation error: {ex.Message}");
                    // Log the error
                    _cronLogger.LogCronRun("Manual EPG rebuild failed", DateTime.UtcNow, false, ex.Message);
                }
            });

            return Ok(new { message = "EPG rebuild started. Check status for completion." });
        }
        catch (Exception ex)
        {
            // Log the error
            _cronLogger.LogCronRun("Failed to start manual EPG rebuild", DateTime.UtcNow, false, ex.Message);
            return StatusCode(500, $"Error starting EPG rebuild: {ex.Message}");
        }
    }
}