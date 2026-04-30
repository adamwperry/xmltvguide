using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using xmlTVGuide.Controllers;
using xmlTVGuide.Services.CronLogger;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class CronLogsControllerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Dictionary<string, string?> _originalEnv = new()
    {
        ["CRONTAB_PATH"] = Environment.GetEnvironmentVariable("CRONTAB_PATH")
    };

    public CronLogsControllerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"xmltvguide-cron-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void GetSchedule_ReturnsScheduleAndLastRunMetadata()
    {
        var crontabPath = Path.Combine(_tempDir, "crontab.txt");
        File.WriteAllText(crontabPath, "*/20 * * * * /app/cron-wrapper.sh >> /var/log/cron.log 2>&1\n");
        Environment.SetEnvironmentVariable("CRONTAB_PATH", crontabPath);

        var cronLogger = new Mock<ICronLogger>();
        cronLogger
            .Setup(logger => logger.GetLastLogs(1))
            .Returns(new List<CronLogEntry>
            {
                new() { Timestamp = DateTime.UtcNow.AddMinutes(-5), Success = true, Message = "ok" }
            });

        var controller = new CronLogsController(cronLogger.Object);

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
        var controller = new CronLogsController(new Mock<ICronLogger>().Object);

        var result = controller.GetSchedule();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"enabled\":false");
    }

    public void Dispose()
    {
        foreach (var (key, value) in _originalEnv)
            Environment.SetEnvironmentVariable(key, value);

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
