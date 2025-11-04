using FluentAssertions;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using xmlTVGuide.Models;
using xmlTVGuide.Services.XMXTVBuilder.Parsers;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class GuideThreeParserTests : IDisposable
{
    private readonly GuideThreeParser _parser;
    private readonly string _testDataPath;

    public GuideThreeParserTests()
    {
        _parser = new GuideThreeParser();
        _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
    }

    #region CanParse Method Tests

    [Fact]
    public void CanParse_WithValidGuideThreeStructure_ReturnsTrue()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Test Show",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
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
    public void CanParse_WithMissingItemsKey_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "data": {
                "content": {
                    "streams": []
                }
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();

        // Act
        var result = _parser.CanParse(epgData);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanParse_WithNonArrayItems_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "items": "not an array"
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();

        // Act
        var result = _parser.CanParse(epgData);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanParse_WithEmptyItemsArray_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "items": []
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();

        // Act
        var result = _parser.CanParse(epgData);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanParse_WithMissingContentKey_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "data": {
                        "streams": []
                    }
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
    public void CanParse_WithNonObjectContent_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": "not an object"
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
    public void CanParse_WithMissingStreamsKey_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "data": []
                    }
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
    public void CanParse_WithNonArrayStreams_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": "not an array"
                    }
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
    public void CanParse_WithEmptyStreamsArray_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": []
                    }
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
    public void CanParse_WithMissingChannelKey_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "title": "Test Show",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
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
    public void CanParse_WithMissingTitleKey_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
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
    public void CanParse_WithMissingStartDateKey_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Test Show",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
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
    public void CanParse_WithMissingEndDateKey_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Test Show",
                                "start_date": "2023-09-30T08:00:00Z"
                            }
                        ]
                    }
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
        var filePath = Path.Combine(_testDataPath, "valid-guidethree-epg.json");
        var jsonContent = File.ReadAllText(filePath);
        var epgData = JsonNode.Parse(jsonContent)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Should().NotBeNull();
        result.Elements("channel").Should().HaveCount(2);
        result.Elements("programme").Should().HaveCount(4);

        // Check first channel
        var firstChannel = result.Elements("channel").First();
        firstChannel.Attribute("id")!.Value.Should().Be("awp-1");
        firstChannel.Element("display-name")!.Value.Should().Be("awp-1"); // Uses channel ID as display name when no mapping

        // Check second channel
        var secondChannel = result.Elements("channel").Skip(1).First();
        secondChannel.Attribute("id")!.Value.Should().Be("wip-2");
        secondChannel.Element("display-name")!.Value.Should().Be("wip-2");

        // Check first programme
        var programmes = result.Elements("programme").ToList();
        programmes[0].Attribute("start")!.Value.Should().Be("20230930080000 +0000");
        programmes[0].Attribute("stop")!.Value.Should().Be("20230930090000 +0000");
        programmes[0].Attribute("channel")!.Value.Should().Be("awp-1");
        programmes[0].Element("title")!.Value.Should().Be("Morning Show");
        programmes[0].Element("title")!.Attribute("lang")!.Value.Should().Be("en");
        programmes[0].Element("desc")!.Value.Should().Be("Daily morning news and entertainment");
        programmes[0].Element("desc")!.Attribute("lang")!.Value.Should().Be("en");
    }

    [Fact]
    public void ProcessChannels_WithChannelMap_UsesChannelMapNames()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Test Show",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");
        var channelMap = new List<ChannelMapDto>
        {
            new() { ChannelId = "awp-1", Name = "AWP Channel One" }
        };

        // Act
        var result = _parser.ProcessChannels(tv, epgData, channelMap);

        // Assert
        var channel = result.Elements("channel").First();
        channel.Element("display-name")!.Value.Should().Be("AWP Channel One");
    }

    [Fact]
    public void ProcessChannels_WithDuplicateChannels_DoesNotAddDuplicateChannelElements()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "First Show",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            },
                            {
                                "channel": "awp-1",
                                "title": "Second Show",
                                "start_date": "2023-09-30T09:00:00Z",
                                "end_date": "2023-09-30T10:00:00Z"
                            }
                        ]
                    }
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
        result.Elements("programme").Should().HaveCount(2);
    }

    [Fact]
    public void ProcessChannels_WithThumbnail_AddsIconElement()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Test Show",
                                "thumbnail": "https://example.com/icon.jpg",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        var channel = result.Elements("channel").First();
        var icon = channel.Element("icon");
        icon.Should().NotBeNull();
        icon!.Attribute("src")!.Value.Should().Be("https://example.com/icon.jpg");
    }

    [Fact]
    public void ProcessChannels_WithoutThumbnail_DoesNotAddIconElement()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Test Show",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        var channel = result.Elements("channel").First();
        channel.Element("icon").Should().BeNull();
    }

    [Fact]
    public void ProcessChannels_WithEmptyThumbnail_DoesNotAddIconElement()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Test Show",
                                "thumbnail": "",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
                }
            ]
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        var channel = result.Elements("channel").First();
        channel.Element("icon").Should().BeNull();
    }

    [Fact]
    public void ProcessChannels_WithDescription_AddsProgrammeDescription()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Test Show",
                                "desc": "This is a test programme description",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
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
        var desc = programme.Element("desc");
        desc.Should().NotBeNull();
        desc!.Value.Should().Be("This is a test programme description");
        desc.Attribute("lang")!.Value.Should().Be("en");
    }

    [Fact]
    public void ProcessChannels_WithoutDescription_DoesNotAddProgrammeDescription()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Test Show",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
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
        programme.Element("desc").Should().BeNull();
    }

    [Fact]
    public void ProcessChannels_WithEmptyDescription_DoesNotAddProgrammeDescription()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Test Show",
                                "desc": "",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
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
        programme.Element("desc").Should().BeNull();
    }

    [Fact]
    public void ProcessChannels_WithValidDateTimeFormats_ConvertsToCorrectFormat()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Test Show",
                                "start_date": "2023-09-30T14:30:00Z",
                                "end_date": "2023-09-30T15:45:00Z"
                            }
                        ]
                    }
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
        programme.Attribute("start")!.Value.Should().Be("20230930143000 +0000");
        programme.Attribute("stop")!.Value.Should().Be("20230930154500 +0000");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void ProcessChannels_WithMissingItemsKey_ReturnsOriginalTv()
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
        result.Should().BeSameAs(tv);
        result.Elements().Should().BeEmpty();
    }

    [Fact]
    public void ProcessChannels_WithNonArrayItems_ReturnsOriginalTv()
    {
        // Arrange
        var json = """
        {
            "items": "not an array"
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Should().BeSameAs(tv);
        result.Elements().Should().BeEmpty();
    }

    [Fact]
    public void ProcessChannels_WithMalformedData_SkipsInvalidItems()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "malformed-guidethree-epg.json");
        var jsonContent = File.ReadAllText(filePath);
        var epgData = JsonNode.Parse(jsonContent)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().HaveCount(1); // Only one valid channel with valid show
        result.Elements("programme").Should().HaveCount(1); // Only one valid programme
        
        var channel = result.Elements("channel").First();
        channel.Attribute("id")!.Value.Should().Be("awp-1");
        
        var programme = result.Elements("programme").First();
        programme.Element("title")!.Value.Should().Be("Valid Show");
    }

    [Fact]
    public void ProcessChannels_WithMissingChannelId_SkipsStream()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "title": "Show Without Channel",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            },
                            {
                                "channel": "awp-1",
                                "title": "Valid Show",
                                "start_date": "2023-09-30T09:00:00Z",
                                "end_date": "2023-09-30T10:00:00Z"
                            }
                        ]
                    }
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
        result.Elements("programme").First().Element("title")!.Value.Should().Be("Valid Show");
    }

    [Fact]
    public void ProcessChannels_WithEmptyChannelId_SkipsStream()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "",
                                "title": "Show With Empty Channel",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
                }
            ]
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
    public void ProcessChannels_WithMissingTitle_SkipsStream()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
                }
            ]
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
    public void ProcessChannels_WithEmptyTitle_SkipsStream()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
                }
            ]
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
    public void ProcessChannels_WithInvalidStartDate_SkipsStream()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Test Show",
                                "start_date": "invalid-date",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
                }
            ]
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
    public void ProcessChannels_WithInvalidEndDate_SkipsStream()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Test Show",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "invalid-date"
                            }
                        ]
                    }
                }
            ]
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
    public void ProcessChannels_WithMissingStartDate_SkipsStream()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Test Show",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
                }
            ]
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
    public void ProcessChannels_WithMissingEndDate_SkipsStream()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Test Show",
                                "start_date": "2023-09-30T08:00:00Z"
                            }
                        ]
                    }
                }
            ]
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
    public void ProcessChannels_WithMissingContentKey_SkipsItem()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "data": {
                        "streams": []
                    }
                },
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Valid Show",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
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
        result.Elements("programme").First().Element("title")!.Value.Should().Be("Valid Show");
    }

    [Fact]
    public void ProcessChannels_WithNonObjectContent_SkipsItem()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": "not an object"
                },
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Valid Show",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
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
        result.Elements("programme").First().Element("title")!.Value.Should().Be("Valid Show");
    }

    [Fact]
    public void ProcessChannels_WithMissingStreamsKey_SkipsItem()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "data": []
                    }
                },
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Valid Show",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
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
        result.Elements("programme").First().Element("title")!.Value.Should().Be("Valid Show");
    }

    [Fact]
    public void ProcessChannels_WithNonArrayStreams_SkipsItem()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": "not an array"
                    }
                },
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Valid Show",
                                "start_date": "2023-09-30T08:00:00Z",
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
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
        result.Elements("programme").First().Element("title")!.Value.Should().Be("Valid Show");
    }

    #endregion

    #region DateTime Parsing Tests

    [Theory]
    [InlineData("2023-09-30T08:00:00Z", "20230930080000 +0000")]
    [InlineData("2023-12-31T23:59:59Z", "20231231235959 +0000")]
    [InlineData("2023-01-01T00:00:00Z", "20230101000000 +0000")]
    [InlineData("2023-06-15T12:30:45Z", "20230615123045 +0000")]
    public void ParseDateTime_WithValidDateTimeString_ReturnsCorrectFormat(string dateTimeString, string expected)
    {
        // Arrange
        var json = $@"{{
            ""items"": [
                {{
                    ""content"": {{
                        ""streams"": [
                            {{
                                ""channel"": ""awp-1"",
                                ""title"": ""Test Show"",
                                ""start_date"": ""{dateTimeString}"",
                                ""end_date"": ""{dateTimeString}""
                            }}
                        ]
                    }}
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
    [InlineData("2023-13-01T08:00:00Z")] // Invalid month
    [InlineData("2023-09-32T08:00:00Z")] // Invalid day
    [InlineData("not-a-date")]
    public void ParseDateTime_WithInvalidDateTimeString_SkipsProgramme(string invalidDateTime)
    {
        // Arrange
        var json = $@"{{
            ""items"": [
                {{
                    ""content"": {{
                        ""streams"": [
                            {{
                                ""channel"": ""awp-1"",
                                ""title"": ""Test Show"",
                                ""start_date"": ""{invalidDateTime}"",
                                ""end_date"": ""2023-09-30T09:00:00Z""
                            }}
                        ]
                    }}
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
    public void ParseDateTime_WithNullDateTime_SkipsProgramme()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "content": {
                        "streams": [
                            {
                                "channel": "awp-1",
                                "title": "Test Show",
                                "start_date": null,
                                "end_date": "2023-09-30T09:00:00Z"
                            }
                        ]
                    }
                }
            ]
        }
        """;
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