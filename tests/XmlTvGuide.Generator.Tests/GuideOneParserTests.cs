using FluentAssertions;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using xmlTVGuide.Models;
using xmlTVGuide.Services.XMXTVBuilder.Parsers;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class GuideOneParserTests : IDisposable
{
    private readonly GuideOneParser _parser;
    private readonly string _testDataPath;

    public GuideOneParserTests()
    {
        _parser = new GuideOneParser();
        _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
    }

    #region CanParse Method Tests

    [Fact]
    public void CanParse_WithValidGuideOneStructure_ReturnsTrue()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "channelId": "123",
                    "callSign": "AWP",
                    "events": [
                        {
                            "startTime": "2025-09-30T08:00:00Z",
                            "program": { "title": "Test Show" }
                        }
                    ]
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();

        // Act
        var result = _parser.CanParse(epgData);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanParse_WithMissingChannelsKey_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "data": [
                {
                    "sourceId": "123",
                    "networkName": "AWP"
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();

        // Act
        var result = _parser.CanParse(epgData);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanParse_WithEmptyChannelsArray_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "channels": []
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();

        // Act
        var result = _parser.CanParse(epgData);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanParse_WithNonArrayChannels_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "channels": "not an array"
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();

        // Act
        var result = _parser.CanParse(epgData);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanParse_WithMissingRequiredFields_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "someOtherField": "value"
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();

        // Act
        var result = _parser.CanParse(epgData);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanParse_WithMissingCallSign_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "channelId": "123",
                    "events": []
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();

        // Act
        var result = _parser.CanParse(epgData);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanParse_WithMissingEvents_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "channelId": "123",
                    "callSign": "AWP"
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();

        // Act
        var result = _parser.CanParse(epgData);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanParse_WithNonArrayEvents_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "channelId": "123",
                    "callSign": "AWP",
                    "events": "not an array"
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();

        // Act
        var result = _parser.CanParse(epgData);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanParse_WithProgramKeyPresent_ReturnsFalse()
    {
        // Arrange - GuideOne format should not have 'program' key at channel level
        var json = """
        {
            "channels": [
                {
                    "channelId": "123",
                    "callSign": "AWP",
                    "events": [],
                    "program": "should not be here"
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();

        // Act
        var result = _parser.CanParse(epgData);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ProcessChannels Method Tests

    [Fact]
    public void ProcessChannels_WithValidData_AddsChannelsAndProgrammes()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "valid-guideone-epg.json");
        var jsonContent = File.ReadAllText(filePath);
        var epgData = JsonNode.Parse(jsonContent)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Should().NotBeNull();
        result.Elements("channel").Should().HaveCount(2);
        result.Elements("programme").Should().HaveCount(3);

        // Check first channel
        var firstChannel = result.Elements("channel").First();
        firstChannel.Attribute("id")!.Value.Should().Be("12345");
        firstChannel.Element("display-name")!.Value.Should().Be("1 AWP");

        // Check icon element
        var icon = firstChannel.Element("icon");
        icon.Should().NotBeNull();
        icon!.Attribute("src")!.Value.Should().Be("https://example.com/awp-logo.png");
    }

    [Fact]
    public void ProcessChannels_WithChannelMap_UsesChannelMapNames()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "channelId": "12345",
                    "callSign": "AWP",
                    "channelNo": "1",
                    "events": []
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");
        var channelMap = new List<ChannelMapDto>
        {
            new() { ChannelId = "12345", Name = "Custom AWP Channel" }
        };

        // Act
        var result = _parser.ProcessChannels(tv, epgData, channelMap);

        // Assert
        var channel = result.Elements("channel").First();
        channel.Element("display-name")!.Value.Should().Be("Custom AWP Channel");
    }

    [Fact]
    public void ProcessChannels_WithDuplicateChannels_AddsChannelOnlyOnce()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "channelId": "12345",
                    "callSign": "AWP",
                    "events": []
                },
                {
                    "channelId": "12345",
                    "callSign": "AWP Duplicate",
                    "events": []
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().HaveCount(1);
    }

    [Fact]
    public void ProcessChannels_WithExistingChannelInTv_DoesNotAddDuplicate()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "channelId": "12345",
                    "callSign": "AWP",
                    "events": []
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv",
            new XElement("channel", new XAttribute("id", "12345"),
                new XElement("display-name", "Existing Channel")));

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().HaveCount(1);
        result.Elements("channel").First().Element("display-name")!.Value.Should().Be("Existing Channel");
    }

    [Fact]
    public void ProcessChannels_WithThumbnailStartingWithSlash_FormatsProperly()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "channelId": "12345",
                    "callSign": "AWP",
                    "thumbnail": "/path/to/logo.png",
                    "events": []
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        var icon = result.Elements("channel").First().Element("icon");
        icon.Should().NotBeNull();
        icon!.Attribute("src")!.Value.Should().Be("https://path/to/logo.png");
    }

    [Fact]
    public void ProcessChannels_WithValidProgrammes_CreatesProgrammeElements()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "channelId": "12345",
                    "callSign": "AWP",
                    "events": [
                        {
                            "startTime": "2025-09-30T08:00:00Z",
                            "endTime": "2025-09-30T09:00:00Z",
                            "program": {
                                "title": "Morning News",
                                "shortDesc": "Local news coverage"
                            }
                        }
                    ]
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        var programme = result.Elements("programme").First();
        programme.Attribute("start")!.Value.Should().Be("20250930080000 +0000");
        programme.Attribute("stop")!.Value.Should().Be("20250930090000 +0000");
        programme.Attribute("channel")!.Value.Should().Be("12345");
        programme.Element("title")!.Value.Should().Be("Morning News");
        programme.Element("title")!.Attribute("lang")!.Value.Should().Be("en");
        programme.Element("desc")!.Value.Should().Be("Local news coverage");
        programme.Element("desc")!.Attribute("lang")!.Value.Should().Be("en");
    }

    [Fact]
    public void ProcessChannels_WithProgrammeWithoutDesc_CreatesProgrammeWithoutDescElement()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "channelId": "12345",
                    "callSign": "AWP",
                    "events": [
                        {
                            "startTime": "2025-09-30T08:00:00Z",
                            "endTime": "2025-09-30T09:00:00Z",
                            "program": {
                                "title": "Morning News"
                            }
                        }
                    ]
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        var programme = result.Elements("programme").First();
        programme.Element("title").Should().NotBeNull();
        programme.Element("desc").Should().BeNull();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void ProcessChannels_WithEmptyChannelsArray_ReturnsEmptyTv()
    {
        // Arrange
        var json = """
        {
            "channels": []
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().BeEmpty();
        result.Elements("programme").Should().BeEmpty();
    }

    [Fact]
    public void ProcessChannels_WithMissingChannelsKey_ReturnsOriginalTv()
    {
        // Arrange
        var json = """
        {
            "data": []
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().BeEmpty();
        result.Elements("programme").Should().BeEmpty();
    }

    [Fact]
    public void ProcessChannels_WithChannelMissingId_SkipsChannel()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "callSign": "AWP",
                    "events": []
                },
                {
                    "channelId": "67890",
                    "callSign": "WIP",
                    "events": []
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().HaveCount(1);
        result.Elements("channel").First().Attribute("id")!.Value.Should().Be("67890");
    }

    [Fact]
    public void ProcessChannels_WithChannelMissingCallSign_SkipsChannel()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "channelId": "12345",
                    "events": []
                },
                {
                    "channelId": "67890",
                    "callSign": "WIP",
                    "events": []
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().HaveCount(1);
        result.Elements("channel").First().Attribute("id")!.Value.Should().Be("67890");
    }

    [Fact]
    public void ProcessChannels_WithInvalidEventData_SkipsMalformedProgrammes()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "malformed-guideone-epg.json");
        var jsonContent = File.ReadAllText(filePath);
        var epgData = JsonNode.Parse(jsonContent)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        // Should add only valid channels and skip malformed ones
        result.Elements("channel").Should().HaveCount(1); // Only the XYZ channel with valid ID and callSign
        result.Elements("programme").Should().BeEmpty(); // All programmes have issues
    }

    [Fact]
    public void ProcessChannels_WithEventMissingRequiredFields_SkipsProgramme()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "channelId": "12345",
                    "callSign": "AWP",
                    "events": [
                        {
                            "startTime": "2025-09-30T08:00:00Z",
                            "program": {
                                "shortDesc": "No title"
                            }
                        },
                        {
                            "endTime": "2025-09-30T09:00:00Z",
                            "program": {
                                "title": "No start time"
                            }
                        },
                        {
                            "startTime": "2025-09-30T08:00:00Z",
                            "endTime": "2025-09-30T09:00:00Z",
                            "program": {
                                "title": "Valid Programme"
                            }
                        }
                    ]
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().HaveCount(1);
        result.Elements("programme").Should().HaveCount(1);
        result.Elements("programme").First().Element("title")!.Value.Should().Be("Valid Programme");
    }

    [Fact]
    public void ProcessChannels_WithInvalidDateFormat_SkipsProgramme()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "channelId": "12345",
                    "callSign": "AWP",
                    "events": [
                        {
                            "startTime": "invalid-date",
                            "endTime": "2025-09-30T09:00:00Z",
                            "program": {
                                "title": "Invalid Start Time"
                            }
                        }
                    ]
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().HaveCount(1);
        result.Elements("programme").Should().BeEmpty();
    }

    [Fact]
    public void ProcessChannels_WithNonJsonObjectEvent_SkipsEvent()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "channelId": "12345",
                    "callSign": "AWP",
                    "events": [
                        "invalid event",
                        {
                            "startTime": "2025-09-30T08:00:00Z",
                            "endTime": "2025-09-30T09:00:00Z",
                            "program": {
                                "title": "Valid Programme"
                            }
                        }
                    ]
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().HaveCount(1);
        result.Elements("programme").Should().HaveCount(1);
        result.Elements("programme").First().Element("title")!.Value.Should().Be("Valid Programme");
    }

    [Fact]
    public void ProcessChannels_ChannelsSortedByCallSign_ReturnsChannelsInOrder()
    {
        // Arrange
        var json = """
        {
            "channels": [
                {
                    "channelId": "3",
                    "callSign": "ZZZ",
                    "events": []
                },
                {
                    "channelId": "1",
                    "callSign": "AAA",
                    "events": []
                },
                {
                    "channelId": "2",
                    "callSign": "MMM",
                    "events": []
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        var channels = result.Elements("channel").ToList();
        channels.Should().HaveCount(3);
        channels[0].Attribute("id")!.Value.Should().Be("1"); // AAA
        channels[1].Attribute("id")!.Value.Should().Be("2"); // MMM
        channels[2].Attribute("id")!.Value.Should().Be("3"); // ZZZ
    }

    #endregion

    #region Helper Method Tests

    [Theory]
    [InlineData("2025-09-30T08:00:00Z", "20250930080000 +0000")]
    [InlineData("2025-12-25T23:59:59Z", "20251225235959 +0000")]
    [InlineData("2025-01-01T00:00:00Z", "20250101000000 +0000")]
    public void FormatTime_WithValidIsoString_ReturnsFormattedTime(string isoString, string expected)
    {
        // Arrange
        var json = $@"{{
            ""channels"": [
                {{
                    ""channelId"": ""12345"",
                    ""callSign"": ""AWP"",
                    ""events"": [
                        {{
                            ""startTime"": ""{isoString}"",
                            ""endTime"": ""{isoString}"",
                            ""program"": {{
                                ""title"": ""Test Show""
                            }}
                        }}
                    ]
                }}
            ]
        }}";
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        var programme = result.Elements("programme").First();
        programme.Attribute("start")!.Value.Should().Be(expected);
        programme.Attribute("stop")!.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("invalid-date")]
    [InlineData("")]
    public void FormatTime_WithInvalidIsoString_SkipsProgramme(string invalidDate)
    {
        // Arrange
        var json = $@"{{
            ""channels"": [
                {{
                    ""channelId"": ""12345"",
                    ""callSign"": ""AWP"",
                    ""events"": [
                        {{
                            ""startTime"": ""{invalidDate}"",
                            ""endTime"": ""2025-09-30T09:00:00Z"",
                            ""program"": {{
                                ""title"": ""Test Show""
                            }}
                        }}
                    ]
                }}
            ]
        }}";
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("programme").Should().BeEmpty();
    }

    [Fact]
    public void FormatTime_WithNullIsoString_SkipsProgramme()
    {
        // Arrange
        var json = @"{
            ""channels"": [
                {
                    ""channelId"": ""12345"",
                    ""callSign"": ""AWP"",
                    ""events"": [
                        {
                            ""startTime"": null,
                            ""endTime"": ""2025-09-30T09:00:00Z"",
                            ""program"": {
                                ""title"": ""Test Show""
                            }
                        }
                    ]
                }
            ]
        }";
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("programme").Should().BeEmpty();
    }

    #endregion

    public void Dispose()
    {
        // Cleanup if needed
    }
}
