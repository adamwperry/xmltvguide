using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using xmlTVGuide.Models;
using xmlTVGuide.Services;
using xmlTVGuide.Services.ChannelMap;
using xmlTVGuide.Services.Validation;
using xmlTVGuide.Services.XMXTVBuilder.Parsers;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class ValidationServiceTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly string _tempDir;

    public ValidationServiceTests()
    {
        _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
        _tempDir = Path.Combine(Path.GetTempPath(), $"xmltvguide-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task TestSourceAsync_WithBlankUrl_ReturnsValidationMessage()
    {
        var service = CreatePreviewValidationService();

        var result = await service.TestSourceAsync(" ");

        result.Success.Should().BeFalse();
        result.Message.Should().Be("URL cannot be empty");
    }

    [Fact]
    public async Task TestSourceAsync_WithReachableValidJsonAndMatchingParser_ReturnsSuccess()
    {
        using var server = await LoopbackServer.StartAsync();

        var fetcher = new Mock<IDataFetcher>();
        fetcher.Setup(service => service.FetchDataWithResultAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FetchResult
            {
                Success = true,
                Data = """{ "channels": [] }"""
            });

        var parser = new StubGuideParser(canParse: true);
        var service = CreateValidationService(fetcher.Object, new[] { parser });

        var result = await service.TestSourceAsync($"{server.Url}?ts={{unixtime}}&ym={{monthyear}}&legacy={{yearmonth}}");

        result.Success.Should().BeTrue();
        result.Reachability.IsReachable.Should().BeTrue();
        result.Format.IsValidJson.Should().BeTrue();
        result.Format.HasRecognizedStructure.Should().BeTrue();
        result.SupportedParsers.Should().Contain(nameof(StubGuideParser));
        result.Format.RecognizedFormats.Should().Contain(nameof(StubGuideParser));
        result.Message.Should().Contain(nameof(StubGuideParser));
    }

    [Fact]
    public async Task TestSourceAsync_WithReachableSourceAndInvalidJson_ReturnsFormatError()
    {
        using var server = await LoopbackServer.StartAsync();

        var fetcher = new Mock<IDataFetcher>();
        fetcher.Setup(service => service.FetchDataWithResultAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FetchResult
            {
                Success = true,
                Data = "not json"
            });

        var service = CreateValidationService(fetcher.Object, new[] { new StubGuideParser(canParse: true) });
        var result = await service.TestSourceAsync(server.Url);

        result.Success.Should().BeFalse();
        result.Reachability.IsReachable.Should().BeTrue();
        result.Format.IsValidJson.Should().BeFalse();
        result.Message.Should().Be("Response is not valid JSON");
    }

    [Fact]
    public async Task TestSourceAsync_WithReachableSourceAndNoMatchingParser_ReturnsUnrecognizedMessage()
    {
        using var server = await LoopbackServer.StartAsync();

        var fetcher = new Mock<IDataFetcher>();
        fetcher.Setup(service => service.FetchDataWithResultAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FetchResult
            {
                Success = true,
                Data = """{ "channels": [] }"""
            });

        var service = CreateValidationService(fetcher.Object, new[] { new StubGuideParser(canParse: false) });
        var result = await service.TestSourceAsync(server.Url);

        result.Success.Should().BeFalse();
        result.Format.IsValidJson.Should().BeTrue();
        result.Format.HasRecognizedStructure.Should().BeFalse();
        result.Message.Should().Be("Source format is not recognized by any parser");
    }

    [Fact]
    public async Task TestSourceAsync_WithHumanVerificationHtml_ReturnsSpecificMessage()
    {
        using var server = await LoopbackServer.StartAsync();

        var fetcher = new Mock<IDataFetcher>();
        fetcher.Setup(service => service.FetchDataWithResultAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FetchResult
            {
                Success = false,
                ErrorMessage = "Source returned an HTML human-verification page instead of JSON data."
            });

        var service = CreateValidationService(fetcher.Object, new[] { new StubGuideParser(canParse: true) });
        var result = await service.TestSourceAsync(server.Url);

        result.Success.Should().BeFalse();
        result.Reachability.IsReachable.Should().BeTrue();
        result.Message.Should().Be("Source returned an HTML human-verification page instead of JSON data.");
    }

    [Fact]
    public async Task TestSourceAsync_WithUnreachableSource_ReturnsReachabilityFailure()
    {
        var service = CreateValidationService(new Mock<IDataFetcher>().Object, new[] { new StubGuideParser(canParse: true) });
        var unusedPort = GetUnusedLocalPort();

        var result = await service.TestSourceAsync($"http://127.0.0.1:{unusedPort}/epg");

        result.Success.Should().BeFalse();
        result.Reachability.IsReachable.Should().BeFalse();
        result.Message.Should().StartWith("Source is not reachable:");
    }

    [Fact]
    public async Task PreviewChannelsAsync_GuideTwo_UsesChannelMapAndReturnsMappedChannels()
    {
        var epgPath = Path.Combine(_testDataPath, "valid-guidetwo-epg.json");
        var mapPath = Path.Combine(_tempDir, "channel-map.json");

        await File.WriteAllTextAsync(mapPath, """
        {
          "channels": [
            { "channel": { "name": "Custom AWP", "channelId": "12345" } },
            { "channel": { "name": "Custom WIP", "channelId": "67890" } }
          ]
        }
        """);

        var service = CreatePreviewValidationService();

        var result = await service.PreviewChannelsAsync(epgPath, mapPath);

        result.Success.Should().BeTrue();
        result.TotalChannels.Should().Be(2);
        result.MappedChannels.Should().Be(2);
        result.UnmappedChannels.Should().Be(0);
        result.DetectedChannels.Should().ContainSingle(c =>
            c.Id == "12345" &&
            c.DisplayName == "Custom AWP" &&
            c.MappedName == "Custom AWP" &&
            c.ProgramCount == 2 &&
            c.Sources.Count == 1 &&
            c.Sources[0] == epgPath);
        result.DetectedChannels.Should().ContainSingle(c =>
            c.Id == "67890" &&
            c.DisplayName == "Custom WIP" &&
            c.MappedName == "Custom WIP" &&
            c.ProgramCount == 1 &&
            c.Sources.Count == 1 &&
            c.Sources[0] == epgPath);
    }

    [Fact]
    public async Task PreviewChannelsAsync_GuideThree_UsesChannelMapAndReturnsMappedChannels()
    {
        var epgPath = Path.Combine(_testDataPath, "valid-guidethree-epg.json");
        var mapPath = Path.Combine(_tempDir, "channel-map-guide3.json");

        await File.WriteAllTextAsync(mapPath, """
        {
          "channels": [
            { "channel": { "name": "Morning Feed", "channelId": "awp-1" } },
            { "channel": { "name": "Sports Feed", "channelId": "wip-2" } }
          ]
        }
        """);

        var service = CreatePreviewValidationService();

        var result = await service.PreviewChannelsAsync(epgPath, mapPath);

        result.Success.Should().BeTrue();
        result.TotalChannels.Should().Be(2);
        result.MappedChannels.Should().Be(2);
        result.DetectedChannels.Should().ContainSingle(c =>
            c.Id == "awp-1" &&
            c.DisplayName == "Morning Feed" &&
            c.MappedName == "Morning Feed" &&
            c.ProgramCount == 3 &&
            c.Sources.Count == 1 &&
            c.Sources[0] == epgPath);
        result.DetectedChannels.Should().ContainSingle(c =>
            c.Id == "wip-2" &&
            c.DisplayName == "Sports Feed" &&
            c.MappedName == "Sports Feed" &&
            c.ProgramCount == 1 &&
            c.Sources.Count == 1 &&
            c.Sources[0] == epgPath);
    }

    [Fact]
    public async Task PreviewChannelsAsync_WithInvalidJson_ReturnsErrorMessage()
    {
        var fetcher = new Mock<IDataFetcher>();
        fetcher.Setup(service => service.FetchDataWithResultAsync("source", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FetchResult
            {
                Success = true,
                Data = "not json"
            });

        var service = CreateValidationService(fetcher.Object, new[] { new StubGuideParser(canParse: true) });

        var result = await service.PreviewChannelsAsync("source", null);

        result.Success.Should().BeFalse();
        result.Message.Should().StartWith("Invalid JSON:");
    }

    [Fact]
    public async Task PreviewChannelsAsync_WithMissingChannelMap_FallsBackToUnmappedPreview()
    {
        var epgPath = Path.Combine(_testDataPath, "valid-guidetwo-epg.json");
        var missingMapPath = Path.Combine(_tempDir, "missing-map.json");
        var service = CreatePreviewValidationService();

        var result = await service.PreviewChannelsAsync(epgPath, missingMapPath);

        result.Success.Should().BeTrue();
        result.TotalChannels.Should().Be(2);
        result.MappedChannels.Should().Be(0);
        result.UnmappedChannels.Should().Be(2);
    }

    [Fact]
    public async Task PreviewChannelsAsync_WithNoMatchingParser_ReturnsParserMessage()
    {
        var fetcher = new Mock<IDataFetcher>();
        fetcher.Setup(service => service.FetchDataWithResultAsync("source", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FetchResult
            {
                Success = true,
                Data = """{ "channels": [] }"""
            });

        var service = CreateValidationService(fetcher.Object, new[] { new StubGuideParser(canParse: false) });
        var result = await service.PreviewChannelsAsync("source", null);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("No parser can handle this source format");
    }

    [Fact]
    public async Task PreviewChannelsAsync_WithHumanVerificationHtml_ReturnsSpecificMessage()
    {
        var fetcher = new Mock<IDataFetcher>();
        fetcher.Setup(service => service.FetchDataWithResultAsync("source", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FetchResult
            {
                Success = false,
                ErrorMessage = "Source returned an HTML human-verification page instead of JSON data."
            });

        var service = CreateValidationService(fetcher.Object, new[] { new StubGuideParser(canParse: true) });
        var result = await service.PreviewChannelsAsync("source", null);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Source returned an HTML human-verification page instead of JSON data.");
    }

    private ValidationService CreatePreviewValidationService()
    {
        var previewFetcher = new FakeDataFetcher();
        var logoFetcher = new Mock<IDataFetcher>();
        logoFetcher.Setup(fetcher => fetcher.ValidateUrl(It.IsAny<string>())).ReturnsAsync(false);

        var parsers = new IGuideParser[]
        {
            new GuideOneParser(),
            new GuideTwoParser(logoFetcher.Object),
            new GuideThreeParser()
        };

        return new ValidationService(
            previewFetcher,
            parsers,
            new ChannelMapLoader(),
            NullLogger<ValidationService>.Instance);
    }

    private static ValidationService CreateValidationService(IDataFetcher dataFetcher, IEnumerable<IGuideParser> parsers)
    {
        return new ValidationService(
            dataFetcher,
            parsers,
            new ChannelMapLoader(),
            NullLogger<ValidationService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private sealed class StubGuideParser : IGuideParser
    {
        private readonly bool _canParse;

        public StubGuideParser(bool canParse)
        {
            _canParse = canParse;
        }

        public bool CanParse(JsonObject epg)
        {
            return _canParse;
        }

        public XElement ProcessChannels(XElement tv, JsonObject epg, List<ChannelMapDto>? channelMap)
        {
            return tv;
        }
    }

    private sealed class LoopbackServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serverTask;

        private LoopbackServer(TcpListener listener, Task serverTask, string url)
        {
            _listener = listener;
            _serverTask = serverTask;
            Url = url;
        }

        public string Url { get; }

        public static async Task<LoopbackServer> StartAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var serverTask = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                var buffer = new byte[4096];
                _ = await stream.ReadAsync(buffer, 0, buffer.Length);

                var body = "ok";
                var response = $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
                var responseBytes = Encoding.ASCII.GetBytes(response);
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
            });

            await Task.Delay(25);
            return new LoopbackServer(listener, serverTask, $"http://127.0.0.1:{port}/test");
        }

        public void Dispose()
        {
            _listener.Stop();
            _serverTask.GetAwaiter().GetResult();
        }
    }

    private static int GetUnusedLocalPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
