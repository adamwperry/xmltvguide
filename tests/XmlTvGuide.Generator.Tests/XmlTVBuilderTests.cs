using FluentAssertions;
using Moq;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using xmlTVGuide.Models;
using xmlTVGuide.Services;
using xmlTVGuide.Services.ChannelMap;
using xmlTVGuide.Services.FileServices;
using xmlTVGuide.Services.XMXTVBuilder.Parsers;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class XmlTVBuilderTests
{
    [Fact]
    public void BuildXmlTV_CleansLeadingChannelNumbersWithoutReorderingGuide()
    {
        // Arrange
        XDocument? savedDocument = null;
        var fileService = new Mock<IFileService>();
        fileService
            .Setup(service => service.SaveFile(It.IsAny<XDocument>(), "guide.xml"))
            .Callback<XDocument, string>((document, _) => savedDocument = document)
            .Returns(true);

        var builder = new XmlTVBuilder(
            fileService.Object,
            new Mock<IChannelMapLoader>().Object,
            new[] { new StubGuideParser() });

        // Act
        builder.BuildXmlTV(new List<string> { """{"format":"stub"}""" }, string.Empty, "guide.xml");

        // Assert
        savedDocument.Should().NotBeNull();
        var nodes = savedDocument!.Root!.Elements().ToList();

        nodes.Select(node => node.Name.LocalName)
            .Should()
            .Equal("channel", "programme", "channel", "programme", "channel", "programme");

        nodes.OfType<XElement>()
            .Where(node => node.Name == "channel")
            .Select(channel => channel.Element("display-name")!.Value)
            .Should()
            .Equal("CMDTVHD", "Bravo", "A&E");
    }

    private sealed class StubGuideParser : IGuideParser
    {
        public bool CanParse(JsonObject epg) => true;

        public XElement ProcessChannels(XElement tv, JsonObject epg, List<ChannelMapDto>? channelMap)
        {
            tv.Add(
                Channel("c", "196 CMDTVHD"),
                Programme("c"),
                Channel("b", "Bravo"),
                Programme("b"),
                Channel("a", "12 A&E"),
                Programme("a"));

            return tv;
        }

        private static XElement Channel(string id, string displayName) =>
            new("channel",
                new XAttribute("id", id),
                new XElement("display-name", displayName));

        private static XElement Programme(string channelId) =>
            new("programme",
                new XAttribute("channel", channelId),
                new XAttribute("start", "20260501140000 +0000"),
                new XAttribute("stop", "20260501150000 +0000"),
                new XElement("title", "Test Program"));
    }
}
