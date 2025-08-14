using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using xmlTVGuide.Services.FileServices;
using xmlTVGuide.Services.ChannelMap;
using xmlTVGuide.Services.XMXTVBuilder.Parsers;

namespace xmlTVGuide.Services;

/// <summary>
/// This class is responsible for building an XML TV file from JSON EPG data.
/// It uses a channel map to map channel IDs to display names.
/// </summary>
/// <remarks>
/// The XML TV format is a standard format for exchanging TV listings.
/// This class provides methods to parse the JSON data, build the XML structure,
/// and save the XML to a file.
/// </remarks>
public class XmlTVBuilder : IXmlTVBuilder
{
    private readonly IFileService _fileSaver;
    private readonly IChannelMapLoader _channelMapLoader;

    private const string TVKey = "tv";

    private readonly IEnumerable<IGuideParser> _parsers;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlTVBuilder"/> class.
    /// </summary>
    /// <param name="fileSaver">The file saver service used to save the XML file.</param>
    /// <param name="channelMapLoader">The channel map loader service used to load the channel map.</param>
    public XmlTVBuilder(IFileService fileSaver, IChannelMapLoader channelMapLoader, IEnumerable<IGuideParser> parsers)
    {
        _fileSaver = fileSaver ?? throw new ArgumentNullException(nameof(fileSaver), "File saver cannot be null.");
        _channelMapLoader = channelMapLoader ?? throw new ArgumentNullException(nameof(channelMapLoader), "Channel map loader cannot be null.");
        _parsers = parsers ?? throw new ArgumentNullException(nameof(parsers), "Parsers cannot be null.");
    }

    public void BuildXmlTV(List<string> epgData, string channelMapPath, string outputPath)
    {
        if (epgData == null || epgData.Count == 0)
            throw new ArgumentNullException(nameof(epgData), "EPG data cannot be null or empty.");

        var channelMap = string.IsNullOrWhiteSpace(channelMapPath)
            ? null
            : _channelMapLoader.LoadChannelMap(channelMapPath);

        var tv = new XElement(TVKey);


        foreach (var data in epgData)
        {
            if (string.IsNullOrWhiteSpace(data))
                continue;

            var epg = JsonNode.Parse(data)?.AsObject()
                ?? throw new Exception("Invalid JSON structure");

            var parser = GetParser(epg);
            tv = parser.ProcessChannels(tv, epg, channelMap);
            if (tv == null)
                throw new InvalidOperationException("Failed to process channels. The resulting XML TV element is null.");
        }

        SaveXmlToFile(tv, outputPath);
    }

    /// <summary>
    /// Gets the appropriate parser for the provided EPG data.
    /// This method iterates through the available parsers and returns the first one that can parse the given EPG data.
    /// If no suitable parser is found, it throws an InvalidOperationException.
    /// This allows for flexibility in handling different EPG data formats without hardcoding specific parser logic.
    /// </summary>
    /// <param name="epgData">The EPG data in JSON format.</param>
    /// <returns>Returns an instance of <see cref="IGuideParser"/> that can handle the provided EPG data.</returns>
    /// <exception cref="InvalidOperationException">Raised when no suitable parser is found for the provided EPG data.</exception>
    private IGuideParser GetParser(JsonObject epgData)
    {
        foreach (var parser in _parsers)
        {
            if (parser.CanParse(epgData))
                return parser;
        }

        throw new InvalidOperationException("No suitable parser found for the provided EPG data.");
    }

    /// <summary>
    /// Saves the XML TV element to a file.
    /// This method uses the file saver service to save the XML document to the specified output path.
    /// </summary>
    /// <param name="tv">The XML TV element to be saved.</param>
    /// <param name="outputPath">The output path for the XML file.</param>
    /// <exception cref="InvalidOperationException"></exception>
    private void SaveXmlToFile(XElement tv, string outputPath)
    {
        try
        {
            _fileSaver.SaveFile(
                new XDocument(new XDeclaration("1.0", "utf-8", "yes"), tv)
                , outputPath
            );
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save XML to file: {outputPath}. Error: {ex.Message}", ex);
        }
    }
}
