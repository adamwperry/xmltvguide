using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using xmlTVGuide.Controllers;
using xmlTVGuide.Models;
using xmlTVGuide.Services.BuildJobLogger;
using xmlTVGuide.Services.CronLogger;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class CronLogsControllerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Dictionary<string, string?> _originalEnv = new()
    {
        ["CRONTAB_PATH"] = Environment.GetEnvironmentVariable("CRONTAB_PATH"),
        ["CRON_LOG_TOKEN"] = Environment.GetEnvironmentVariable("CRON_LOG_TOKEN")
    };

    public CronLogsControllerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"xmltvguide-cron-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("CRON_LOG_TOKEN", null);
    }

    [Fact]
    public void GetSchedule_ReturnsScheduleAndLastRunMetadata()
    {
        var crontabPath = Path.Combine(_tempDir, "crontab.txt");
        File.WriteAllText(crontabPath, "*/20 * * * * /app/cron-wrapper.sh >> /var/log/cron.log 2>&1\n");
        Environment.SetEnvironmentVariable("CRONTAB_PATH", crontabPath);

        var cronLogger = new Mock<ICronLogger>();
        var buildJobLogger = new Mock<IBuildJobLogger>();
        cronLogger
            .Setup(logger => logger.GetLastLogs(1))
            .Returns(new List<CronLogEntry>
            {
                new() { Timestamp = DateTime.UtcNow.AddMinutes(-5), Success = true, Message = "ok" }
            });

        var controller = new CronLogsController(cronLogger.Object, buildJobLogger.Object);

        var result = controller.GetSchedule();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"enabled\":true");
        json.Should().Contain("\"expression\":\"*/20 * * * *\"");
        json.Should().Contain("\"lastRunSuccess\":true");
    }

    [Fact]
    public void GetSchedule_ReturnsDisabledWhenCrontabIsMissing()
    {
        Environment.SetEnvironmentVariable("CRONTAB_PATH", Path.Combine(_tempDir, "missing-crontab.txt"));
        var controller = new CronLogsController(new Mock<ICronLogger>().Object, new Mock<IBuildJobLogger>().Object);

        var result = controller.GetSchedule();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"enabled\":false");
    }

    [Fact]
    public void LogCronRun_AllowsLoopbackCronRequest()
    {
        var cronLogger = new Mock<ICronLogger>();
        var controller = CreateController(cronLogger, "127.0.0.1");

        var result = controller.LogCronRun(new CronLogRequest
        {
            Message = "EPG generation completed successfully",
            Timestamp = DateTime.UtcNow,
            Success = true
        });

        result.Should().BeOfType<OkObjectResult>();
        cronLogger.Verify(logger => logger.LogCronRun(
            "EPG generation completed successfully",
            It.IsAny<DateTime>(),
            true,
            null), Times.Once);
    }

    [Fact]
    public void LogCronRun_RejectsNonLoopbackAnonymousRequest()
    {
        var cronLogger = new Mock<ICronLogger>();
        var controller = CreateController(cronLogger, "203.0.113.10");

        var result = controller.LogCronRun(new CronLogRequest
        {
            Message = "fake cron run",
            Timestamp = DateTime.UtcNow,
            Success = true
        });

        result.Should().BeOfType<UnauthorizedObjectResult>();
        cronLogger.Verify(logger => logger.LogCronRun(
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<bool>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void GetSchedule_UsesMostRecentManualRebuildWhenNewerThanCronRun()
    {
        var crontabPath = Path.Combine(_tempDir, "crontab.txt");
        File.WriteAllText(crontabPath, "*/20 * * * * /app/cron-wrapper.sh >> /var/log/cron.log 2>&1\n");
        Environment.SetEnvironmentVariable("CRONTAB_PATH", crontabPath);

        var cronLogger = new Mock<ICronLogger>();
        cronLogger
            .Setup(logger => logger.GetLastLogs(1))
            .Returns(new List<CronLogEntry>
            {
                new() { Timestamp = DateTime.UtcNow.AddMinutes(-30), Success = true, Message = "Cron ok" }
            });

        var buildJobLogger = new Mock<IBuildJobLogger>();
        buildJobLogger
            .Setup(logger => logger.GetLastJobs(1))
            .Returns(new List<BuildJobEntry>
            {
                new()
                {
                    StartTime = DateTime.UtcNow.AddMinutes(-5),
                    EndTime = DateTime.UtcNow.AddMinutes(-4),
                    Success = false,
                    Message = "Manual rebuild failed"
                }
            });

        var controller = new CronLogsController(cronLogger.Object, buildJobLogger.Object);

        var result = controller.GetSchedule();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"lastRunSuccess\":false");
        json.Should().Contain("Manual rebuild failed");
    }

    [Fact]
    public void GetSchedule_ComputesNextRunForFixedDailyCron()
    {
        var crontabPath = Path.Combine(_tempDir, "crontab.txt");
        File.WriteAllText(crontabPath, "0 3 * * * /app/cron-wrapper.sh >> /var/log/cron.log 2>&1\n");
        Environment.SetEnvironmentVariable("CRONTAB_PATH", crontabPath);

        var controller = new CronLogsController(new Mock<ICronLogger>().Object, new Mock<IBuildJobLogger>().Object);

        var result = controller.GetSchedule();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"expression\":\"0 3 * * *\"");
        json.Should().NotContain("\"nextRunUtc\":null");
    }

    [Fact]
    public void GetSchedule_ComputesNextRunForSparseAnnualCron()
    {
        var crontabPath = Path.Combine(_tempDir, "crontab.txt");
        File.WriteAllText(crontabPath, "0 0 1 1 * /app/cron-wrapper.sh >> /var/log/cron.log 2>&1\n");
        Environment.SetEnvironmentVariable("CRONTAB_PATH", crontabPath);

        var controller = new CronLogsController(new Mock<ICronLogger>().Object, new Mock<IBuildJobLogger>().Object);

        var result = controller.GetSchedule();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"expression\":\"0 0 1 1 *\"");
        json.Should().NotContain("\"nextRunUtc\":null");
    }

    private static CronLogsController CreateController(Mock<ICronLogger> cronLogger, string remoteIp)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remoteIp);

        return new CronLogsController(cronLogger.Object, new Mock<IBuildJobLogger>().Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = context
            }
        };
    }

    public void Dispose()
    {
        foreach (var (key, value) in _originalEnv)
            Environment.SetEnvironmentVariable(key, value);

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
