using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using xmlTVGuide.Controllers;
using xmlTVGuide.Services.AppSettings;
using xmlTVGuide.Services.ChannelMap;
using xmlTVGuide.Services.Validation;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class ConfigControllerTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigControllerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"xmltvguide-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void ValidateJson_WithDuplicateAndBlankChannelIds_ReturnsWarnings()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();

        var request = new SaveFileRequest
        {
            Content = """
            {
              "channels": [
                { "channel": { "name": "A&E NETWORK", "channelId": "21760" } },
                { "channel": { "name": "Duplicate A&E", "channelId": "21760" } },
                { "channel": { "name": "Cartoon Network", "channelId": "" } }
              ]
            }
            """
        };

        var result = controller.ValidateJson(request).Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(result.Value);

        json.Should().Contain("\"valid\":true");
        json.Should().Contain("blank channelId and will be ignored");
        json.Should().Contain("Duplicate channelId values detected");
        json.Should().Contain("\"DuplicateChannelIdCount\":1");
        json.Should().Contain("\"BlankChannelIdCount\":1");
    }

    [Fact]
    public async Task SaveChannelMap_WithWarnings_PersistsFileAndReturnsAnalysis()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();
        var channelMapPath = Path.Combine(_tempDir, "ChannelMap.json");

        var content = """
        {
          "channels": [
            { "channel": { "name": "A&E NETWORK", "channelId": "21760" } },
            { "channel": { "name": "Duplicate A&E", "channelId": "21760" } },
            { "channel": { "name": "Cartoon Network", "channelId": "" } }
          ]
        }
        """;

        var result = await controller.SaveChannelMap(new SaveFileRequest { Content = content });
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);

        File.Exists(channelMapPath).Should().BeTrue();
        File.ReadAllText(channelMapPath).Should().Contain("\"A&E NETWORK\"");
        json.Should().Contain("Channel map saved successfully");
        json.Should().Contain("blank channelId and will be ignored");
        json.Should().Contain("Duplicate channelId values detected");
    }

    [Fact]
    public async Task GetEpgUrls_WhenFileIsMissing_ReturnsEmptyContent()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();

        var result = await controller.GetEpgUrls();
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);

        json.Should().Contain("\"content\":\"\"");
        json.Should().Contain("epg_urls.txt");
    }

    [Fact]
    public async Task SaveEpgUrls_WithInvalidUrls_ReturnsBadRequest()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();

        var result = await controller.SaveEpgUrls(new SaveFileRequest
        {
            Content = "https://valid.example.com\na-local-file.json"
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var json = JsonSerializer.Serialize(badRequest.Value);
        json.Should().Contain("Invalid URLs found");
        json.Should().Contain("a-local-file.json");
    }

    [Fact]
    public async Task SaveEpgUrls_NormalizesLineEndings_AndWritesFile()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();
        var epgUrlsPath = Path.Combine(_tempDir, "epg_urls.txt");

        var result = await controller.SaveEpgUrls(new SaveFileRequest
        {
            Content = "https://one.example.com\r\nhttps://two.example.com\r\n"
        });

        result.Should().BeOfType<OkObjectResult>();
        File.ReadAllText(epgUrlsPath).Should().Be("https://one.example.com\nhttps://two.example.com\n");
    }

    [Fact]
    public async Task SaveEpgUrls_AllowsCommentsAndBlankLines()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();
        var epgUrlsPath = Path.Combine(_tempDir, "epg_urls.txt");

        var content = """
        # Primary EPG source
        https://one.example.com

           # Backup EPG source
        https://two.example.com
        """;

        var result = await controller.SaveEpgUrls(new SaveFileRequest { Content = content });

        result.Should().BeOfType<OkObjectResult>();
        File.ReadAllText(epgUrlsPath).Should().Contain("# Primary EPG source");
        File.ReadAllText(epgUrlsPath).Should().Contain("https://two.example.com");
    }

    [Fact]
    public async Task ExportBackup_ReturnsEpgUrlsAndChannelMap()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();
        File.WriteAllText(Path.Combine(_tempDir, "epg_urls.txt"), "https://one.example.com\n");
        File.WriteAllText(Path.Combine(_tempDir, "ChannelMap.json"), "{\"channels\":[]}");
        File.WriteAllText(Path.Combine(_tempDir, "settings.json"), "{\"channel\":{\"useChannelNamesInsteadOfNumericIds\":true}}");

        var result = await controller.ExportBackup();

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("application/json");
        file.FileDownloadName.Should().StartWith("xmltvguide-config-backup-");

        var json = System.Text.Encoding.UTF8.GetString(file.FileContents);
        json.Should().Contain("https://one.example.com");
        json.Should().Contain("channels");
        json.Should().Contain("useChannelNamesInsteadOfNumericIds");
    }

    [Fact]
    public async Task RestoreBackup_ValidatesAndWritesBothConfigFiles()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();
        var epgUrlsPath = Path.Combine(_tempDir, "epg_urls.txt");
        var channelMapPath = Path.Combine(_tempDir, "ChannelMap.json");

        var result = await controller.RestoreBackup(new ConfigBackup
        {
            EpgUrls = new ConfigBackupFile { Content = "https://restored.example.com\r\n" },
            ChannelMap = new ConfigBackupFile { Content = "{\"channels\":[{\"channel\":{\"name\":\"ABC\",\"channelId\":\"123\"}}]}" },
            Settings = new ConfigBackupFile { Content = "{\"channel\":{\"useChannelNamesInsteadOfNumericIds\":true}}" }
        });

        result.Should().BeOfType<OkObjectResult>();
        File.ReadAllText(epgUrlsPath).Should().Be("https://restored.example.com\n");
        File.ReadAllText(channelMapPath).Should().Contain("\"ABC\"");
        File.ReadAllText(Path.Combine(_tempDir, "settings.json")).Should().Contain("useChannelNamesInsteadOfNumericIds");
    }

    [Fact]
    public async Task RestoreBackup_WithInvalidSettings_DoesNotOverwriteExistingFiles()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();
        var epgUrlsPath = Path.Combine(_tempDir, "epg_urls.txt");
        var channelMapPath = Path.Combine(_tempDir, "ChannelMap.json");
        var settingsPath = Path.Combine(_tempDir, "settings.json");

        File.WriteAllText(epgUrlsPath, "https://existing.example.com\n");
        File.WriteAllText(channelMapPath, "{\"channels\":[]}");
        File.WriteAllText(settingsPath, "{\"channel\":{\"useChannelNamesInsteadOfNumericIds\":false}}");

        var result = await controller.RestoreBackup(new ConfigBackup
        {
            EpgUrls = new ConfigBackupFile { Content = "https://restored.example.com\r\n" },
            ChannelMap = new ConfigBackupFile { Content = "{\"channels\":[{\"channel\":{\"name\":\"ABC\",\"channelId\":\"123\"}}]}" },
            Settings = new ConfigBackupFile { Content = "{\"channel\":" }
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        File.ReadAllText(epgUrlsPath).Should().Be("https://existing.example.com\n");
        File.ReadAllText(channelMapPath).Should().Be("{\"channels\":[]}");
        File.ReadAllText(settingsPath).Should().Contain("\"useChannelNamesInsteadOfNumericIds\":false");
    }

    [Fact]
    public async Task RestoreBackup_RollsBackPreviouslyWrittenFiles_WhenALaterWriteFails()
    {
        var blockedSettingsPath = Path.Combine(_tempDir, "settings-blocked");
        Directory.CreateDirectory(blockedSettingsPath);

        using var env = CreateEnvironmentScope(new Dictionary<string, string?>
        {
            ["SETTINGS_PATH"] = blockedSettingsPath
        });

        var controller = CreateController();
        var epgUrlsPath = Path.Combine(_tempDir, "epg_urls.txt");
        var channelMapPath = Path.Combine(_tempDir, "ChannelMap.json");

        File.WriteAllText(epgUrlsPath, "https://existing.example.com\n");
        File.WriteAllText(channelMapPath, "{\"channels\":[]}");

        var result = await controller.RestoreBackup(new ConfigBackup
        {
            EpgUrls = new ConfigBackupFile { Content = "https://restored.example.com\r\n" },
            ChannelMap = new ConfigBackupFile { Content = "{\"channels\":[{\"channel\":{\"name\":\"ABC\",\"channelId\":\"123\"}}]}" },
            Settings = new ConfigBackupFile { Content = "{\"channel\":{\"useChannelNamesInsteadOfNumericIds\":true}}" }
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        File.ReadAllText(epgUrlsPath).Should().Be("https://existing.example.com\n");
        File.ReadAllText(channelMapPath).Should().Be("{\"channels\":[]}");
    }

    [Fact]
    public async Task GetSettings_WhenFileIsMissing_ReturnsDefaultSettings()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();

        var result = await controller.GetSettings();
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);

        json.Should().Contain("\"UseChannelNamesInsteadOfNumericIds\":false");
        json.Should().Contain("\"SortChannelsByIdThenDisplayName\":true");
        json.Should().Contain("settings.json");
    }

    [Fact]
    public async Task SaveSettings_PersistsSettingsJson()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();

        var result = await controller.SaveSettings(new AppSettings
        {
            Channel = new ChannelOutputSettings { UseChannelNamesInsteadOfNumericIds = true }
        });

        result.Should().BeOfType<OkObjectResult>();
        File.ReadAllText(Path.Combine(_tempDir, "settings.json"))
            .Should()
            .Contain("\"useChannelNamesInsteadOfNumericIds\": true");
        File.ReadAllText(Path.Combine(_tempDir, "settings.json"))
            .Should()
            .Contain("\"sortChannelsByIdThenDisplayName\": true");
    }

    [Fact]
    public async Task GetSettings_AppliesDockerEnvironmentOverrides()
    {
        using var env = CreateEnvironmentScope(new Dictionary<string, string?>
        {
            ["USE_CHANNEL_NAMES_INSTEAD_OF_NUMERIC_IDS"] = "true",
            ["SORT_CHANNELS_BY_ID"] = "false"
        });
        File.WriteAllText(Path.Combine(_tempDir, "settings.json"), """
        {
          "channel": {
            "useChannelNamesInsteadOfNumericIds": false,
            "sortChannelsByIdThenDisplayName": true
          }
        }
        """);
        var controller = CreateController();

        var result = await controller.GetSettings();
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);

        json.Should().Contain("\"UseChannelNamesInsteadOfNumericIds\":true");
        json.Should().Contain("\"SortChannelsByIdThenDisplayName\":false");
    }

    [Fact]
    public async Task GetChannelMap_WhenFileIsMissing_ReturnsDefaultContent()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();

        var result = await controller.GetChannelMap();
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);

        json.Should().Contain("{\\u0022channels\\u0022: []}");
    }

    [Fact]
    public async Task SaveChannelMap_WithInvalidJson_ReturnsBadRequest()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();

        var result = await controller.SaveChannelMap(new SaveFileRequest
        {
            Content = "{ invalid json"
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var json = JsonSerializer.Serialize(badRequest.Value);
        json.Should().Contain("Invalid JSON format");
    }

    [Fact]
    public void ValidateJson_WithInvalidJson_ReturnsValidFalse()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();

        var result = controller.ValidateJson(new SaveFileRequest
        {
            Content = "{ invalid json"
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"valid\":false");
    }

    [Fact]
    public void GetStatus_ReflectsCurrentFiles()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();
        File.WriteAllText(Path.Combine(_tempDir, "epg_urls.txt"), "https://example.com");
        File.WriteAllText(Path.Combine(_tempDir, "ChannelMap.json"), "{\"channels\":[]}");

        var result = controller.GetStatus();
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);

        json.Should().Contain("\"epgUrlsExists\":true");
        json.Should().Contain("\"channelMapExists\":true");
    }

    [Fact]
    public async Task TestSource_WithBlankUrl_ReturnsBadRequest()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();

        var result = await controller.TestSource(new TestSourceRequest { Url = " " });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TestSource_DelegatesToValidationService()
    {
        using var env = CreateEnvironmentScope();
        var validationService = new Mock<IValidationService>();
        validationService
            .Setup(service => service.TestSourceAsync("https://example.com/epg"))
            .ReturnsAsync(new SourceTestResult { Success = true, Message = "ok" });

        var controller = new ConfigController(validationService.Object, new ChannelMapLoader(), new FileAppSettingsService());

        var result = await controller.TestSource(new TestSourceRequest { Url = "https://example.com/epg" });
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);

        json.Should().Contain("\"Success\":true");
        validationService.Verify(service => service.TestSourceAsync("https://example.com/epg"), Times.Once);
    }

    [Fact]
    public async Task PreviewChannels_WithBlankUrl_ReturnsBadRequest()
    {
        using var env = CreateEnvironmentScope();
        var controller = CreateController();

        var result = await controller.PreviewChannels(new PreviewChannelsRequest { Url = " " });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PreviewChannels_UsesCurrentMap_WhenChannelMapExists()
    {
        using var env = CreateEnvironmentScope();
        var channelMapPath = Path.Combine(_tempDir, "ChannelMap.json");
        File.WriteAllText(channelMapPath, "{\"channels\":[]}");

        var validationService = new Mock<IValidationService>();
        validationService
            .Setup(service => service.PreviewChannelsAsync("https://example.com/epg", channelMapPath))
            .ReturnsAsync(new ChannelPreviewResult { Success = true, Message = "preview" });

        var controller = new ConfigController(validationService.Object, new ChannelMapLoader(), new FileAppSettingsService());

        await controller.PreviewChannels(new PreviewChannelsRequest { Url = "https://example.com/epg", UseCurrentMap = true });

        validationService.Verify(service => service.PreviewChannelsAsync("https://example.com/epg", channelMapPath), Times.Once);
    }

    [Fact]
    public async Task PreviewChannels_PassesNullMap_WhenCurrentMapIsMissing()
    {
        using var env = CreateEnvironmentScope();
        var validationService = new Mock<IValidationService>();
        validationService
            .Setup(service => service.PreviewChannelsAsync("https://example.com/epg", null))
            .ReturnsAsync(new ChannelPreviewResult { Success = true, Message = "preview" });

        var controller = new ConfigController(validationService.Object, new ChannelMapLoader(), new FileAppSettingsService());

        await controller.PreviewChannels(new PreviewChannelsRequest { Url = "https://example.com/epg", UseCurrentMap = true });

        validationService.Verify(service => service.PreviewChannelsAsync("https://example.com/epg", null), Times.Once);
    }

    private ConfigController CreateController()
    {
        var validationService = new Mock<IValidationService>();
        return new ConfigController(validationService.Object, new ChannelMapLoader(), new FileAppSettingsService());
    }

    private EnvironmentVariableScope CreateEnvironmentScope(IDictionary<string, string?>? additionalUpdates = null)
    {
        var updates = new Dictionary<string, string?>
        {
            ["CHANNEL_MAP_PATH"] = Path.Combine(_tempDir, "ChannelMap.json"),
            ["EPG_URL_FILES"] = Path.Combine(_tempDir, "epg_urls.txt"),
            ["SETTINGS_PATH"] = Path.Combine(_tempDir, "settings.json")
        };

        if (additionalUpdates is not null)
        {
            foreach (var update in additionalUpdates)
                updates[update.Key] = update.Value;
        }

        return new EnvironmentVariableScope(updates);
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
