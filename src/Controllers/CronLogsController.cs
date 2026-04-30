using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Net;
using xmlTVGuide.Models;
using xmlTVGuide.Services.CronLogger;
using IOFile = System.IO.File;

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

    [HttpGet("schedule")]
    public IActionResult GetSchedule()
    {
        try
        {
            var crontabPath = Environment.GetEnvironmentVariable("CRONTAB_PATH");
            if (string.IsNullOrWhiteSpace(crontabPath))
            {
                crontabPath = IOFile.Exists("/app/crontab.txt")
                    ? "/app/crontab.txt"
                    : Path.Combine(Environment.CurrentDirectory, "crontab.txt");
            }

            var scheduleLine = ReadScheduleLine(crontabPath);
            var expression = scheduleLine is null
                ? null
                : string.Join(" ", scheduleLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(5));

            var lastRun = (_cronLogger.GetLastLogs(1) ?? new List<CronLogEntry>()).FirstOrDefault();

            return Ok(new
            {
                enabled = scheduleLine is not null,
                crontabPath,
                expression,
                scheduleLine,
                nextRunUtc = expression is null ? null : TryGetNextRunUtc(expression, DateTime.UtcNow),
                lastRunAt = lastRun?.Timestamp,
                lastRunSuccess = lastRun?.Success,
                lastRunMessage = lastRun?.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving cron schedule", error = ex.Message });
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

    private static string? ReadScheduleLine(string crontabPath)
    {
        if (!IOFile.Exists(crontabPath))
            return null;

        return IOFile.ReadLines(crontabPath)
            .Select(line => line.Trim())
            .FirstOrDefault(line =>
                !string.IsNullOrWhiteSpace(line) &&
                !line.StartsWith("#", StringComparison.Ordinal) &&
                line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 6 &&
                line.Contains("cron-wrapper", StringComparison.OrdinalIgnoreCase));
    }

    private static DateTime? TryGetNextRunUtc(string expression, DateTime nowUtc)
    {
        var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
            return null;

        var minutePart = parts[0];
        if (!minutePart.StartsWith("*/", StringComparison.Ordinal) ||
            !int.TryParse(minutePart[2..], out var intervalMinutes) ||
            intervalMinutes <= 0)
        {
            return null;
        }

        var startOfHour = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, 0, 0, DateTimeKind.Utc);
        var nextMinute = ((nowUtc.Minute / intervalMinutes) + 1) * intervalMinutes;

        return nextMinute >= 60
            ? startOfHour.AddHours(1)
            : startOfHour.AddMinutes(nextMinute);
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
    [AllowAnonymous]
    public IActionResult LogCronRun([FromBody] CronLogRequest request)
    {
        try
        {
            if (!IsAuthorizedCronLogRequest())
                return Unauthorized(new { message = "Cron log endpoint is only available to local cron jobs." });

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

    private bool IsAuthorizedCronLogRequest()
    {
        var token = Environment.GetEnvironmentVariable("CRON_LOG_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
        {
            return Request.Headers.TryGetValue("X-Cron-Log-Token", out var providedToken) &&
                string.Equals(providedToken.ToString(), token, StringComparison.Ordinal);
        }

        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        return remoteIp is not null && IPAddress.IsLoopback(remoteIp);
    }
}
