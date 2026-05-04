using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using xmlTVGuide.Controllers;
using xmlTVGuide.Services;
using xmlTVGuide.Services.AppSettings;
using xmlTVGuide.Services.BackgroundJobs;
using xmlTVGuide.Services.CronLogger;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class GuideControllerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Dictionary<string, string?> _originalEnv = new()
    {
        ["OUTPUT_PATH"] = Environment.GetEnvironmentVariable("OUTPUT_PATH"),
        ["CHANNEL_MAP_PATH"] = Environment.GetEnvironmentVariable("CHANNEL_MAP_PATH"),
        ["EPG_URL_FILES"] = Environment.GetEnvironmentVariable("EPG_URL_FILES"),
        ["SETTINGS_PATH"] = Environment.GetEnvironmentVariable("SETTINGS_PATH")
    };

    public GuideControllerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"xmltvguide-guide-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void get_guide_xml_returns_not_found_when_file_missing()
    {
        Environment.SetEnvironmentVariable("OUTPUT_PATH", Path.Combine(_tempDir, "missing.xml"));
        var controller = CreateController();

        var result = controller.GetGuideXml();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void get_guide_xml_returns_physical_file_when_present()
    {
        var outputPath = Path.Combine(_tempDir, "guide.xml");
        File.WriteAllText(outputPath, "<tv></tv>");
        Environment.SetEnvironmentVariable("OUTPUT_PATH", outputPath);

        var controller = CreateController();
        var result = controller.GetGuideXml().Should().BeOfType<PhysicalFileResult>().Subject;

        result.FileName.Should().Be(outputPath);
        result.ContentType.Should().Be("application/xml");
    }

    [Fact]
    public void get_guide_xml_allows_anonymous_access_for_emby()
    {
        var method = typeof(GuideController).GetMethod(nameof(GuideController.GetGuideXml));

        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: false)
            .Should()
            .ContainSingle();
    }

    [Fact]
    public void get_status_returns_file_metadata()
    {
        var outputPath = Path.Combine(_tempDir, "guide.xml");
        File.WriteAllText(outputPath, "<tv></tv>");
        Environment.SetEnvironmentVariable("OUTPUT_PATH", outputPath);

        var controller = CreateController();
        var result = controller.GetGuideStatus().Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(result.Value);

        json.Should().Contain("\"guideExists\":true");
        json.Should().Contain("\"fileSize\":9");
    }

    [Fact]
    public async Task rebuild_returns_bad_request_when_epg_urls_missing()
    {
        Environment.SetEnvironmentVariable("EPG_URL_FILES", Path.Combine(_tempDir, "missing-epg.txt"));
        Environment.SetEnvironmentVariable("CHANNEL_MAP_PATH", Path.Combine(_tempDir, "ChannelMap.json"));

        var result = await CreateController().RebuildGuide();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task rebuild_returns_bad_request_when_channel_map_missing()
    {
        var epgPath = Path.Combine(_tempDir, "epg_urls.txt");
        File.WriteAllText(epgPath, "https://example.com/epg");
        Environment.SetEnvironmentVariable("EPG_URL_FILES", epgPath);
        Environment.SetEnvironmentVariable("CHANNEL_MAP_PATH", Path.Combine(_tempDir, "missing-map.json"));

        var result = await CreateController().RebuildGuide();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task rebuild_returns_conflict_when_background_job_is_running()
    {
        var epgPath = Path.Combine(_tempDir, "epg_urls.txt");
        var channelMapPath = Path.Combine(_tempDir, "ChannelMap.json");
        var outputPath = Path.Combine(_tempDir, "guide.xml");
        File.WriteAllText(epgPath, "https://example.com/epg");
        File.WriteAllText(channelMapPath, "{\"channels\":[]}");
        SetPaths(outputPath, channelMapPath, epgPath);

        var backgroundJobs = new Mock<IBackgroundJobService>();
        backgroundJobs
            .Setup(service => service.TryStartJobAsync(It.IsAny<Func<CancellationToken, Task>>(), "EPG Rebuild"))
            .ReturnsAsync((false, "A job is already running."));

        var result = await CreateController(backgroundJobs: backgroundJobs).RebuildGuide();
        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;

        JsonSerializer.Serialize(conflict.Value).Should().Contain("already running");
    }

    [Fact]
    public async Task rebuild_returns_accepted_when_background_job_starts()
    {
        var epgPath = Path.Combine(_tempDir, "epg_urls.txt");
        var channelMapPath = Path.Combine(_tempDir, "ChannelMap.json");
        var outputPath = Path.Combine(_tempDir, "guide.xml");
        File.WriteAllText(epgPath, "https://example.com/epg");
        File.WriteAllText(channelMapPath, "{\"channels\":[]}");
        SetPaths(outputPath, channelMapPath, epgPath);

        var backgroundJobs = new Mock<IBackgroundJobService>();
        backgroundJobs
            .Setup(service => service.TryStartJobAsync(It.IsAny<Func<CancellationToken, Task>>(), "EPG Rebuild"))
            .ReturnsAsync((true, "EPG Rebuild started successfully"));

        var result = await CreateController(backgroundJobs: backgroundJobs).RebuildGuide();
        var accepted = result.Should().BeOfType<AcceptedResult>().Subject;

        JsonSerializer.Serialize(accepted.Value).Should().Contain("started successfully");
    }

    [Fact]
    public async Task rebuild_passes_strip_channel_numbers_option_to_generation_service()
    {
        var epgPath = Path.Combine(_tempDir, "epg_urls.txt");
        var channelMapPath = Path.Combine(_tempDir, "ChannelMap.json");
        var outputPath = Path.Combine(_tempDir, "guide.xml");
        File.WriteAllText(epgPath, "https://example.com/epg");
        File.WriteAllText(channelMapPath, "{\"channels\":[]}");
        SetPaths(outputPath, channelMapPath, epgPath);

        Func<CancellationToken, Task>? rebuildJob = null;
        var backgroundJobs = new Mock<IBackgroundJobService>();
        backgroundJobs
            .Setup(service => service.TryStartJobAsync(It.IsAny<Func<CancellationToken, Task>>(), "EPG Rebuild"))
            .Callback<Func<CancellationToken, Task>, string>((job, _) => rebuildJob = job)
            .ReturnsAsync((true, "EPG Rebuild started successfully"));

        var generationService = new Mock<IEpgGenerationService>();
        generationService
            .Setup(service => service.GenerateAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpgGenerationResult { Success = true, Message = "ok" });

        var result = await CreateController(generationService: generationService, backgroundJobs: backgroundJobs)
            .RebuildGuide(new RebuildGuideRequest { StripChannelNumbers = true });

        result.Should().BeOfType<AcceptedResult>();
        rebuildJob.Should().NotBeNull();
        await rebuildJob!(CancellationToken.None);

        generationService.Verify(service => service.GenerateAsync(It.Is<string[]>(args =>
            args.Contains("--strip-channel-numbers")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task rebuild_uses_persisted_settings_when_request_does_not_override()
    {
        var epgPath = Path.Combine(_tempDir, "epg_urls.txt");
        var channelMapPath = Path.Combine(_tempDir, "ChannelMap.json");
        var outputPath = Path.Combine(_tempDir, "guide.xml");
        File.WriteAllText(epgPath, "https://example.com/epg");
        File.WriteAllText(channelMapPath, "{\"channels\":[]}");
        SetPaths(outputPath, channelMapPath, epgPath);
        File.WriteAllText(
            Path.Combine(_tempDir, "settings.json"),
            "{\"channel\":{\"useChannelNamesInsteadOfNumericIds\":true}}");

        Func<CancellationToken, Task>? rebuildJob = null;
        var backgroundJobs = new Mock<IBackgroundJobService>();
        backgroundJobs
            .Setup(service => service.TryStartJobAsync(It.IsAny<Func<CancellationToken, Task>>(), "EPG Rebuild"))
            .Callback<Func<CancellationToken, Task>, string>((job, _) => rebuildJob = job)
            .ReturnsAsync((true, "EPG Rebuild started successfully"));

        var generationService = new Mock<IEpgGenerationService>();
        generationService
            .Setup(service => service.GenerateAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpgGenerationResult { Success = true, Message = "ok" });

        var result = await CreateController(generationService: generationService, backgroundJobs: backgroundJobs)
            .RebuildGuide();

        result.Should().BeOfType<AcceptedResult>();
        rebuildJob.Should().NotBeNull();
        await rebuildJob!(CancellationToken.None);

        generationService.Verify(service => service.GenerateAsync(It.Is<string[]>(args =>
            args.Contains("--strip-channel-numbers")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task rebuild_passes_preserve_channel_order_when_sort_setting_is_disabled()
    {
        var epgPath = Path.Combine(_tempDir, "epg_urls.txt");
        var channelMapPath = Path.Combine(_tempDir, "ChannelMap.json");
        var outputPath = Path.Combine(_tempDir, "guide.xml");
        File.WriteAllText(epgPath, "https://example.com/epg");
        File.WriteAllText(channelMapPath, "{\"channels\":[]}");
        SetPaths(outputPath, channelMapPath, epgPath);
        File.WriteAllText(
            Path.Combine(_tempDir, "settings.json"),
            "{\"channel\":{\"sortChannelsByIdThenDisplayName\":false}}");

        Func<CancellationToken, Task>? rebuildJob = null;
        var backgroundJobs = new Mock<IBackgroundJobService>();
        backgroundJobs
            .Setup(service => service.TryStartJobAsync(It.IsAny<Func<CancellationToken, Task>>(), "EPG Rebuild"))
            .Callback<Func<CancellationToken, Task>, string>((job, _) => rebuildJob = job)
            .ReturnsAsync((true, "EPG Rebuild started successfully"));

        var generationService = new Mock<IEpgGenerationService>();
        generationService
            .Setup(service => service.GenerateAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpgGenerationResult { Success = true, Message = "ok" });

        var result = await CreateController(generationService: generationService, backgroundJobs: backgroundJobs)
            .RebuildGuide();

        result.Should().BeOfType<AcceptedResult>();
        rebuildJob.Should().NotBeNull();
        await rebuildJob!(CancellationToken.None);

        generationService.Verify(service => service.GenerateAsync(It.Is<string[]>(args =>
            args.Contains("--preserve-channel-order")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task rebuild_returns_500_when_background_service_throws()
    {
        var epgPath = Path.Combine(_tempDir, "epg_urls.txt");
        var channelMapPath = Path.Combine(_tempDir, "ChannelMap.json");
        var outputPath = Path.Combine(_tempDir, "guide.xml");
        File.WriteAllText(epgPath, "https://example.com/epg");
        File.WriteAllText(channelMapPath, "{\"channels\":[]}");
        SetPaths(outputPath, channelMapPath, epgPath);

        var backgroundJobs = new Mock<IBackgroundJobService>();
        backgroundJobs
            .Setup(service => service.TryStartJobAsync(It.IsAny<Func<CancellationToken, Task>>(), "EPG Rebuild"))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await CreateController(backgroundJobs: backgroundJobs).RebuildGuide();
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;

        objectResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public void rebuild_status_and_history_return_background_job_state()
    {
        var backgroundJobs = new Mock<IBackgroundJobService>();
        backgroundJobs.Setup(service => service.GetCurrentStatus()).Returns(new JobStatus
        {
            IsRunning = true,
            CurrentMessage = "Running"
        });
        backgroundJobs.Setup(service => service.GetHistory(200)).Returns(new List<JobHistoryEntry>
        {
            new() { Message = "done", Success = true }
        });

        var controller = CreateController(backgroundJobs: backgroundJobs);

        controller.GetRebuildStatus().Should().BeOfType<OkObjectResult>();
        var historyResult = controller.GetRebuildHistory(250).Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(historyResult.Value);
        json.Should().Contain("\"count\":1");
        backgroundJobs.Verify(service => service.GetHistory(200), Times.Once);
    }

    [Fact]
    public void cancel_rebuild_sends_cancellation_request()
    {
        var backgroundJobs = new Mock<IBackgroundJobService>();
        var controller = CreateController(backgroundJobs: backgroundJobs);

        var result = controller.CancelRebuild();

        result.Should().BeOfType<OkObjectResult>();
        backgroundJobs.Verify(service => service.CancelCurrent(), Times.Once);
    }

    [Fact]
    public void clear_rebuild_history_clears_background_job_history()
    {
        var backgroundJobs = new Mock<IBackgroundJobService>();
        var controller = CreateController(backgroundJobs: backgroundJobs);

        var result = controller.ClearRebuildHistory();

        result.Should().BeOfType<OkObjectResult>();
        backgroundJobs.Verify(service => service.ClearHistory(), Times.Once);
    }

    private GuideController CreateController(
        Mock<ICronLogger>? cronLogger = null,
        Mock<IEpgGenerationService>? generationService = null,
        Mock<IBackgroundJobService>? backgroundJobs = null)
    {
        return new GuideController(
            (cronLogger ?? new Mock<ICronLogger>()).Object,
            (generationService ?? new Mock<IEpgGenerationService>()).Object,
            (backgroundJobs ?? new Mock<IBackgroundJobService>()).Object,
            new FileAppSettingsService());
    }

    private void SetPaths(string outputPath, string channelMapPath, string epgPath)
    {
        Environment.SetEnvironmentVariable("OUTPUT_PATH", outputPath);
        Environment.SetEnvironmentVariable("CHANNEL_MAP_PATH", channelMapPath);
        Environment.SetEnvironmentVariable("EPG_URL_FILES", epgPath);
        Environment.SetEnvironmentVariable("SETTINGS_PATH", Path.Combine(_tempDir, "settings.json"));
    }

    public void Dispose()
    {
        foreach (var (key, value) in _originalEnv)
            Environment.SetEnvironmentVariable(key, value);

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
