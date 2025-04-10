using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using xmlTVGuide.Models;
using xmlTVGuide.Services.FileServices;
using xmlTVGuide.Services.ChannelMap;

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
    private const string ChannelKey = "channel";
    private const string ChannelsKey = "channels";
    private const string CallSignKey = "callSign";
    private const string ChannelIdKey = "channelId";
    private const string ChannelNoKey = "channelNo";
    private const string DisplayNameKey = "display-name";
    private const string EventsKey = "events";
    private const string ThumbnailKey = "thumbnail";
    private const string StartTimeKey = "startTime";
    private const string EndTimeKey = "endTime";
    private const string ProgramKey = "program";
    private const string TitleKey = "title";
    private const string ShortDescKey = "shortDesc";

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlTVBuilder"/> class.
    /// </summary>
    /// <param name="fileSaver">The file saver service used to save the XML file.</param>
    /// <param name="channelMapLoader">The channel map loader service used to load the channel map.</param>
    public XmlTVBuilder(IFileService fileSaver, IChannelMapLoader channelMapLoader)
    {
        _fileSaver = fileSaver ?? throw new ArgumentNullException(nameof(fileSaver), "File saver cannot be null.");
        _channelMapLoader = channelMapLoader ?? throw new ArgumentNullException(nameof(channelMapLoader), "Channel map loader cannot be null.");
    }

    /// <summary>
    /// Builds the XML TV file from the provided EPG data and saves it to the specified output path.
    /// </summary>
    /// <param name="epgData">The EPG data in JSON format.</param>
    /// <param name="channelMapPath">The path to the channel map JSON file.</param>
    /// <param name="outputPath">The output path for the generated XML file.</param>
    public void BuildXmlTV(JsonObject epgData, string channelMapPath, string outputPath)
    {
        if (epgData == null)
            throw new ArgumentNullException(nameof(epgData), "EPG data cannot be null.");

        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be null or empty.", nameof(outputPath));

        var channelMap = string.IsNullOrWhiteSpace(channelMapPath)
            ? null
            : _channelMapLoader.LoadChannelMap(channelMapPath);

        var tv = new XElement(TVKey);

        var sortedChannels = GetSortedChannels(epgData);

        foreach (var channel in sortedChannels)
        {
            var channelElement = BuildChannelElement(channel, channelMap);
            if (channelElement != null)
            {
                tv.Add(channelElement);
                AddProgrammeElements(tv, channel);
            }
        }

        SaveXmlToFile(tv, outputPath);
    }

    /// <summary>
    /// Gets the sorted channels from the EPG data.
    /// It ensures that each channel is unique based on its ID.
    /// The channels are sorted by their call sign.
    /// </summary>
    /// <param name="epgData">The EPG data in JSON format.</param>
    /// <returns>
    /// A collection of unique and sorted channels.
    /// Each channel is represented as a <see cref="JsonNode"/>.
    /// </returns>
    private IEnumerable<JsonNode> GetSortedChannels(JsonObject epgData)
    {
        var array = epgData[ChannelsKey]?.AsArray();
        if (array == null)
            return Enumerable.Empty<JsonNode>();

        var uniqueChannels = new Dictionary<string, JsonNode>();

        foreach (var c in array)
        {
            var id = c?[ChannelIdKey]?.ToString();
            if (!string.IsNullOrWhiteSpace(id) && !uniqueChannels.ContainsKey(id))
                uniqueChannels.TryAdd(id, c!);
        }

        return uniqueChannels.Values.OrderBy(c => c[CallSignKey]?.ToString() ?? "");
    }

    /// <summary>
    /// Builds a channel element for the XML TV file.
    /// This method creates an XML element for a channel, including its ID, display name, and thumbnail.
    /// </summary>
    /// <param name="channel">The channel data in JSON format.</param>
    /// <param name="channelMap">A list of channel mappings.</param>
    /// <returns>
    /// An XML element representing the channel.
    /// </returns>
    private XElement? BuildChannelElement(JsonNode? channel, List<ChannelMapDto>? channelMap)
    {
        var id = channel?[ChannelIdKey]?.ToString();
        var name = channel?[CallSignKey]?.ToString();
        var number = channel?[ChannelNoKey]?.ToString();
        var thumb = channel?[ThumbnailKey]?.ToString();

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            return null;

        var mappedChannel = channelMap?.FirstOrDefault(map => map.ChannelId == id);
        name = mappedChannel != null
            ? mappedChannel.Name
            : string.Join(' ', number, name).Trim();


        var chan = new XElement(ChannelKey, new XAttribute("id", id));
        chan.Add(new XElement(DisplayNameKey, name.Trim()));

        if (!string.IsNullOrWhiteSpace(thumb))
        {
            var iconElement = BuildIconElement(thumb);
            if (iconElement != null)
                chan.Add(iconElement);
        }

        return chan;
    }

    /// <summary>
    /// Builds an icon element for the channel.
    /// </summary>
    /// <param name="thumb">The thumbnail URL.</param>
    /// <returns>An XML element representing the icon.</returns>
    private XElement? BuildIconElement(string? thumb)
    {
        if (string.IsNullOrWhiteSpace(thumb)) return null;
        var logoUrl = thumb.StartsWith("http") ? thumb : $"https://{thumb.TrimStart('/')}";
        return new XElement("icon", new XAttribute("src", logoUrl));
    }

    /// <summary>
    /// Adds programme elements to the XML TV file.
    /// This method iterates through the events of a channel and creates XML elements for each event.
    /// </summary>
    /// <param name="tv">The XML TV element to which the programme elements will be added.</param>
    /// <param name="channel">The channel data in JSON format.</param>
    private void AddProgrammeElements(XElement tv, JsonNode? channel)
    {
        if (channel is null) return;

        var id = channel[ChannelIdKey]?.ToString();
        if (string.IsNullOrWhiteSpace(id)) return;

        var events = channel[EventsKey]?.AsArray();
        if (events is null) return;

        foreach (var ev in events)
        {
            var programme = BuildProgrammeElement(ev, id);
            if (programme != null)
                tv.Add(programme);
        }
    }

    /// <summary>
    /// Builds a programme element for the XML TV file.
    /// This method creates an XML element for a programme, including its start time, stop time, title, and description.
    /// </summary>
    /// <param name="ev">The event data in JSON format.</param>
    /// <param name="channelId">The ID of the channel to which the programme belongs.</param>
    /// <returns>
    /// An XML element representing the programme.
    /// </returns>
    private XElement? BuildProgrammeElement(JsonNode ev, string channelId)
    {
        var start = FormatTime(ev?[StartTimeKey]?.ToString());
        var stop = FormatTime(ev?[EndTimeKey]?.ToString());
        var title = ev?[ProgramKey]?[TitleKey]?.ToString();
        var desc = ev?[ProgramKey]?[ShortDescKey]?.ToString();

        if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(stop) || string.IsNullOrWhiteSpace(title))
            return null;

        var programme = new XElement("programme",
            new XAttribute("start", start),
            new XAttribute("stop", stop),
            new XAttribute("channel", channelId));

        programme.Add(new XElement("title", new XAttribute("lang", "en"), title));
        if (!string.IsNullOrWhiteSpace(desc))
            programme.Add(new XElement("desc", new XAttribute("lang", "en"), desc));

        return programme;
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

    /// <summary>
    /// Formats the time string to the required format.
    /// </summary>
    /// <param name="iso">The ISO time string.</param>
    /// <returns>
    /// A formatted time string in the format "yyyyMMddHHmmss +0000".
    /// </returns>
    private string? FormatTime(string? iso) =>
        DateTime.TryParse(iso, out var dt)
            ? dt.ToUniversalTime().ToString("yyyyMMddHHmmss") + " +0000"
            : null;
}
