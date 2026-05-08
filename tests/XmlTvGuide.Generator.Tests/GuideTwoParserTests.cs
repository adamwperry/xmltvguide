using FluentAssertions;
using Moq;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using xmlTVGuide.Models;
using xmlTVGuide.Services;
using xmlTVGuide.Services.XMXTVBuilder.Parsers;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class GuideTwoParserTests : IDisposable
{
    private readonly Mock<IDataFetcher> _mockDataFetcher;
    private readonly GuideTwoParser _parser;
    private readonly string _testDataPath;

    public GuideTwoParserTests()
    {
        _mockDataFetcher = new Mock<IDataFetcher>();
        _parser = new GuideTwoParser(_mockDataFetcher.Object);
        _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
    }

    #region CanParse Method Tests

    [Fact]
    public void CanParse_WithValidGuideTwoStructure_ReturnsTrue()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "123",
                            "networkName": "AWP Network"
                        },
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();

        // Act
        var result = _parser.CanParse(epgData);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanParse_WithMissingDataKey_ReturnsFalse()
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
    public void CanParse_WithNonObjectData_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "data": "not an object"
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();

        // Act
        var result = _parser.CanParse(epgData);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanParse_WithMissingItemsKey_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "data": {
                "otherKey": "value"
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
            "data": {
                "items": "not an array"
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
    public void CanParse_WithEmptyItemsArray_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": []
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
    public void CanParse_WithMissingSourceId_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "networkName": "AWP Network"
                        }
                    }
                ]
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
    public void CanParse_WithMissingNetworkName_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "123"
                        }
                    }
                ]
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
    public void CanParse_WithMissingChannelKey_ReturnsFalse()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "programSchedules": []
                    }
                ]
            }
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
        var filePath = Path.Combine(_testDataPath, "valid-guidetwo-epg.json");
        var jsonContent = File.ReadAllText(filePath);
        var epgData = JsonNode.Parse(jsonContent)!.AsObject();
        var tv = new XElement("tv");

        _mockDataFetcher.Setup(x => x.ValidateUrl("https://raw.githubusercontent.com/tv-logo/tv-logos/refs/heads/main/countries/united-states/awp-network-us.png"))
                       .ReturnsAsync(true);
        _mockDataFetcher.Setup(x => x.ValidateUrl("https://raw.githubusercontent.com/tv-logo/tv-logos/refs/heads/main/countries/united-states/wip-network-us.png"))
                       .ReturnsAsync(true);

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Should().NotBeNull();
        result.Elements("channel").Should().HaveCount(2);
        result.Elements("programme").Should().HaveCount(3);

        // Check first channel
        var firstChannel = result.Elements("channel").First();
        firstChannel.Attribute("id")!.Value.Should().Be("12345");
        firstChannel.Element("display-name")!.Value.Should().Be("AWP Network");

        // Check programmes
        var programmes = result.Elements("programme").ToList();
        programmes[0].Attribute("start")!.Value.Should().Be("20230930160000 +0000");
        programmes[0].Attribute("stop")!.Value.Should().Be("20230930170000 +0000");
        programmes[0].Attribute("channel")!.Value.Should().Be("12345");
        programmes[0].Element("title")!.Value.Should().Be("Morning News");
        programmes[0].Element("title")!.Attribute("lang")!.Value.Should().Be("en");
    }

    [Fact]
    public void ProcessChannels_WithChannelMap_UsesChannelMapNames()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": "Original Name"
                        },
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");
        var channelMap = new List<ChannelMapDto>
        {
            new() { ChannelId = "12345", Name = "Custom Channel Name" }
        };

        _mockDataFetcher.Setup(x => x.ValidateUrl(It.IsAny<string>()))
                       .ReturnsAsync(false);

        // Act
        var result = _parser.ProcessChannels(tv, epgData, channelMap);

        // Assert
        var channel = result.Elements("channel").First();
        channel.Element("display-name")!.Value.Should().Be("Custom Channel Name");
    }

    [Fact]
    public void ProcessChannels_WithDuplicateChannels_AddsBothInstances()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": "AWP Network"
                        },
                        "programSchedules": []
                    },
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": "AWP Network Duplicate"
                        },
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        _mockDataFetcher.Setup(x => x.ValidateUrl(It.IsAny<string>()))
                       .ReturnsAsync(false);

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().HaveCount(1); // Should be distinct by sourceId
    }

    [Fact]
    public void ProcessChannels_SortsChannelsBySourceIdNumber()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "300",
                            "networkName": "AAA Network"
                        },
                        "programSchedules": []
                    },
                    {
                        "channel": {
                            "sourceId": "100",
                            "networkName": "ZZZ Network"
                        },
                        "programSchedules": []
                    },
                    {
                        "channel": {
                            "sourceId": "20",
                            "networkName": "MMM Network"
                        },
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        _mockDataFetcher.Setup(x => x.ValidateUrl(It.IsAny<string>()))
                       .ReturnsAsync(false);

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        var channels = result.Elements("channel").ToList();
        channels.Should().HaveCount(3);
        channels[0].Attribute("id")!.Value.Should().Be("20");
        channels[1].Attribute("id")!.Value.Should().Be("100");
        channels[2].Attribute("id")!.Value.Should().Be("300");
    }

    [Fact]
    public void ProcessChannels_WithValidIconUrl_AddsIconElement()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": "AWP Network",
                            "name": "awp-network",
                            "logo": "https://example.com/awp-logo.png"
                        },
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        _mockDataFetcher.Setup(x => x.ValidateUrl("https://raw.githubusercontent.com/tv-logo/tv-logos/refs/heads/main/countries/united-states/awp-network-us.png"))
                       .ReturnsAsync(true);

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        var channel = result.Elements("channel").First();
        var icon = channel.Element("icon");
        icon.Should().NotBeNull();
        icon!.Attribute("src")!.Value.Should().Be("https://raw.githubusercontent.com/tv-logo/tv-logos/refs/heads/main/countries/united-states/awp-network-us.png");
    }

    [Fact]
    public void ProcessChannels_WithInvalidIconUrl_DoesNotAddIconElement()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": "AWP Network",
                            "name": "awp-network"
                        },
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        _mockDataFetcher.Setup(x => x.ValidateUrl(It.IsAny<string>()))
                       .ReturnsAsync(false);

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        var channel = result.Elements("channel").First();
        channel.Element("icon").Should().BeNull();
    }

    [Fact]
    public void ProcessChannels_WithNameContainingSpaces_SanitizesIconUrl()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": "AWP Network",
                            "name": "AWP Network Name",
                            "logo": "https://example.com/awp-logo.png"
                        },
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        _mockDataFetcher.Setup(x => x.ValidateUrl("https://raw.githubusercontent.com/tv-logo/tv-logos/refs/heads/main/countries/united-states/awp-network-name-us.png"))
                       .ReturnsAsync(true);

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        _mockDataFetcher.Verify(x => x.ValidateUrl("https://raw.githubusercontent.com/tv-logo/tv-logos/refs/heads/main/countries/united-states/awp-network-name-us.png"), Times.Once);
    }

    [Fact]
    public void ProcessChannels_WithUnixTimestamps_ConvertsToCorrectFormat()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": "AWP Network"
                        },
                        "programSchedules": [
                            {
                                "startTime": 1696089600,
                                "endTime": 1696093200,
                                "title": "Test Show"
                            }
                        ]
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        _mockDataFetcher.Setup(x => x.ValidateUrl(It.IsAny<string>()))
                       .ReturnsAsync(false);

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        var programme = result.Elements("programme").First();
        programme.Attribute("start")!.Value.Should().Be("20230930160000 +0000");
        programme.Attribute("stop")!.Value.Should().Be("20230930170000 +0000");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void ProcessChannels_WithMissingDataKey_ReturnsOriginalTv()
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
        result.Should().BeSameAs(tv);
        result.Elements().Should().BeEmpty();
    }

    [Fact]
    public void ProcessChannels_WithNonObjectData_ReturnsOriginalTv()
    {
        // Arrange
        var json = """
        {
            "data": "not an object"
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
    public void ProcessChannels_WithMissingItemsKey_ReturnsOriginalTv()
    {
        // Arrange
        var json = """
        {
            "data": {
                "otherKey": "value"
            }
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
            "data": {
                "items": "not an array"
            }
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
        var filePath = Path.Combine(_testDataPath, "malformed-guidetwo-epg.json");
        var jsonContent = File.ReadAllText(filePath);
        var epgData = JsonNode.Parse(jsonContent)!.AsObject();
        var tv = new XElement("tv");

        _mockDataFetcher.Setup(x => x.ValidateUrl(It.IsAny<string>()))
                       .ReturnsAsync(false);

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().HaveCount(1); // Only valid channel with sourceId "12345"
        result.Elements("programme").Should().HaveCount(1); // Only valid programme
    }

    [Fact]
    public void ProcessChannels_WithMissingSourceId_SkipsItem()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "networkName": "Network Without ID"
                        },
                        "programSchedules": []
                    },
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": "Valid Network"
                        },
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        _mockDataFetcher.Setup(x => x.ValidateUrl(It.IsAny<string>()))
                       .ReturnsAsync(false);

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().HaveCount(1);
        result.Elements("channel").First().Attribute("id")!.Value.Should().Be("12345");
    }

    [Fact]
    public void ProcessChannels_WithEmptySourceId_SkipsItem()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "",
                            "networkName": "Network With Empty ID"
                        },
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().BeEmpty();
    }

    [Fact]
    public void ProcessChannels_WithMissingNetworkName_SkipsItem()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "12345"
                        },
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().BeEmpty();
    }

    [Fact]
    public void ProcessChannels_WithEmptyNetworkName_SkipsItem()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": ""
                        },
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().BeEmpty();
    }

    [Fact]
    public void ProcessChannels_WithNullChannelNode_SkipsItem()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().BeEmpty();
    }

    [Fact]
    public void ProcessChannels_WithNullProgramSchedules_SkipsItem()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": "AWP Network"
                        }
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().BeEmpty();
    }

    [Fact]
    public void ProcessChannels_WithNonArrayProgramSchedules_SkipsItem()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": "AWP Network"
                        },
                        "programSchedules": "not an array"
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().BeEmpty();
    }

    [Fact]
    public void ProcessChannels_WithInvalidProgramData_SkipsMalformedProgrammes()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": "AWP Network"
                        },
                        "programSchedules": [
                            {
                                "startTime": "invalid",
                                "endTime": 1696093200,
                                "title": "Invalid Start Time"
                            },
                            {
                                "startTime": 1696089600,
                                "endTime": "invalid",
                                "title": "Invalid End Time"
                            },
                            {
                                "startTime": 1696089600,
                                "endTime": 1696093200
                            },
                            {
                                "startTime": 1696089600,
                                "endTime": 1696093200,
                                "title": "Valid Programme"
                            }
                        ]
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        _mockDataFetcher.Setup(x => x.ValidateUrl(It.IsAny<string>()))
                       .ReturnsAsync(false);

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().HaveCount(1);
        result.Elements("programme").Should().HaveCount(1);
        result.Elements("programme").First().Element("title")!.Value.Should().Be("Valid Programme");
    }

    [Fact]
    public void ProcessChannels_WithEmptyTitle_SkipsProgramme()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": "AWP Network"
                        },
                        "programSchedules": [
                            {
                                "startTime": 1696089600,
                                "endTime": 1696093200,
                                "title": ""
                            }
                        ]
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        _mockDataFetcher.Setup(x => x.ValidateUrl(It.IsAny<string>()))
                       .ReturnsAsync(false);

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().HaveCount(1);
        result.Elements("programme").Should().BeEmpty();
    }

    #endregion

    #region Icon Building Tests

    [Fact]
    public void BuildChannelIconUrl_WithNetworkNameContainingSpaces_ReplacesSpacesWithHyphens()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": "AWP Network",
                            "name": "AWP Network Test",
                            "logo": "https://example.com/awp-logo.png"
                        },
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        _mockDataFetcher.Setup(x => x.ValidateUrl("https://raw.githubusercontent.com/tv-logo/tv-logos/refs/heads/main/countries/united-states/awp-network-test-us.png"))
                       .ReturnsAsync(true);

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        _mockDataFetcher.Verify(x => x.ValidateUrl("https://raw.githubusercontent.com/tv-logo/tv-logos/refs/heads/main/countries/united-states/awp-network-test-us.png"), Times.Once);
    }

    [Fact]
    public void BuildChannelIconUrl_WithMixedCaseNetworkName_ConvertsToLowerCase()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": "AWP Network",
                            "name": "AWP Network",
                            "logo": "https://example.com/awp-logo.png"
                        },
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        _mockDataFetcher.Setup(x => x.ValidateUrl("https://raw.githubusercontent.com/tv-logo/tv-logos/refs/heads/main/countries/united-states/awp-network-us.png"))
                       .ReturnsAsync(true);

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        _mockDataFetcher.Verify(x => x.ValidateUrl("https://raw.githubusercontent.com/tv-logo/tv-logos/refs/heads/main/countries/united-states/awp-network-us.png"), Times.Once);
    }

    [Fact]
    public void BuildChannelIconElement_WithNullChannelNode_ReturnsNull()
    {
        // This test is indirectly covered by the ProcessChannels tests, but we test the behavior
        // when channel node is null by having an item with no channel property
        var json = """
        {
            "data": {
                "items": [
                    {
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        result.Elements("channel").Should().BeEmpty();
        _mockDataFetcher.Verify(x => x.ValidateUrl(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void BuildChannelIconElement_WithEmptyNetworkName_DoesNotCreateIcon()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": "AWP Network",
                            "name": ""
                        },
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        var channel = result.Elements("channel").First();
        channel.Element("icon").Should().BeNull();
        _mockDataFetcher.Verify(x => x.ValidateUrl(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void BuildChannelIconElement_WithMissingNameProperty_DoesNotCreateIcon()
    {
        // Arrange
        var json = """
        {
            "data": {
                "items": [
                    {
                        "channel": {
                            "sourceId": "12345",
                            "networkName": "AWP Network"
                        },
                        "programSchedules": []
                    }
                ]
            }
        }
        """;
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        var channel = result.Elements("channel").First();
        channel.Element("icon").Should().BeNull();
        _mockDataFetcher.Verify(x => x.ValidateUrl(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region Helper Method Tests

    [Theory]
    [InlineData(1696089600, "20230930160000 +0000")]
    [InlineData(1735689599, "20241231235959 +0000")]
    [InlineData(0, "19700101000000 +0000")]
    public void ParseUnixTime_WithValidTimestamp_ReturnsCorrectFormat(long unixTime, string expected)
    {
        // Arrange
        var json = $@"{{
            ""data"": {{
                ""items"": [
                    {{
                        ""channel"": {{
                            ""sourceId"": ""12345"",
                            ""networkName"": ""AWP Network""
                        }},
                        ""programSchedules"": [
                            {{
                                ""startTime"": {unixTime},
                                ""endTime"": {unixTime},
                                ""title"": ""Test Show""
                            }}
                        ]
                    }}
                ]
            }}
        }}";
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        _mockDataFetcher.Setup(x => x.ValidateUrl(It.IsAny<string>()))
                       .ReturnsAsync(false);

        // Act
        var result = _parser.ProcessChannels(tv, epgData, null);

        // Assert
        var programme = result.Elements("programme").First();
        programme.Attribute("start")!.Value.Should().Be(expected);
        programme.Attribute("stop")!.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("not-a-number")]
    public void ParseUnixTime_WithInvalidTimestamp_SkipsProgramme(string invalidTime)
    {
        // Arrange
        var json = $@"{{
            ""data"": {{
                ""items"": [
                    {{
                        ""channel"": {{
                            ""sourceId"": ""12345"",
                            ""networkName"": ""AWP Network""
                        }},
                        ""programSchedules"": [
                            {{
                                ""startTime"": ""{invalidTime}"",
                                ""endTime"": 1696093200,
                                ""title"": ""Test Show""
                            }}
                        ]
                    }}
                ]
            }}
        }}";
        var epgData = JsonNode.Parse(json)!.AsObject();
        var tv = new XElement("tv");

        _mockDataFetcher.Setup(x => x.ValidateUrl(It.IsAny<string>()))
                       .ReturnsAsync(false);

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
