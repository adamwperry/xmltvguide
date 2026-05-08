using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Net;
using xmlTVGuide.Models;
using xmlTVGuide.Services.CronLogger;
using xmlTVGuide.Services.BuildJobLogger;
using IOFile = System.IO.File;

namespace xmlTVGuide.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CronLogsController : ControllerBase
{
    private readonly ICronLogger _cronLogger;
    private readonly IBuildJobLogger _buildJobLogger;

    public CronLogsController(ICronLogger cronLogger, IBuildJobLogger buildJobLogger)
    {
        _cronLogger = cronLogger;
        _buildJobLogger = buildJobLogger;
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

            var cronRun = (_cronLogger.GetLastLogs(1) ?? new List<CronLogEntry>()).FirstOrDefault();
            var rebuildRun = (_buildJobLogger.GetLastJobs(1) ?? new List<BuildJobEntry>()).FirstOrDefault();
            var lastRunAt = GetMostRecentRunAt(cronRun, rebuildRun);
            var lastRunSuccess = GetMostRecentRunSuccess(cronRun, rebuildRun);
            var lastRunMessage = GetMostRecentRunMessage(cronRun, rebuildRun);

            return Ok(new
            {
                enabled = scheduleLine is not null,
                crontabPath,
                expression,
                scheduleLine,
                nextRunUtc = expression is null ? null : TryGetNextRunUtc(expression, DateTime.UtcNow),
                lastRunAt,
                lastRunSuccess,
                lastRunMessage
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

        var minuteMatcher = CronFieldMatcher.TryParse(parts[0], 0, 59);
        var hourMatcher = CronFieldMatcher.TryParse(parts[1], 0, 23);
        var dayOfMonthMatcher = CronFieldMatcher.TryParse(parts[2], 1, 31);
        var monthMatcher = CronFieldMatcher.TryParse(parts[3], 1, 12);
        var dayOfWeekMatcher = CronFieldMatcher.TryParse(parts[4], 0, 7);

        if (minuteMatcher is null || hourMatcher is null || dayOfMonthMatcher is null || monthMatcher is null || dayOfWeekMatcher is null)
            return null;

        var cursor = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, nowUtc.Minute, 0, DateTimeKind.Utc)
            .AddMinutes(1);
        var maxYear = cursor.Year + 5;

        while (cursor.Year <= maxYear)
        {
            if (!monthMatcher.TryGetNextOrSame(cursor.Month, out var nextMonth))
            {
                cursor = new DateTime(cursor.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                continue;
            }

            if (nextMonth != cursor.Month)
            {
                cursor = new DateTime(cursor.Year, nextMonth, 1, 0, 0, 0, DateTimeKind.Utc);
                continue;
            }

            if (!IsDayMatch(cursor, dayOfMonthMatcher, dayOfWeekMatcher))
            {
                cursor = cursor.Date.AddDays(1);
                continue;
            }

            if (!hourMatcher.TryGetNextOrSame(cursor.Hour, out var nextHour))
            {
                cursor = cursor.Date.AddDays(1);
                continue;
            }

            if (nextHour != cursor.Hour)
            {
                cursor = new DateTime(cursor.Year, cursor.Month, cursor.Day, nextHour, 0, 0, DateTimeKind.Utc);
                continue;
            }

            if (!minuteMatcher.TryGetNextOrSame(cursor.Minute, out var nextMinute))
            {
                cursor = new DateTime(cursor.Year, cursor.Month, cursor.Day, cursor.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
                continue;
            }

            if (nextMinute != cursor.Minute)
                return new DateTime(cursor.Year, cursor.Month, cursor.Day, cursor.Hour, nextMinute, 0, DateTimeKind.Utc);

            return cursor;
        }

        return null;
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

    private static DateTime? GetMostRecentRunAt(CronLogEntry? cronRun, BuildJobEntry? rebuildRun)
    {
        if (cronRun is null)
            return rebuildRun?.EndTime ?? rebuildRun?.StartTime;

        var rebuildTimestamp = rebuildRun?.EndTime ?? rebuildRun?.StartTime;
        if (rebuildTimestamp is null || cronRun.Timestamp >= rebuildTimestamp.Value)
            return cronRun.Timestamp;

        return rebuildTimestamp;
    }

    private static bool? GetMostRecentRunSuccess(CronLogEntry? cronRun, BuildJobEntry? rebuildRun)
    {
        if (cronRun is null)
            return rebuildRun?.Success;

        var rebuildTimestamp = rebuildRun?.EndTime ?? rebuildRun?.StartTime;
        if (rebuildTimestamp is null || cronRun.Timestamp >= rebuildTimestamp.Value)
            return cronRun.Success;

        return rebuildRun?.Success;
    }

    private static string? GetMostRecentRunMessage(CronLogEntry? cronRun, BuildJobEntry? rebuildRun)
    {
        if (cronRun is null)
            return rebuildRun?.Message;

        var rebuildTimestamp = rebuildRun?.EndTime ?? rebuildRun?.StartTime;
        if (rebuildTimestamp is null || cronRun.Timestamp >= rebuildTimestamp.Value)
            return cronRun.Message;

        return rebuildRun?.Message;
    }

    private static bool IsDayMatch(DateTime timestampUtc, CronFieldMatcher dayOfMonthMatcher, CronFieldMatcher dayOfWeekMatcher)
    {
        var dayOfMonthWildcard = dayOfMonthMatcher.IsWildcard;
        var dayOfWeekWildcard = dayOfWeekMatcher.IsWildcard;

        var dayOfMonthMatch = dayOfMonthMatcher.IsMatch(timestampUtc.Day);
        var dayOfWeekValue = timestampUtc.DayOfWeek == DayOfWeek.Sunday ? 0 : (int)timestampUtc.DayOfWeek;
        var dayOfWeekMatch = dayOfWeekMatcher.IsMatch(dayOfWeekValue);

        if (dayOfMonthWildcard && dayOfWeekWildcard)
            return true;

        if (dayOfMonthWildcard)
            return dayOfWeekMatch;

        if (dayOfWeekWildcard)
            return dayOfMonthMatch;

        return dayOfMonthMatch || dayOfWeekMatch;
    }
}

internal sealed class CronFieldMatcher
{
    private readonly HashSet<int> _values;
    private readonly List<int> _sortedValues;

    private CronFieldMatcher(HashSet<int> values, bool isWildcard)
    {
        _values = values;
        _sortedValues = values.OrderBy(value => value).ToList();
        IsWildcard = isWildcard;
    }

    public bool IsWildcard { get; }

    public bool IsMatch(int value)
    {
        if (IsWildcard)
            return true;

        if (value == 0 && _values.Contains(7))
            return true;

        return _values.Contains(value);
    }

    public bool TryGetNextOrSame(int value, out int nextValue)
    {
        if (IsWildcard)
        {
            nextValue = value;
            return true;
        }

        foreach (var candidate in _sortedValues)
        {
            if (candidate >= value)
            {
                nextValue = candidate;
                return true;
            }
        }

        nextValue = default;
        return false;
    }

    public static CronFieldMatcher? TryParse(string field, int min, int max)
    {
        if (string.IsNullOrWhiteSpace(field))
            return null;

        if (field == "*")
            return new CronFieldMatcher(new HashSet<int>(), true);

        var values = new HashSet<int>();
        foreach (var segment in field.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseSegment(segment, min, max, values))
                return null;
        }

        return new CronFieldMatcher(values, false);
    }

    private static bool TryParseSegment(string segment, int min, int max, HashSet<int> values)
    {
        if (segment.Contains('/'))
        {
            var parts = segment.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var step) || step <= 0)
                return false;

            var range = parts[0];
            if (range == "*")
                return AddRange(min, max, step, values);

            var bounds = range.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (bounds.Length != 2 ||
                !int.TryParse(bounds[0], out var start) ||
                !int.TryParse(bounds[1], out var end))
            {
                return false;
            }

            if (!IsInRange(start, min, max) || !IsInRange(end, min, max) || start > end)
                return false;

            return AddRange(start, end, step, values);
        }

        if (segment.Contains('-'))
        {
            var bounds = segment.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (bounds.Length != 2 ||
                !int.TryParse(bounds[0], out var start) ||
                !int.TryParse(bounds[1], out var end))
            {
                return false;
            }

            if (!IsInRange(start, min, max) || !IsInRange(end, min, max) || start > end)
                return false;

            return AddRange(start, end, 1, values);
        }

        if (!int.TryParse(segment, out var singleValue) || !IsInRange(singleValue, min, max))
            return false;

        values.Add(singleValue);
        return true;
    }

    private static bool AddRange(int start, int end, int step, HashSet<int> values)
    {
        for (var value = start; value <= end; value += step)
            values.Add(value);

        return true;
    }

    private static bool IsInRange(int value, int min, int max) => value >= min && value <= max;
}
