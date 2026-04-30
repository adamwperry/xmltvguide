using System.Xml.Linq;
using FluentAssertions;
using xmlTVGuide.Services.BuildJobLogger;
using xmlTVGuide.Services.CronLogger;
using xmlTVGuide.Services.FileServices;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class FileAndLoggerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _originalLogPath = Environment.GetEnvironmentVariable("LOG_PATH");

    public FileAndLoggerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"xmltvguide-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("LOG_PATH", _tempDir);
    }

    [Fact]
    public void xml_file_service_returns_false_for_non_xml_content()
    {
        var service = new XMLFileService();

        var result = service.SaveFile("not xml", Path.Combine(_tempDir, "guide.xml"));

        result.Should().BeFalse();
    }

    [Fact]
    public void xml_file_service_creates_directory_and_writes_document()
    {
        var service = new XMLFileService();
        var outputPath = Path.Combine(_tempDir, "nested", "guide.xml");

        var result = service.SaveFile(new XDocument(new XElement("tv")), outputPath);

        result.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();
    }

    [Fact]
    public void xml_file_service_returns_false_when_output_path_is_directory()
    {
        var service = new XMLFileService();

        var result = service.SaveFile(new XDocument(new XElement("tv")), _tempDir);

        result.Should().BeFalse();
    }

    [Fact]
    public void cron_logger_logs_reads_and_clears_entries()
    {
        var logger = new CronLogger();
        logger.ClearLogs();

        logger.LogCronRun("first", DateTime.UtcNow.AddMinutes(-1), true);
        logger.LogCronRun("second", DateTime.UtcNow, false, "boom");

        var logs = logger.GetLastLogs(10);
        logs.Should().HaveCount(2);
        logs[0].Message.Should().Be("second");
        logs[1].Message.Should().Be("first");

        logger.ClearLogs();
        logger.GetLastLogs().Should().BeEmpty();
    }

    [Fact]
    public void cron_logger_returns_empty_list_for_corrupt_log_file()
    {
        var path = Path.Combine(_tempDir, "cron.log.json");
        File.WriteAllText(path, "{ not valid json");

        var logger = new CronLogger();

        logger.GetLastLogs().Should().BeEmpty();
    }

    [Fact]
    public void build_job_logger_logs_reads_and_clears_entries()
    {
        var logger = new BuildJobLogger();
        logger.ClearLogs();

        logger.LogBuildJob(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow, TimeSpan.FromMinutes(1), true, "first");
        logger.LogBuildJob(DateTime.UtcNow, DateTime.UtcNow, TimeSpan.FromSeconds(30), false, "second", "boom");

        var logs = logger.GetLastJobs(10);
        logs.Should().HaveCount(2);
        logs[0].Message.Should().Be("second");
        logs[0].ErrorMessage.Should().Be("boom");

        logger.ClearLogs();
        logger.GetLastJobs().Should().BeEmpty();
    }

    [Fact]
    public void build_job_logger_returns_empty_list_for_corrupt_log_file()
    {
        var path = Path.Combine(_tempDir, "rebuild.log.json");
        File.WriteAllText(path, "{ not valid json");

        var logger = new BuildJobLogger();

        logger.GetLastJobs().Should().BeEmpty();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LOG_PATH", _originalLogPath);

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
