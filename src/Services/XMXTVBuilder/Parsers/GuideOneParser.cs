using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using xmlTVGuide.Models;

namespace xmlTVGuide.Services.XMXTVBuilder.Parsers;

/// <summary>
/// Parser for GuideOne EPG data.
/// This parser processes EPG data from GuideOne, extracting channel and program information.
/// It builds the XML structure required for the xmlTV format.
/// </summary>
public class GuideOneParser
: ParserBase
, IGuideParser
{
    /// <summary>
    /// Checks if the parser can handle the provided EPG data.
    /// This method checks the basic structure of the EPG data to determine if it is compatible
    /// with the GuideOne format. It looks for the presence of channels and typical fields.
    /// </summary>
    /// <param name="epgData"><see cref="JsonObject"/> representing the EPG data.</param>
    /// <returns>
    /// A <see cref="bool"/> indicating whether the parser can handle the provided EPG data.
    /// </returns>
    public override bool CanParse(JsonObject epgData)
    {
        // Basic structure check
        if (!epgData.TryGetPropertyValue(ChannelsKey, out var channelsNode))
            return false;

        var channels = channelsNode as JsonArray;
        if (channels is null || channels.Count == 0)
            return false;

        // Sample deeper check: look for typical GuideOne fields
        var sample = channels[0];
        return sample?[CallSignKey] is not null &&
            sample?[EventsKey] is JsonArray &&
            sample?[ChannelIdKey] is not null &&
            sample?[ProgramKey] is null; // Optional if only top-level structure matters
    }

    public override XElement ProcessChannels(XElement tv, JsonObject epg, List<ChannelMapDto>? channelMap)
    {
        var sortedChannels = GetSortedChannels(epg);
        foreach (var channel in sortedChannels)
        {
            var id = channel?[ChannelIdKey]?.ToString();
            if (string.IsNullOrWhiteSpace(id)) continue;

            // Check if the channel already exists in the tv element
            if (tv.Elements(ChannelKey).Any(e => e.Attribute(IdKey)?.Value == id))
                continue;

            var channelElement = BuildChannelElement(channel, channelMap);
            if (channelElement != null)
            {
                tv.Add(channelElement);
                AddProgrammeElements(tv, channel);
            }
        }
        return tv;
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


        var chan = new XElement(ChannelKey, new XAttribute(IdKey, id));
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
        return new XElement(IconKey, new XAttribute(SrcKey, logoUrl));
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
            if (ev is not JsonObject)
                continue;

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

        var programme = new XElement(ProgrammeKey,
            new XAttribute(StartKey, start),
            new XAttribute(StopKey, stop),
            new XAttribute(ChannelKey, channelId));

        programme.Add(new XElement(TitleKey, new XAttribute(LangKey, EnKey), title));
        if (!string.IsNullOrWhiteSpace(desc))
            programme.Add(new XElement(DescKey, new XAttribute(LangKey, EnKey), desc));

        return programme;
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
