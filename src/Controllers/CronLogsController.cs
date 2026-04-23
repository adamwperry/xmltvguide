using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using xmlTVGuide.Models;
using xmlTVGuide.Services.CronLogger;

namespace xmlTVGuide.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CronLogsController : ControllerBase
{
    private readonly ICronLogger _cronLogger;

    public CronLogsController(ICronLogger cronLogger)
    {
        _cronLogger = cronLogger;
    }

    [HttpGet]
    public IActionResult GetLogs([FromQuery] int count = 100)
    {
        try
        {
            var logs = _cronLogger.GetLastLogs(count);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving cron logs", error = ex.Message });
        }
    }

    [HttpDelete]
    public IActionResult ClearLogs()
    {
        try
        {
            _cronLogger.ClearLogs();
            return Ok(new { message = "Cron logs cleared successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error clearing cron logs", error = ex.Message });
        }
    }

    [HttpPost("test")]
    public IActionResult AddTestLogs()
    {
        try
        {
            var timestamp = DateTime.UtcNow;

            // Add some test log entries
            _cronLogger.LogCronRun("EPG generation completed successfully", timestamp.AddMinutes(-5), true);
            _cronLogger.LogCronRun("EPG generation completed successfully", timestamp.AddMinutes(-10), true);
            _cronLogger.LogCronRun("EPG generation failed - Network timeout", timestamp.AddMinutes(-15), false, "Failed to fetch EPG data from https://example.com/guide.xml: The operation has timed out");
            _cronLogger.LogCronRun("EPG generation completed successfully", timestamp.AddMinutes(-20), true);
            _cronLogger.LogCronRun("EPG generation completed successfully", timestamp.AddMinutes(-25), true);

            return Ok(new { message = "Test cron logs added successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error adding test logs", error = ex.Message });
        }
    }

    [HttpPost("log")]
    public IActionResult LogCronRun([FromBody] CronLogRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Message))
                return BadRequest(new { message = "Message is required" });

            _cronLogger.LogCronRun(
                request.Message,
                request.Timestamp ?? DateTime.UtcNow,
                request.Success,
                request.ErrorMessage
            );

            return Ok(new { message = "Cron run logged successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error logging cron run", error = ex.Message });
        }
    }
}

