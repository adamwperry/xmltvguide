using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using xmlTVGuide;
using xmlTVGuide.Controllers;
using xmlTVGuide.Services;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class OperationalRegressionTests
{
    [Fact]
    public async Task RunEpgGenerationForWeb_WithHelp_DoesNotCrashWhenStatusTrackerIsRegistered()
    {
        var result = await Program.RunEpgGenerationForWeb(new[] { "--help" });

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Usage:");
    }

    [Fact]
    public void GetHealth_DoesNotLeakSourceUrlsOrDetailedErrors()
    {
        using var tempDir = new TempDirectory();
        var outputPath = Path.Combine(tempDir.Path, "guide.xml");
        var channelMapPath = Path.Combine(tempDir.Path, "ChannelMap.json");
        var epgUrlsPath = Path.Combine(tempDir.Path, "epg_urls.txt");

        File.WriteAllText(outputPath, "<tv></tv>");
        File.WriteAllText(channelMapPath, "{}");
        File.WriteAllText(epgUrlsPath, "https://example.com/guide?token=super-secret");

        var tracker = new InMemoryEpgGenerationStatusTracker();
        tracker.UpdateStatus(new EpgGenerationStatus
        {
            HasRecordedRun = true,
            LastRunAt = DateTime.UtcNow,
            LastRunSuccess = false,
            LastRunMessage = "Fetch failed",
            TotalSources = 1,
            SuccessfulSources = 0,
            SourceResults =
            {
                new SourceFetchStatus
                {
                    Url = "https://example.com/guide?token=super-secret",
                    Success = false,
                    ErrorMessage = "401 from upstream with token super-secret",
                    HttpStatusCode = 401,
                    ResponseTimeMs = 50,
                    ResponseSizeBytes = 0,
                    FetchedAt = DateTime.UtcNow
                }
            },
            WarningDetails = { "warning with secret token super-secret" },
            ErrorDetails = { "error with secret token super-secret" }
        });

        using var env = new EnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["OUTPUT_PATH"] = outputPath,
            ["CHANNEL_MAP_PATH"] = channelMapPath,
            ["EPG_URL_FILES"] = epgUrlsPath
        });

        var controller = new HealthController(tracker, NullLogger<HealthController>.Instance);

        var result = controller.GetHealth().Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(result.Value);

        json.Should().NotContain("super-secret");
        json.Should().NotContain("sourceDetails");
        json.Should().NotContain("\"warnings\":");
        json.Should().NotContain("\"errors\":");
        json.Should().Contain("\"warningCount\":1");
        json.Should().Contain("\"errorCount\":1");
    }

    [Fact]
    public void GetHealth_WithStaleGuideAndNoRunHistory_ReturnsDegradedInsteadOfUnhealthy()
    {
        using var tempDir = new TempDirectory();
        var outputPath = Path.Combine(tempDir.Path, "guide.xml");
        var channelMapPath = Path.Combine(tempDir.Path, "ChannelMap.json");

        File.WriteAllText(outputPath, "<tv></tv>");
        File.SetLastWriteTimeUtc(outputPath, DateTime.UtcNow.AddHours(-3));
        File.WriteAllText(channelMapPath, "{}");

        using var env = new EnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["OUTPUT_PATH"] = outputPath,
            ["CHANNEL_MAP_PATH"] = channelMapPath,
            ["EPG_URL_FILES"] = Path.Combine(tempDir.Path, "epg_urls.txt")
        });

        var controller = new HealthController(new InMemoryEpgGenerationStatusTracker(), NullLogger<HealthController>.Instance);

        var result = controller.GetHealth().Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(result.Value);

        json.Should().Contain("\"status\":\"degraded\"");
        json.Should().NotContain("\"status\":\"unhealthy\"");
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
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
