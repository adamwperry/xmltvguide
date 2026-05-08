using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using xmlTVGuide.Controllers;
using xmlTVGuide.Services;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class HealthControllerTests : IDisposable
{
    private readonly string _tempDir;

    public HealthControllerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"xmltvguide-health-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void GetHealth_WithMissingGuideAndSuccessfulLastRun_ReturnsUnhealthy()
    {
        var channelMapPath = WriteFile("ChannelMap.json", "{}");
        var epgUrlsPath = WriteFile("epg_urls.txt", "https://example.com/epg");
        using var env = CreateEnvironmentScope(null, channelMapPath, epgUrlsPath);

        var tracker = new InMemoryEpgGenerationStatusTracker();
        tracker.UpdateStatus(new EpgGenerationStatus
        {
            HasRecordedRun = true,
            LastRunSuccess = true,
            LastRunMessage = "Completed"
        });

        var result = CreateController(tracker).GetHealth().Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(result.Value);

        json.Should().Contain("\"status\":\"unhealthy\"");
        json.Should().Contain("last generation succeeded but file missing");
    }

    [Fact]
    public void GetHealth_WithStaleGuideAndFailedRun_ReturnsUnhealthy()
    {
        var outputPath = WriteFile("guide.xml", "<tv></tv>");
        File.SetLastWriteTimeUtc(outputPath, DateTime.UtcNow.AddHours(-3));
        var channelMapPath = WriteFile("ChannelMap.json", "{}");
        var epgUrlsPath = WriteFile("epg_urls.txt", "https://example.com/epg");
        using var env = CreateEnvironmentScope(outputPath, channelMapPath, epgUrlsPath);

        var tracker = new InMemoryEpgGenerationStatusTracker();
        tracker.UpdateStatus(new EpgGenerationStatus
        {
            HasRecordedRun = true,
            LastRunSuccess = false,
            LastRunMessage = "Upstream fetch failed"
        });

        var result = CreateController(tracker).GetHealth().Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(result.Value);

        json.Should().Contain("\"status\":\"unhealthy\"");
        json.Should().Contain("generation is failing");
    }

    [Fact]
    public void GetHealth_WithPartialSourceFailures_ReturnsDegraded()
    {
        var outputPath = WriteFile("guide.xml", "<tv></tv>");
        var channelMapPath = WriteFile("ChannelMap.json", "{}");
        var epgUrlsPath = WriteFile("epg_urls.txt", "https://example.com/epg");
        using var env = CreateEnvironmentScope(outputPath, channelMapPath, epgUrlsPath);

        var tracker = new InMemoryEpgGenerationStatusTracker();
        tracker.UpdateStatus(new EpgGenerationStatus
        {
            HasRecordedRun = true,
            LastRunAt = DateTime.UtcNow,
            LastRunSuccess = true,
            TotalSources = 3,
            SuccessfulSources = 2
        });

        var result = CreateController(tracker).GetHealth().Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(result.Value);

        json.Should().Contain("\"status\":\"degraded\"");
        json.Should().Contain("Only 2/3 EPG source(s) succeeded");
    }

    [Fact]
    public void GetHealth_WithFreshGuideAndSuccessfulRun_ReturnsHealthy()
    {
        var outputPath = WriteFile("guide.xml", "<tv></tv>");
        var channelMapPath = WriteFile("ChannelMap.json", "{}");
        var epgUrlsPath = WriteFile("epg_urls.txt", "https://example.com/epg");
        using var env = CreateEnvironmentScope(outputPath, channelMapPath, epgUrlsPath);

        var tracker = new InMemoryEpgGenerationStatusTracker();
        tracker.UpdateStatus(new EpgGenerationStatus
        {
            HasRecordedRun = true,
            LastRunAt = DateTime.UtcNow,
            LastRunSuccess = true,
            LastRunMessage = "Completed"
        });

        var result = CreateController(tracker).GetHealth().Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(result.Value);

        json.Should().Contain("\"status\":\"healthy\"");
        json.Should().Contain("Guide present and up-to-date");
    }

    [Fact]
    public void GetReadiness_ReturnsServiceUnavailable_WhenChannelMapMissing()
    {
        var outputPath = WriteFile("guide.xml", "<tv></tv>");
        var epgUrlsPath = WriteFile("epg_urls.txt", "https://example.com/epg");
        var missingChannelMap = Path.Combine(_tempDir, "missing-ChannelMap.json");
        using var env = CreateEnvironmentScope(outputPath, missingChannelMap, epgUrlsPath);

        var result = CreateController(new InMemoryEpgGenerationStatusTracker()).GetReadiness();

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public void GetLiveness_ReturnsAlive()
    {
        var result = CreateController(new InMemoryEpgGenerationStatusTracker()).GetLiveness().Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(result.Value);

        json.Should().Contain("\"status\":\"alive\"");
    }

    private HealthController CreateController(IEpgGenerationStatusTracker tracker)
    {
        return new HealthController(tracker, NullLogger<HealthController>.Instance);
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private EnvironmentVariableScope CreateEnvironmentScope(string? outputPath, string channelMapPath, string epgUrlsPath)
    {
        return new EnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["OUTPUT_PATH"] = outputPath,
            ["CHANNEL_MAP_PATH"] = channelMapPath,
            ["EPG_URL_FILES"] = epgUrlsPath
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new();

        public EnvironmentVariableScope(IDictionary<string, string?> updates)
        {
            foreach (var entry in updates)
            {
                _originalValues[entry.Key] = Environment.GetEnvironmentVariable(entry.Key);
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }
        }

        public void Dispose()
        {
            foreach (var entry in _originalValues)
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        }
    }
}
